using System;
using System.Buffers;
using System.Reactive.Subjects;
using Asv.Common;
using Asv.Sdr.V2;

namespace Asv.Sdr
{
    public class ReaderIqSubject<TOut> : DisposableOnceWithCancel, IReaderIqSubject<TOut>
    {
        private readonly Subject<Memory<TOut>> _onData = new();
        protected readonly TOut[] Buffer;
        protected readonly Memory<TOut> Memory;

        public ReaderIqSubject(int size, bool useArrayPool)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            OutputBufferSize = size;
            if (useArrayPool)
            {
                Buffer = ArrayPool<TOut>.Shared.Rent(size);
                Disposable.AddAction(() => ArrayPool<TOut>.Shared.Return(Buffer));
            }
            else
            {
                Buffer = new TOut[size];
            }
            Memory = new Memory<TOut>(Buffer, 0, size);
        }

        protected void Publish()
        {
            if (IsDisposed == false)
                _onData.OnNext(Memory);
        }

        public int OutputBufferSize { get; }

        public IDisposable Subscribe(IObserver<Memory<TOut>> observer) => _onData.Subscribe(observer);

        protected override void InternalDisposeOnce()
        {
            base.InternalDisposeOnce();
            _onData.OnCompleted();
            _onData.Dispose();
        }
    }

    public abstract class ReaderIqSubject<TIn, TOut> : ReaderIqSubject<TOut>
    {
        protected ReaderIqSubject(IReaderIqSubject<TIn> input, int outputSize, bool useArrayPool) : base(outputSize, useArrayPool)
        {
            Disposable.Add(input.Subscribe(OnData));
        }

        private void OnData(Memory<TIn> buffer)
        {
            Process(buffer.Span, Memory.Span);
            Publish();
        }

        protected abstract void Process(ReadOnlySpan<TIn> input, Span<TOut> output);
    }

    public delegate void ProcessDelegate<TIn, TOut>(ReadOnlySpan<TIn> input, Span<TOut> output);
    
    public delegate void ProcessDelegate<T>(ReadOnlySpan<T> input);

    public class ReaderIqCallbackSubject<T> : DisposableOnceWithCancel, IReaderIqSubject<T>
    {
        private readonly ProcessDelegate<T> _processCallback;
        private readonly Subject<Memory<T>> _output;

        public ReaderIqCallbackSubject(IReaderIqSubject<T> input, ProcessDelegate<T> processCallback)
        {
            _processCallback = processCallback ?? throw new ArgumentNullException(nameof(processCallback));
            OutputBufferSize = input.OutputBufferSize;
            _output = new Subject<Memory<T>>().DisposeItWith(Disposable);
            input.Subscribe(OnData).DisposeItWith(Disposable);
        }

        private void OnData(Memory<T> memory)
        {
            _processCallback(memory.Span);
            _output.OnNext(memory);
        }

        public IDisposable Subscribe(IObserver<Memory<T>> observer)
        {
            return _output.Subscribe(observer);
        }
        public int OutputBufferSize { get; }
    }
    
    public class ReaderIqCallbackSubject<TIn, TOut> : ReaderIqSubject<TIn, TOut>
    {
        private readonly ProcessDelegate<TIn, TOut> _processCallback;

        public ReaderIqCallbackSubject(IReaderIqSubject<TIn> input, int outputSize, ProcessDelegate<TIn, TOut> processCallback, bool useArrayPool) : base(input, outputSize, useArrayPool)
        {
            _processCallback = processCallback ?? throw new ArgumentNullException(nameof(processCallback));
        }

        protected override void Process(ReadOnlySpan<TIn> input, Span<TOut> output)
        {
            _processCallback(input, output);
        }
    }

    public delegate TOut ProcessValueDelegate<in TIn, out TOut>(TIn input,int index);

    public class ReaderIqSelectIAndQSubject<TIn, TOut> : ReaderIqSubject<TIn, TOut>
    {
        private readonly ProcessValueDelegate<TIn, TOut> _processCallbackI;
        private readonly ProcessValueDelegate<TIn, TOut> _processCallbackQ;
        private readonly int _size;

        public ReaderIqSelectIAndQSubject(IReaderIqSubject<TIn> input, ProcessValueDelegate<TIn,TOut> iCallback, ProcessValueDelegate<TIn, TOut> qCallback, bool useArrayPool) : base(input, input.OutputBufferSize, useArrayPool)
        {
            _processCallbackI = iCallback ?? throw new ArgumentNullException(nameof(iCallback));
            _processCallbackQ = qCallback ?? throw new ArgumentNullException(nameof(qCallback));
            _size = input.OutputBufferSize / 2;
        }

        protected override void Process(ReadOnlySpan<TIn> input, Span<TOut> output)
        {
            for (var i = 0; i < _size; i++)
            {
                output[i * 2] = _processCallbackI(input[i * 2], i);
                output[i * 2 + 1] = _processCallbackQ(input[i * 2 + 1], i);
            }
        }
    }

    public delegate void ProcessIWithQValueDelegate<in TIn, TOut>(TIn inputI, TIn inputQ, int index, out TOut outI, out TOut outQ);

    public class ReaderIqSelectIWithQSubject<TIn, TOut> : ReaderIqSubject<TIn, TOut>
    {
        private readonly ProcessIWithQValueDelegate<TIn, TOut> _processCallback;
        
        private readonly int _size;

        public ReaderIqSelectIWithQSubject(IReaderIqSubject<TIn> input, ProcessIWithQValueDelegate<TIn, TOut> callback, bool useArrayPool) : base(input, input.OutputBufferSize, useArrayPool)
        {
            _processCallback = callback ?? throw new ArgumentNullException(nameof(callback));
            _size = input.OutputBufferSize / 2;
        }

        protected override void Process(ReadOnlySpan<TIn> input, Span<TOut> output)
        {
            for (var i = 0; i < _size; i++)
            {
                _processCallback(input[i * 2], input[i * 2 + 1], i, out var iValue, out var qValue);
                output[i * 2] = iValue;
                output[i * 2 + 1] = qValue;
            }
        }
    }
}