using System;
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
        private readonly List<IObserver<Memory<T>>> _subscribers = new(4);

        public ReaderIqParallelSubject(IReaderIqSubject<T> source)
        {
            _source = source;
            _source.Subscribe(OnSample).DisposeItWith(Disposable);
            _rwLock.DisposeItWith(Disposable);
            source.DisposeItWith(Disposable);
        }

        private void OnSample(Memory<T> memory)
        {
            _rwLock.EnterReadLock();
            try
            {
                var parallelTasks = new Task[_subscribers.Count];
                for (var i = 0; i < parallelTasks.Length; i++)
                {
                    var i1 = i;
                    parallelTasks[i] = Task.Factory.StartNew(() =>_subscribers[i1].OnNext(memory),DisposeCancel, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                }
                Task.WaitAll(parallelTasks, DisposeCancel);
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        public IDisposable Subscribe(IObserver<Memory<T>> observer)
        {
            _rwLock.EnterWriteLock();
            try
            {
                _subscribers.Add(observer);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
            return System.Reactive.Disposables.Disposable.Create(() =>
            {
                InternalUnsubscribe(observer);
            });
        }

        private void InternalUnsubscribe(IObserver<Memory<T>> observer)
        {
            _rwLock.EnterWriteLock();
            try
            {
                _subscribers.Remove(observer);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        public int OutputBufferSize => _source.OutputBufferSize;
    }
}