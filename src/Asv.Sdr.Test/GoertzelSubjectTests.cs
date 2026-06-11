using System;
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
            using var subscription = source.Goertzel(sampleRate, frequencyHz).Subscribe(x => received = x);

            source.Publish(CreateComplexTone(sampleCount, sampleRate, frequencyHz, amplitude, phase));

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
            using var subscription = source.GetGoertzelAm(sampleRate, frequencyHz).Subscribe(x => received = x);

            source.Publish(CreateRealAm(sampleCount, sampleRate, frequencyHz, modulationDepth));

            Assert.True(Math.Abs(modulationDepth - received) < 1e-10);
        }

        private static double[] CreateComplexTone(
            int sampleCount,
            int sampleRate,
            double frequencyHz,
            double amplitude,
            double phase)
        {
            var buffer = new double[sampleCount * 2];
            var angleStep = 2.0 * Math.PI * frequencyHz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                var angle = angleStep * i + phase;
                buffer[i * 2] = amplitude * Math.Cos(angle);
                buffer[i * 2 + 1] = amplitude * Math.Sin(angle);
            }

            return buffer;
        }

        private static double[] CreateRealAm(
            int sampleCount,
            int sampleRate,
            double frequencyHz,
            double modulationDepth)
        {
            var buffer = new double[sampleCount * 2];
            var angleStep = 2.0 * Math.PI * frequencyHz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                buffer[i * 2] = 1.0 + modulationDepth * Math.Cos(angleStep * i);
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
