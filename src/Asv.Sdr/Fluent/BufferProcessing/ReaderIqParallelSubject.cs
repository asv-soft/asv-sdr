using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asv.Common;

namespace Asv.Sdr
{
    public abstract class ReaderIqZipSubject<TIn1, TIn2, TOut> : ReaderIqSubject<TOut>, IReaderIqSubject<TOut>
    {
        private readonly AutoResetEvent _autoEvent = new(false);
        private int _flag1;
        private int _flag2;
        private Memory<TIn1>? _memory1;
        private Memory<TIn2>? _memory2;

        protected ReaderIqZipSubject(IReaderIqSubject<TIn1> input1, IReaderIqSubject<TIn2> input2, int size,
            bool useArrayPool):base(size, useArrayPool)
        {
            input1.Subscribe(OnData1).DisposeItWith(Disposable);
            input2.Subscribe(OnData2).DisposeItWith(Disposable);
            
        }

        private void OnData1(Memory<TIn1> memory)
        {
            if (Interlocked.CompareExchange(ref _flag1,1,0) !=0) return;
            _memory1 = memory;
            if (_memory2.HasValue == false)
            {
                _autoEvent.WaitOne();
            }
            else
            {
                Process(_memory1.Value.Span, _memory2.Value.Span,Memory.Span);
                Publish();
                _memory1 = null;
                _memory2 = null;
                _autoEvent.Set();
            }
            Interlocked.Exchange(ref _flag1, 0);
        }

        private void OnData2(Memory<TIn2> memory)
        {
            if (Interlocked.CompareExchange(ref _flag2, 1, 0) != 0) return;
            _memory2 = memory;
            if (_memory1.HasValue == false)
            {
                _autoEvent.WaitOne();
            }
            else
            {
                Process(_memory1.Value.Span, _memory2.Value.Span, Memory.Span);
                Publish();
                _memory1 = null;
                _memory2 = null;
                _autoEvent.Set();
            }
            Interlocked.Exchange(ref _flag2, 0);
        }

        protected abstract void Process(ReadOnlySpan<TIn1> input1, ReadOnlySpan<TIn2> input2, Span<TOut> output);

    }

    public delegate void ProcessDelegate<TIn1, TIn2, TOut>(ReadOnlySpan<TIn1> input1, ReadOnlySpan<TIn2> input2, Span<TOut> output);

    public class ReaderIqZipSubjectCallback<TIn1, TIn2, TOut> : ReaderIqZipSubject<TIn1, TIn2, TOut>
    {
        private readonly ProcessDelegate<TIn1, TIn2, TOut> _processCallback;

        public ReaderIqZipSubjectCallback(IReaderIqSubject<TIn1> input1, IReaderIqSubject<TIn2> input2, int outputSize, ProcessDelegate<TIn1, TIn2, TOut> processCallback, bool useArrayPool) : base(input1, input2, outputSize, useArrayPool)
        {
            _processCallback = processCallback ?? throw new ArgumentNullException(nameof(processCallback));
        }

        protected override void Process(ReadOnlySpan<TIn1> input1, ReadOnlySpan<TIn2> input2, Span<TOut> output)
        {
            _processCallback(input1, input2, output);
        }
    }

    public class ReaderIqParallelSubject<T>:DisposableOnceWithCancel, IReaderIqSubject<T>
    {
        private readonly IReaderIqSubject<T> _source;
        private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.NoRecursion);
        private readonly List<SubscriberWorker> _subscribers = new(4);

        public ReaderIqParallelSubject(IReaderIqSubject<T> source)
        {
            _source = source;
            _source.Subscribe(OnSample).DisposeItWith(Disposable);
            source.DisposeItWith(Disposable);
        }

        private void OnSample(Memory<T> memory)
        {
            if (IsDisposed) return;

            SubscriberWorker[] subscribers;
            _rwLock.EnterReadLock();
            try
            {
                subscribers = _subscribers.ToArray();
            }
            finally
            {
                _rwLock.ExitReadLock();
            }

            if (subscribers.Length == 0) return;

            var tasks = new Task[subscribers.Length];
            for (var i = 0; i < subscribers.Length; i++)
            {
                tasks[i] = subscribers[i].Publish(memory, DisposeCancel);
            }

            Task.WaitAll(tasks, DisposeCancel);
        }

