using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Xunit;

namespace Asv.Sdr.Test
{
    public class ReaderIqParallelSubjectTests
    {
        [Fact]
        public async Task SubscriberCanUnsubscribeInsideOnNext()
        {
            var source = new TestReaderIqSubject<double>(2);
            using var parallel = new ReaderIqParallelSubject<double>(source);
            var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            IDisposable? subscription = null;

            subscription = parallel.Subscribe(new CallbackObserver<Memory<double>>(_ =>
            {
                subscription?.Dispose();
                received.TrySetResult();
            }));

            var publishTask = Task.Run(
                () => source.Publish(new double[] { 1, 0 }),
                TestContext.Current.CancellationToken
            );

            var completedPublishTask = await Task.WhenAny(
                publishTask,
                Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken)
            );
            Assert.Same(publishTask, completedPublishTask);

            var completedReceivedTask = await Task.WhenAny(
                received.Task,
                Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken)
            );
            Assert.Same(received.Task, completedReceivedTask);
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
