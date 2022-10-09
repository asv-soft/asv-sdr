using System;
using System.Reactive.Subjects;
using Asv.Common;

namespace Asv.Sdr
{
    public class ReaderIqCopyToArray<TOut> : DisposableOnce, IObservable<(TOut[] i, TOut[] q)>
    {
        private readonly IDisposable _subscribe;
        private readonly Subject<(TOut[] i, TOut[] q)> _subject = new();
        private readonly int _size;

        public ReaderIqCopyToArray(IReaderIqSubject<TOut> src)
        {
            _size = src.OutputBufferSize / 2;
            _subscribe = src.Subscribe(OnNext);
        }

        private void OnNext(Memory<TOut> value)
        {
            var dataI = new TOut[_size];
            var dataQ = new TOut[_size];
            var span = value.Span;
            for (var i = 0; i < _size; i++)
            {
                dataI[i] = span[i * 2];
                dataQ[i] = span[i * 2 + 1];
            }
            _subject.OnNext((dataI, dataQ));
        }

        protected override void InternalDisposeOnce()
        {
            _subscribe.Dispose();
        }

        public IDisposable Subscribe(IObserver<(TOut[] i, TOut[] q)> observer)
        {
            return _subject.Subscribe(observer);
        }
    }
}