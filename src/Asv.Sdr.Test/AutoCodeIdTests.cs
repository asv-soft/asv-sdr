using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using Xunit;

namespace Asv.Sdr.Test
{
    public class AutoCodeIdTests
    {
        // S O S in international Morse.
        private static readonly string[] Sos = { "...", "---", "..." };

        [Fact]
        public void AutoCodeId_RepeatedIdent_DecodesAndSelfCalibrates()
        {
            using var src = new Subject<double>();
            var received = new List<CodeId>();
            using var sub = src.AutoCodeId(100, 10_000).Subscribe(received.Add); // 10 ms / window

            // dot 100 ms, dash 300 ms, symbol gap 100 ms, char gap 300 ms, word gap 700 ms.
            FeedRepeatedIdent(src, Sos, repeats: 8, dotW: 10, dashW: 30, symW: 10, groupGapW: 70);

            Assert.NotEmpty(received);
            Assert.Equal("SOS", received[^1].Value);
            Assert.InRange(received[^1].DotTimeMs, 75, 125);
            Assert.InRange(received[^1].DashTimeMs, 260, 340);
            Assert.InRange(received[^1].SymbolPauseMs, 70, 130);
            Assert.InRange(received[^1].CharPauseMs, 240, 360);
        }

        [Fact]
        public void AutoCodeId_AdaptsToDifferentDotLength()
        {
            using var src = new Subject<double>();
            var received = new List<CodeId>();
            using var sub = src.AutoCodeId(100, 10_000).Subscribe(received.Add);

            // Faster keying: dot 50 ms, dash 150 ms. The decoder must self-calibrate.
            FeedRepeatedIdent(src, Sos, repeats: 10, dotW: 5, dashW: 15, symW: 5, groupGapW: 35);

            Assert.NotEmpty(received);
            Assert.Equal("SOS", received[^1].Value);
            Assert.InRange(received[^1].DotTimeMs, 30, 70);
            Assert.InRange(received[^1].DashTimeMs, 120, 180);
        }

        [Fact]
        public void AutoCodeId_AllDashIdent_CalibratesWithoutUnitLock()
        {
            using var src = new Subject<double>();
            var received = new List<CodeId>();
            using var sub = src.AutoCodeId(100, 10_000).Subscribe(received.Add);

            // "TT" has no 1-unit elements at all; the unit must not lock onto 3x the dot.
            FeedRepeatedIdent(src, new[] { "-", "-" }, repeats: 10, dotW: 10, dashW: 30, symW: 10, groupGapW: 70);

            Assert.NotEmpty(received);
            Assert.Equal("TT", received[^1].Value);
            Assert.InRange(received[^1].DashTimeMs, 260, 340);
        }

        [Fact]
        public void AutoCodeId_RecoversFromSingleCorruptedElement()
        {
            using var src = new Subject<double>();
            var received = new List<CodeId>();
            using var sub = src.AutoCodeId(100, 10_000).Subscribe(received.Add);

            Push(src, 0.0, 80);
            for (var r = 0; r < 10; r++)
            {
                // Corrupt the middle character of the 4th repeat (drop the tone): O -> still recoverable.
                var morse = r == 3 ? new[] { "...", "..", "..." } : Sos;
                PushWord(src, morse, 0.3, 0.0, 10, 30, 10);
                Push(src, 0.0, 70);
            }
            Push(src, 0.0, 80);

            Assert.NotEmpty(received);
            Assert.Equal("SOS", received[^1].Value); // majority vote heals the single bad repeat
        }

        [Fact]
        public void AutoCodeId_PureNoise_PublishesNothing()
        {
            using var src = new Subject<double>();
            var received = new List<CodeId>();
            using var sub = src.AutoCodeId(100, 10_000).Subscribe(received.Add);

            var noise = new Random(1234);
            for (var i = 0; i < 4000; i++)
            {
                Push(src, 0.02 + (noise.NextDouble() * 0.01), 1); // low, structureless
            }

            Assert.Empty(received);
        }

        private static void FeedRepeatedIdent(
            Subject<double> src,
            string[] morse,
            int repeats,
            int dotW,
            int dashW,
            int symW,
            int groupGapW
        )
        {
            Push(src, 0.0, 80); // leading silence / level warm-up
            for (var r = 0; r < repeats; r++)
            {
                PushWord(src, morse, 0.3, 0.0, dotW, dashW, symW);
                Push(src, 0.0, groupGapW);
            }

            Push(src, 0.0, 80);
        }

        private static void PushWord(
            Subject<double> src,
            string[] morse,
            double on,
            double off,
            int dotW,
            int dashW,
            int symW
        )
        {
            for (var c = 0; c < morse.Length; c++)
            {
                var symbol = morse[c];
                for (var s = 0; s < symbol.Length; s++)
                {
                    Push(src, on, symbol[s] == '.' ? dotW : dashW);
                    if (s < symbol.Length - 1)
                    {
                        Push(src, off, symW);
                    }
                }

                if (c < morse.Length - 1)
                {
                    Push(src, off, symW * 3);
                }
            }
        }

        private static void Push(ISubject<double> src, double value, int count)
        {
            for (var i = 0; i < count; i++)
            {
                src.OnNext(value);
            }
        }
    }
}
