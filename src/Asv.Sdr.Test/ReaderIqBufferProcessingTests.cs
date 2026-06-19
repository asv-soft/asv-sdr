using System;
using System.Reactive.Subjects;
using Xunit;

namespace Asv.Sdr.Test
{
    public class ReaderIqMagnitudeTests
    {
        [Fact]
        public void DoubleMagnitudeMatchesScalarAbs()
        {
            var input = new[]
            {
                3.0,
                4.0,
                -5.0,
                12.0,
                0.0,
                -0.0,
                1e-200,
                -1e-200,
                double.PositiveInfinity,
                double.NaN,
                double.NaN,
                2.0,
                8.0,
                15.0,
                1e200,
                1e200,
                -7.0,
                24.0
            };

            var received = PublishMagnitude(input);

            Assert.Equal(input.Length, received.Length);
            for (var index = 0; index < input.Length; index += 2)
            {
                AssertMagnitudeEqual(DspMathEx.Abs(input[index], input[index + 1]), received[index]);
                Assert.Equal(0.0, received[index + 1]);
            }
        }

        [Fact]
        public void FloatMagnitudeMatchesScalarAbs()
        {
            var input = new[]
            {
                3.0f,
                4.0f,
                -5.0f,
                12.0f,
                0.0f,
                -0.0f,
                float.Epsilon,
                -float.Epsilon,
                float.PositiveInfinity,
                float.NaN,
                float.NaN,
                2.0f,
                8.0f,
                15.0f,
                -7.0f,
                24.0f,
                0.25f,
                -0.75f
            };

            var received = PublishMagnitude(input);

            Assert.Equal(input.Length, received.Length);
            for (var index = 0; index < input.Length; index += 2)
            {
                AssertMagnitudeEqual(DspMathEx.Abs(input[index], input[index + 1]), received[index]);
                Assert.Equal(0.0, received[index + 1]);
            }
        }

        private static double[] PublishMagnitude(double[] input)
        {
            using var source = new TestReaderIqSubject<double>(input.Length);
            using var magnitude = source.Magnitude();
            var received = Array.Empty<double>();
            using var subscription = magnitude.Subscribe(buffer => received = buffer.ToArray());

            source.Publish(input);

            return received;
        }

        private static double[] PublishMagnitude(float[] input)
        {
            using var source = new TestReaderIqSubject<float>(input.Length);
            using var magnitude = source.Magnitude();
            var received = Array.Empty<double>();
            using var subscription = magnitude.Subscribe(buffer => received = buffer.ToArray());

            source.Publish(input);

            return received;
        }

        private static void AssertMagnitudeEqual(double expected, double actual)
        {
            if (double.IsNaN(expected))
            {
                Assert.True(double.IsNaN(actual), $"Expected NaN, actual {actual:R}.");
                return;
            }

            if (double.IsInfinity(expected))
            {
                Assert.Equal(expected, actual);
                return;
            }

            var tolerance = Math.Abs(expected) * 1e-12;
            if (tolerance == 0.0)
            {
                Assert.Equal(expected, actual);
                return;
            }

            Assert.True(
                Math.Abs(expected - actual) <= tolerance,
                $"Expected {expected:R}, actual {actual:R}."
            );
        }
    }

    public class ReaderIqWindowFilterTests
    {
        [Fact]
        public void DoubleWindowFilterMatchesScalarMultiplication()
        {
            var input = CreateDoubleInput(19);
            var filter = WindowFilters.Create(WindowFilterEnum.Hann, input.Length);
            var received = PublishWindow(input, WindowFilterEnum.Hann);

            Assert.Equal(input.Length, received.Length);
            for (var index = 0; index < input.Length; index++)
            {
                Assert.Equal(input[index] * filter[index], received[index], 12);
            }
        }

        [Fact]
        public void FloatWindowFilterMatchesScalarMultiplication()
        {
            var input = CreateFloatInput(19);
            var filter = WindowFilters.Create(WindowFilterEnum.Hamming, input.Length);
            var received = PublishWindow(input, WindowFilterEnum.Hamming);

            Assert.Equal(input.Length, received.Length);
            for (var index = 0; index < input.Length; index++)
            {
                Assert.Equal(input[index] * filter[index], received[index], 12);
            }
        }

        [Fact]
        public void NoneWindowContainsOnlyOnes()
        {
            var filter = WindowFilters.None(19);

            Assert.All(filter, value => Assert.Equal(1.0, value));
        }

        private static double[] PublishWindow(double[] input, WindowFilterEnum filter)
        {
            using var source = new TestReaderIqSubject<double>(input.Length);
            using var window = source.WindowFilter(filter);
            var received = Array.Empty<double>();
            using var subscription = window.Subscribe(buffer => received = buffer.ToArray());

            source.Publish(input);

            return received;
        }

        private static double[] PublishWindow(float[] input, WindowFilterEnum filter)
        {
            using var source = new TestReaderIqSubject<float>(input.Length);
            using var window = source.WindowFilter(filter);
            var received = Array.Empty<double>();
            using var subscription = window.Subscribe(buffer => received = buffer.ToArray());

            source.Publish(input);

            return received;
        }

        private static double[] CreateDoubleInput(int length)
        {
            var input = new double[length];
            for (var index = 0; index < input.Length; index++)
            {
                input[index] = index % 2 == 0 ? index + 0.25 : -index - 0.75;
            }

            return input;
        }

        private static float[] CreateFloatInput(int length)
        {
            var input = new float[length];
            for (var index = 0; index < input.Length; index++)
            {
                input[index] = index % 2 == 0 ? index + 0.25f : -index - 0.75f;
            }

            return input;
        }
    }

    internal sealed class TestReaderIqSubject<T> : IReaderIqSubject<T>
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
