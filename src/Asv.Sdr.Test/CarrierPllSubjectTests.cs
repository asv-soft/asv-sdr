using System;
using System.Reactive.Subjects;
using Xunit;

namespace Asv.Sdr.Test
{
    public class CarrierPllSubjectTests
    {
        [Fact]
        public void CarrierPllTracksComplexToneOffset()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 4_096;
            const double nominalFrequencyHz = 15_000.0;
            const double offsetHz = 350.0;
            const double amplitude = 1.0;

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            ReaderIqCarrierPllResult received = null;
            using var subscription = source
                .TrackCarrierPll(
                    new ReaderIqCarrierPllOptions(sampleRate, nominalFrequencyHz)
                    {
                        LoopBandwidthHz = 600.0,
                        MaxFrequencyOffsetHz = 1_000.0,
                        LockThresholdRad = 0.4,
                    }
                )
                .Subscribe(x => received = x);

            for (var window = 0; window < 24; window++)
            {
                source.Publish(
                    CreateComplexTone(
                        sampleCount,
                        sampleRate,
                        nominalFrequencyHz + offsetHz,
                        amplitude,
                        window * sampleCount
                    )
                );
            }

            Assert.NotNull(received);
            Assert.InRange(received.FrequencyOffsetHz, offsetHz - 2.0, offsetHz + 2.0);
            Assert.True(received.IsLocked);
        }

        [Fact]
        public void CarrierPllTracksFloatComplexToneOffset()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 4_096;
            const double nominalFrequencyHz = 15_000.0;
            const double offsetHz = -275.0;
            const double amplitude = 1.0;

            using var source = new TestReaderIqSubject<float>(sampleCount * 2);
            var received = 0.0;
            using var subscription = source
                .GetPllFrequencyOffset(
                    new ReaderIqCarrierPllOptions(sampleRate, nominalFrequencyHz)
                    {
                        LoopBandwidthHz = 600.0,
                        MaxFrequencyOffsetHz = 1_000.0,
                    }
                )
                .Subscribe(x => received = x);

            for (var window = 0; window < 24; window++)
            {
                source.Publish(
                    CreateComplexToneFloat(
                        sampleCount,
                        sampleRate,
                        nominalFrequencyHz + offsetHz,
                        amplitude,
                        window * sampleCount
                    )
                );
            }

            Assert.InRange(received, offsetHz - 2.0, offsetHz + 2.0);
        }

        [Fact]
        public void CarrierPllClampsFrequencyOffset()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 4_096;
            const double nominalFrequencyHz = 15_000.0;
            const double offsetHz = 800.0;
            const double maxOffsetHz = 200.0;
            const double amplitude = 1.0;

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            var received = 0.0;
            using var subscription = source
                .GetPllFrequencyOffset(
                    new ReaderIqCarrierPllOptions(sampleRate, nominalFrequencyHz)
                    {
                        LoopBandwidthHz = 600.0,
                        MaxFrequencyOffsetHz = maxOffsetHz,
                    }
                )
                .Subscribe(x => received = x);

            for (var window = 0; window < 6; window++)
            {
                source.Publish(
                    CreateComplexTone(
                        sampleCount,
                        sampleRate,
                        nominalFrequencyHz + offsetHz,
                        amplitude,
                        window * sampleCount
                    )
                );
            }

            Assert.InRange(received, maxOffsetHz - 1.0, maxOffsetHz + 1.0);
        }

        private static double[] CreateComplexTone(
            int sampleCount,
            int sampleRate,
            double frequencyHz,
            double amplitude,
            int sampleOffset
        )
        {
            var buffer = new double[sampleCount * 2];
            var angleStep = 2.0 * Math.PI * frequencyHz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                var angle = angleStep * (i + sampleOffset);
                buffer[i * 2] = amplitude * Math.Cos(angle);
                buffer[i * 2 + 1] = amplitude * Math.Sin(angle);
            }

            return buffer;
        }

        private static float[] CreateComplexToneFloat(
            int sampleCount,
            int sampleRate,
            double frequencyHz,
            double amplitude,
            int sampleOffset
        )
        {
            var buffer = new float[sampleCount * 2];
            var angleStep = 2.0 * Math.PI * frequencyHz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                var angle = angleStep * (i + sampleOffset);
                buffer[i * 2] = (float)(amplitude * Math.Cos(angle));
                buffer[i * 2 + 1] = (float)(amplitude * Math.Sin(angle));
            }

            return buffer;
        }

        private sealed class TestReaderIqSubject<T> : IReaderIqSubject<T>
        {
            private readonly Subject<Memory<T>> _subject = new();

            public TestReaderIqSubject(int outputBufferSize)
            {
                OutputBufferSize = outputBufferSize;
            }

            public int OutputBufferSize { get; }

            public IDisposable Subscribe(IObserver<Memory<T>> observer)
            {
                return _subject.Subscribe(observer);
            }

            public void Publish(T[] buffer)
            {
                _subject.OnNext(buffer);
            }

            public void Dispose()
            {
                _subject.OnCompleted();
                _subject.Dispose();
            }
        }
    }
}
