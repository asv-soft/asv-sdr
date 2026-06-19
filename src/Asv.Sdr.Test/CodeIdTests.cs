using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using Xunit;

namespace Asv.Sdr.Test
{
    public class CodeIdTests
    {
        [Fact]
        public void CodeId_AutoDotTime_DecodesWithoutDotTime()
        {
            using var src = new Subject<double>();
            using var done = new AutoResetEvent(false);
            var received = new List<CodeId>();

            src.CodeId(0.05, 0.8, 100, 10_000).Subscribe(x =>
            {
                received.Add(x);
                done.Set();
            });

            Push(src, 0.3, 10);
            Push(src, 0.0, 10);
            Push(src, 0.3, 30);
            Push(src, 0.0, 70);
            Push(src, 0.3, 2); // >= SignalDebounceWindows so the trailing onset confirms and flushes the word

            Assert.True(done.WaitOne(TimeSpan.FromSeconds(1)));
            Assert.Single(received);
            Assert.Equal("A", received[0].Value);
            Assert.Equal(100, received[0].DotTimeMs, 5);
            Assert.Equal(300, received[0].DashTimeMs, 5);
        }

        [Fact]
        public void CodeId_LeadingSilenceAndVorTiming_DecodesFullWord()
        {
            using var src = new Subject<double>();
            using var done = new AutoResetEvent(false);
            var received = new List<CodeId>();

            src.CodeId(0.05, 0.8, 100, 10_000).Subscribe(x =>
            {
                received.Add(x);
                done.Set();
            });

            Push(src, 0.0, 500);
            PushMorse(src, "... --- ... ..-", 0.3, 0.0, 15, 30, 45);
            Push(src, 0.0, 500);
            Push(src, 0.3, 2); // >= SignalDebounceWindows so the trailing onset confirms and flushes the word

            Assert.True(done.WaitOne(TimeSpan.FromSeconds(1)));
            Assert.Single(received);
            Assert.Equal("SOSU", received[0].Value);
            Assert.Equal(150, received[0].DotTimeMs, 10);
            Assert.Equal(300, received[0].DashTimeMs, 10);
            Assert.Equal(150, received[0].SymbolPauseMs, 10);
            Assert.Equal(450, received[0].CharPauseMs, 10);
        }

        [Fact]
        public void CodeId_RepeatedVorCodeWithoutWordPause_DecodesOnePeriod()
        {
            using var src = new Subject<double>();
            using var done = new AutoResetEvent(false);
            var received = new List<CodeId>();

            src.CodeId(0.05, 0.8, 100, 10_000).Subscribe(x =>
            {
                received.Add(x);
                done.Set();
            });

            Push(src, 0.0, 500);
            PushMorse(src, "... --- ... ..- ... --- ... ..-", 0.3, 0.0, 15, 30, 45);
            Push(src, 0.0, 45);
            Push(src, 0.3, 2); // >= SignalDebounceWindows so the trailing onset confirms and flushes the word

            Assert.True(done.WaitOne(TimeSpan.FromSeconds(1)));
            Assert.Single(received);
            Assert.Equal("SOSU", received[0].Value);
            Assert.Equal(150, received[0].DotTimeMs, 10);
            Assert.Equal(300, received[0].DashTimeMs, 10);
            Assert.Equal(150, received[0].SymbolPauseMs, 10);
            Assert.Equal(450, received[0].CharPauseMs, 10);
        }

        [Fact]
        public void CodeId_FixedDotTime_DecodesFullWord()
        {
            using var src = new Subject<double>();
            using var done = new AutoResetEvent(false);
            var received = new List<CodeId>();

            src.CodeId(0.05, 0.8, 100, 10_000, 150).Subscribe(x =>
            {
                received.Add(x);
                done.Set();
            });

            Push(src, 0.0, 500);
            PushMorse(src, "... --- ... ..-", 0.3, 0.0, 15, 30, 45);
            Push(src, 0.0, 500);
            Push(src, 0.3, 2); // >= SignalDebounceWindows so the trailing onset confirms and flushes the word

            Assert.True(done.WaitOne(TimeSpan.FromSeconds(1)));
            Assert.Single(received);
            Assert.Equal("SOSU", received[0].Value);
            Assert.Equal(150, received[0].DotTimeMs, 10);
            Assert.Equal(300, received[0].DashTimeMs, 10);
            Assert.Equal(150, received[0].SymbolPauseMs, 10);
            Assert.Equal(450, received[0].CharPauseMs, 10);
        }

        private static void Push(ISubject<double> src, double value, int count)
        {
            for (var i = 0; i < count; i++)
            {
                src.OnNext(value);
            }
        }

        private static void PushMorse(
            ISubject<double> src,
            string code,
            double high,
            double low,
            int dotCount,
            int dashCount,
            int charPauseCount)
        {
            var characters = code.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var charIndex = 0; charIndex < characters.Length; charIndex++)
            {
                var symbols = characters[charIndex];
                for (var symbolIndex = 0; symbolIndex < symbols.Length; symbolIndex++)
                {
                    Push(src, high, symbols[symbolIndex] == '.' ? dotCount : dashCount);
                    if (symbolIndex < symbols.Length - 1)
                    {
                        Push(src, low, dotCount);
                    }
                }

                if (charIndex < characters.Length - 1)
                {
                    Push(src, low, charPauseCount);
                }
            }
        }
    }
}
