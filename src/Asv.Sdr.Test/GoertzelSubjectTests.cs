using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using Xunit;

namespace Asv.Sdr.Test
{
    public class GoertzelSubjectTests
    {
        [Fact]
        public void GoertzelMeasuresExactComplexFrequency()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 16_384;
            const double frequencyHz = 30.0;
            const double amplitude = 3.0;
            const double phase = 0.7;

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            var received = default(ReaderIqGoertzelResult);
            using var subscription = source
                .Goertzel(sampleRate, frequencyHz)
                .Subscribe(x => received = x);

            source.Publish(
                CreateComplexTone(sampleCount, sampleRate, frequencyHz, amplitude, phase)
            );

            Assert.True(Math.Abs(amplitude * sampleCount - received.Magnitude) < 1e-6);
            Assert.True(Math.Abs(DspMathEx.GetDistanceAngleRad(received.Phase, phase)) < 1e-10);
        }

        [Fact]
        public void GoertzelAmRemovesDcBeforeMeasuringTargetFrequency()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 16_384;
            const double frequencyHz = sampleRate * 16.0 / sampleCount;
            const double modulationDepth = 0.35;

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            var received = 0.0;
            using var subscription = source
                .GetGoertzelAm(sampleRate, frequencyHz)
                .Subscribe(x => received = x);

            source.Publish(CreateRealAm(sampleCount, sampleRate, frequencyHz, modulationDepth));

            Assert.True(Math.Abs(modulationDepth - received) < 1e-10);
        }

        [Fact]
        public void GoertzelFrequencyOffsetTracksToneOffset()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 9_600;
            const double nominalFrequencyHz = 30.0;
            const double offsetHz = 1.25;
            const double amplitude = 3.0;
            var received = new List<double>();

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            using var subscription = source
                .GetGoertzelFrequencyOffset(sampleRate, nominalFrequencyHz)
                .Subscribe(received.Add);

            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    0
                )
            );
            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    sampleCount
                )
            );
            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    sampleCount * 2
                )
            );

            Assert.Equal(2, received.Count);
            Assert.All(received, x => Assert.True(Math.Abs(offsetHz - x) < 1e-9));
        }

        [Fact]
        public void GoertzelFrequencyOffsetIsIndependentFromWindowSize()
        {
            const int sampleRate = 96_000;
            const double offsetHz = -1.75;
            const double amplitude = 2.0;

            var shortWindowOffset = MeasureFrequencyOffset(
                sampleRate,
                9_600,
                0,
                offsetHz,
                amplitude
            );
            var longWindowOffset = MeasureFrequencyOffset(
                sampleRate,
                16_000,
                0,
                offsetHz,
                amplitude
            );

            Assert.True(Math.Abs(offsetHz - shortWindowOffset) < 1e-9);
            Assert.True(Math.Abs(offsetHz - longWindowOffset) < 1e-9);
        }

        [Fact]
        public void GoertzelFrequencyOffsetTracksRealToneOffset()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 9_600;
            const double nominalFrequencyHz = 30.0;
            const double offsetHz = 0.25;
            const double amplitude = 0.3;
            var received = new List<double>();

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            using var subscription = source
                .GetGoertzelFrequencyOffset(sampleRate, nominalFrequencyHz)
                .Subscribe(received.Add);

            source.Publish(
                CreateRealTone(sampleCount, sampleRate, nominalFrequencyHz + offsetHz, amplitude, 0)
            );
            source.Publish(
                CreateRealTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    sampleCount
                )
            );
            source.Publish(
                CreateRealTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    sampleCount * 2
                )
            );

            Assert.Equal(2, received.Count);
            Assert.All(received, x => Assert.True(Math.Abs(offsetHz - x) < 0.02));
        }

        [Fact]
        public void GoertzelFrequencyOffsetTracksFloatToneOffset()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 9_600;
            const double nominalFrequencyHz = 0.0;
            const double offsetHz = -1.25;
            const double amplitude = 1.5;
            var received = new List<double>();

            using var source = new TestReaderIqSubject<float>(sampleCount * 2);
            using var subscription = source
                .GetGoertzelFrequencyOffset(sampleRate, nominalFrequencyHz)
                .Subscribe(received.Add);

            source.Publish(
                CreateComplexToneFloat(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    0
                )
            );
            source.Publish(
                CreateComplexToneFloat(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    sampleCount
                )
            );
            source.Publish(
                CreateComplexToneFloat(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    sampleCount * 2
                )
            );

            Assert.Equal(2, received.Count);
            Assert.All(received, x => Assert.True(Math.Abs(offsetHz - x) < 1e-6));
        }

        private static double[] CreateComplexTone(
            int sampleCount,
            int sampleRate,
            double frequencyHz,
            double amplitude,
            double phase
        )
        {
            return CreateComplexTone(sampleCount, sampleRate, frequencyHz, amplitude, phase, 0);
        }

        private static double[] CreateComplexTone(
            int sampleCount,
            int sampleRate,
            double frequencyHz,
            double amplitude,
            double phase,
            int sampleOffset
        )
        {
            var buffer = new double[sampleCount * 2];
            var angleStep = 2.0 * Math.PI * frequencyHz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                var angle = angleStep * (i + sampleOffset) + phase;
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
            double phase,
            int sampleOffset
        )
        {
            var buffer = new float[sampleCount * 2];
            var angleStep = 2.0 * Math.PI * frequencyHz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                var angle = angleStep * (i + sampleOffset) + phase;
                buffer[i * 2] = (float)(amplitude * Math.Cos(angle));
                buffer[i * 2 + 1] = (float)(amplitude * Math.Sin(angle));
            }

            return buffer;
        }

        private static double MeasureFrequencyOffset(
            int sampleRate,
            int sampleCount,
            double nominalFrequencyHz,
            double offsetHz,
            double amplitude
        )
        {
            var received = 0.0;
            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            using var subscription = source
                .GetGoertzelFrequencyOffset(sampleRate, nominalFrequencyHz)
                .Subscribe(x => received = x);

            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    0
                )
            );
            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    sampleCount
                )
            );
            return received;
        }

        private static double[] CreateRealAm(
            int sampleCount,
            int sampleRate,
            double frequencyHz,
            double modulationDepth
        )
        {
            var buffer = new double[sampleCount * 2];
            var angleStep = 2.0 * Math.PI * frequencyHz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                buffer[i * 2] = 1.0 + modulationDepth * Math.Cos(angleStep * i);
            }

            return buffer;
        }

        private static double[] CreateRealTone(
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
                buffer[i * 2] = amplitude * Math.Cos(angleStep * (i + sampleOffset));
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
