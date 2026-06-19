using System;
using System.Reactive.Subjects;
using Xunit;

namespace Asv.Sdr.Test
{
    public class ReaderIqFreqShiftTests
    {
        [Fact]
        public void DoubleAppliesComplexMultiplication()
        {
            using var source = new TestReaderIqSubject<double>(4);
            using var shift = source.FrequencyShift(4.0, 1);
            var output = Array.Empty<double>();

            shift.Subscribe(new CallbackObserver<Memory<double>>(memory => output = memory.ToArray()));

            source.Publish(new double[] { 1.0, 2.0, 2.0, 3.0 });

            AssertNearlyEqual(1.0, output[0]);
            AssertNearlyEqual(2.0, output[1]);
            AssertNearlyEqual(-3.0, output[2]);
            AssertNearlyEqual(2.0, output[3]);
        }

        [Fact]
        public void FloatAppliesComplexMultiplication()
        {
            using var source = new TestReaderIqSubject<float>(4);
            using var shift = source.FrequencyShift(4.0, 1);
            var output = Array.Empty<double>();

            shift.Subscribe(new CallbackObserver<Memory<double>>(memory => output = memory.ToArray()));

            source.Publish(new[] { 1.0f, 2.0f, 2.0f, 3.0f });

            AssertNearlyEqual(1.0, output[0]);
            AssertNearlyEqual(2.0, output[1]);
            AssertNearlyEqual(-3.0, output[2]);
            AssertNearlyEqual(2.0, output[3]);
        }

        private static void AssertNearlyEqual(double expected, double actual)
        {
            Assert.True(
                Math.Abs(expected - actual) < 1E-12,
                $"Expected {expected}, got {actual}."
            );
        }

        private sealed class CallbackObserver<T> : IObserver<T>
        {
            private readonly Action<T> _onNext;

            public CallbackObserver(Action<T> onNext)
            {
                _onNext = onNext;
            }

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(T value)
            {
                _onNext(value);
            }
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
