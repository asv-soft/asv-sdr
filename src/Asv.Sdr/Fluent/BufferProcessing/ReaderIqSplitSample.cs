using System;
using System.Buffers;
using System.Reactive.Subjects;
using Asv.Common;

namespace Asv.Sdr
{
    public class ReaderIqSplitSample<T> : DisposableOnceWithCancel, IReaderIqSubject<T>
    {
        private readonly Subject<Memory<T>> _onData = new();
        private readonly Memory<T> _memory;
        private int _freeSpace;
        private readonly int _sourceSize;
        public ReaderIqSplitSample(IReaderIqSubject<T> src, int samplesCnt, bool useArrayPool = false)
        {
            T[] buffer;
            if (samplesCnt <= 0) throw new ArgumentOutOfRangeException(nameof(samplesCnt));
            OutputBufferSize = samplesCnt;
            _freeSpace = samplesCnt;
            _sourceSize = src.OutputBufferSize;
            if (useArrayPool)
            {
                buffer = ArrayPool<T>.Shared.Rent(samplesCnt);
                Disposable.AddAction(() => ArrayPool<T>.Shared.Return(buffer));
            }
            else
            {
                buffer = new T[samplesCnt];
            }
            _memory = new Memory<T>(buffer, 0, samplesCnt);
            
            Disposable.Add(src.Subscribe(OnData));
        }
        
        public IDisposable Subscribe(IObserver<Memory<T>> observer) => _onData.Subscribe(observer);

        private void OnData(Memory<T> buffer)
        {
            var input = buffer.Span;
            var inputIndex = 0;
            
            var output = _memory.Span;
            var outputIndex = OutputBufferSize - _freeSpace;
            
            while (inputIndex < _sourceSize)
            {
                if (_freeSpace <= (input.Length - inputIndex))
                {
                    var spanIn = input[new Range(new Index(inputIndex), new Index(inputIndex + _freeSpace))];
                    var spanOut = output[new Range(new Index(outputIndex), new Index(outputIndex + _freeSpace))];
                    inputIndex += _freeSpace;
                    outputIndex = 0;
                    _freeSpace = OutputBufferSize;
                    spanIn.CopyTo(spanOut);
                    Publish();
                }
                else
                {
                    var leftCnt = input.Length - inputIndex;
                    var spanIn = input[inputIndex..];
                    var spanOut = output[new Range(new Index(outputIndex), new Index(outputIndex + leftCnt))];
                    inputIndex += leftCnt;
                    outputIndex += leftCnt;
                    _freeSpace -= leftCnt;
                    spanIn.CopyTo(spanOut);
                }
            }
        }
        
        private void Publish()
        {
            if (IsDisposed == false)
                _onData.OnNext(_memory);
        }
        
        public int OutputBufferSize { get; }
        
        protected override void InternalDisposeOnce()
        {
            base.InternalDisposeOnce();
            _onData.OnCompleted();
            _onData.Dispose();
        }
    }
}