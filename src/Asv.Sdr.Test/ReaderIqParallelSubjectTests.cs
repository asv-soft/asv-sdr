using System;
using System.Collections.Concurrent;
using System.Reactive.Subjects;
using System.Threading;
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

        [Fact]
        public async Task BlockingSubscriberDoesNotPreventOtherSubscribersFromReceivingSample()
        {
            var source = new TestReaderIqSubject<double>(2);
            using var parallel = new ReaderIqParallelSubject<double>(source);
            using var firstCanFinish = new ManualResetEventSlim(false);
            var firstStarted = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            var secondReceived = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously
            );

            parallel.Subscribe(new CallbackObserver<Memory<double>>(_ =>
            {
                firstStarted.TrySetResult();
                firstCanFinish.Wait(TestContext.Current.CancellationToken);
            }));
            parallel.Subscribe(new CallbackObserver<Memory<double>>(_ =>
            {
                secondReceived.TrySetResult();
            }));

            var publishTask = Task.Run(
                () => source.Publish(new double[] { 1, 0 }),
                TestContext.Current.CancellationToken
            );

            await AssertCompletes(firstStarted.Task);
            await AssertCompletes(secondReceived.Task);
            Assert.False(publishTask.IsCompleted);

            firstCanFinish.Set();
            await AssertCompletes(publishTask);
        }

        [Fact]
        public async Task SubscriberReusesWorkerAcrossSamples()
        {
            var source = new TestReaderIqSubject<double>(2);
            using var parallel = new ReaderIqParallelSubject<double>(source);
            var workerThreadIds = new ConcurrentQueue<int>();
            var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var receivedCount = 0;

            parallel.Subscribe(new CallbackObserver<Memory<double>>(_ =>
            {
                workerThreadIds.Enqueue(Environment.CurrentManagedThreadId);
                if (Interlocked.Increment(ref receivedCount) == 2)
                {
                    received.TrySetResult();
                }
            }));

            var publishTask = Task.Run(
                () =>
                {
                    source.Publish(new double[] { 1, 0 });
                    source.Publish(new double[] { 2, 0 });
                },
                TestContext.Current.CancellationToken
            );

            await AssertCompletes(publishTask);
            await AssertCompletes(received.Task);

            var ids = workerThreadIds.ToArray();
            Assert.Equal(2, ids.Length);
            Assert.Equal(ids[0], ids[1]);
        }

        [Fact]
        public async Task ReaderIqZipSubjectPublishesFromParallelSubjectsWhenSecondInputArrivesFirst()
        {
            var firstSource = new TestReaderIqSubject<double>(2);
            var secondSource = new TestReaderIqSubject<double>(2);
            using var firstParallel = new ReaderIqParallelSubject<double>(firstSource);
            using var secondParallel = new ReaderIqParallelSubject<double>(secondSource);
            var secondProcessed = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            using var secondBranch = secondParallel.Select((input, output) =>
            {
                input.CopyTo(output);
                secondProcessed.TrySetResult();
            });
            using var zipped = firstParallel.IqZip<double, double, double>(
                secondBranch,
                (input1, input2, output) =>
                {
                    output[0] = input1[0] + input2[0];
                    output[1] = input1[1] + input2[1];
                },
                2
            );
            var received = new TaskCompletionSource<double[]>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );

            zipped.Subscribe(
                new CallbackObserver<Memory<double>>(memory =>
                {
                    received.TrySetResult(memory.ToArray());
                })
            );

            var secondPublishTask = Task.Run(
                () => secondSource.Publish(new double[] { 10, 20 }),
                TestContext.Current.CancellationToken
            );

            await AssertCompletes(secondProcessed.Task);
            var completedBeforeFirstInput = await Task.WhenAny(
                secondPublishTask,
                Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken)
            );
            Assert.NotSame(secondPublishTask, completedBeforeFirstInput);

            var firstPublishTask = Task.Run(
                () => firstSource.Publish(new double[] { 1, 2 }),
                TestContext.Current.CancellationToken
            );

            var output = await AssertCompletes(received.Task);
            Assert.Equal(11, output[0]);
            Assert.Equal(22, output[1]);

            await AssertCompletes(Task.WhenAll(firstPublishTask, secondPublishTask));
        }

        [Fact]
        public async Task ReactiveParallelJoinPublishesAfterBothSourcesEmit()
        {
            using var first = new Subject<int>();
            using var second = new Subject<int>();
            using var joined = new ReactiveParallelJoin<int, int, int>(
                first,
                second,
                (firstValue, secondValue) => firstValue + secondValue
            );
            var received = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );

            joined.Subscribe(
                new CallbackObserver<int>(value =>
                {
                    received.TrySetResult(value);
                })
            );

            var firstPublishTask = Task.Run(
                () => first.OnNext(10),
                TestContext.Current.CancellationToken
            );
            var secondPublishTask = Task.Run(
                () => second.OnNext(32),
                TestContext.Current.CancellationToken
            );

            var output = await AssertCompletes(received.Task);
            Assert.Equal(42, output);

            await AssertCompletes(Task.WhenAll(firstPublishTask, secondPublishTask));
        }

        private static async Task AssertCompletes(Task task)
        {
            var completedTask = await Task.WhenAny(
                task,
                Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken)
            );
            Assert.Same(task, completedTask);
            await task;
        }

        private static async Task<T> AssertCompletes<T>(Task<T> task)
        {
            var completedTask = await Task.WhenAny(
                task,
                Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken)
            );
            Assert.Same(task, completedTask);
            return await task;
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