        public IDisposable Subscribe(IObserver<Memory<T>> observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            if (IsDisposed) return System.Reactive.Disposables.Disposable.Create(() => { });

            var subscriber = new SubscriberWorker(observer, DisposeCancel);
            var lockTaken = false;
            try
            {
                _rwLock.EnterWriteLock();
                lockTaken = true;
                if (IsDisposed)
                {
                    subscriber.Dispose();
                    return System.Reactive.Disposables.Disposable.Create(() => { });
                }

                _subscribers.Add(subscriber);
            }
            catch (ObjectDisposedException)
            {
                subscriber.Dispose();
                return System.Reactive.Disposables.Disposable.Create(() => { });
            }
            finally
            {
                if (lockTaken)
                {
                    _rwLock.ExitWriteLock();
                }
            }

            return System.Reactive.Disposables.Disposable.Create(() =>
            {
                InternalUnsubscribe(subscriber);
            });
        }

        private void InternalUnsubscribe(SubscriberWorker subscriber)
        {
            var lockTaken = false;
            try
            {
                _rwLock.EnterWriteLock();
                lockTaken = true;
                if (_subscribers.Remove(subscriber))
                {
                    subscriber.Dispose();
                }
            }
            catch (ObjectDisposedException)
            {
                subscriber.Dispose();
            }
            finally
            {
                if (lockTaken)
                {
                    _rwLock.ExitWriteLock();
                }
            }
        }

        public int OutputBufferSize => _source.OutputBufferSize;

        protected override void InternalDisposeOnce()
        {
            base.InternalDisposeOnce();

            SubscriberWorker[] subscribers;
            _rwLock.EnterWriteLock();
            try
            {
                subscribers = _subscribers.ToArray();
                _subscribers.Clear();
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            foreach (var subscriber in subscribers)
            {
                subscriber.Dispose();
            }

            _rwLock.Dispose();
        }

        private sealed class SubscriberWorker : IDisposable
        {
            private readonly CancellationToken _disposeCancel;
            private readonly IObserver<Memory<T>> _observer;
            private readonly BlockingCollection<PublishRequest> _queue = new();
            private int _isDisposed;

            public SubscriberWorker(IObserver<Memory<T>> observer, CancellationToken disposeCancel)
            {
                _observer = observer;
                _disposeCancel = disposeCancel;
                _ = Task.Factory.StartNew(
                    Run,
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default
                );
            }

            public Task Publish(Memory<T> memory, CancellationToken cancellationToken)
            {
                if (Volatile.Read(ref _isDisposed) != 0 || cancellationToken.IsCancellationRequested)
                {
                    return Task.CompletedTask;
                }

                var completion = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
                var request = new PublishRequest(memory, completion);

                try
                {
                    _queue.Add(request, cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    return Task.CompletedTask;
                }
                catch (InvalidOperationException)
                {
                    return Task.CompletedTask;
                }
                catch (OperationCanceledException)
                {
                    return Task.CompletedTask;
                }

                return completion.Task;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;

                try
                {
                    _queue.CompleteAdding();
                }
                catch (ObjectDisposedException)
                {
                    // Owner cancellation may stop the worker and dispose the queue first.
                }
            }

            private void Run()
            {
                try
                {
                    foreach (var request in _queue.GetConsumingEnumerable(_disposeCancel))
                    {
                        Notify(request);
                    }
                }
                catch (OperationCanceledException)
                {
                    CancelPending();
                }
                catch (ObjectDisposedException)
                {
                    CancelPending();
                }
                finally
                {
                    CancelPending();
                    _queue.Dispose();
                }
            }

            private void Notify(PublishRequest request)
            {
                try
                {
                    _observer.OnNext(request.Memory);
                    request.Completion.TrySetResult();
                }
                catch (Exception ex)
                {
                    request.Completion.TrySetException(ex);
                }
            }

            private void CancelPending()
            {
                while (_queue.TryTake(out var request))
                {
                    request.Completion.TrySetCanceled();
                }
            }
        }

        private sealed class PublishRequest
        {
            public PublishRequest(Memory<T> memory, TaskCompletionSource completion)
            {
                Memory = memory;
                Completion = completion;
            }

            public Memory<T> Memory { get; }

            public TaskCompletionSource Completion { get; }
        }
    }
}
