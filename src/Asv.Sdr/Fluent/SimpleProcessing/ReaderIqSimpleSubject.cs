using System;
using System.Reactive.Subjects;
using Asv.Common;

namespace Asv.Sdr
{
    public abstract class ReaderIqSimpleSubject<TIn, TOut> : DisposableOnce, IObservable<TOut>
    {
        private readonly Subject<TOut> _onData = new();
        private readonly IDisposable _subscribe;

        protected ReaderIqSimpleSubject(IReaderIqSubject<TIn> input)
        {
            _subscribe = input.Subscribe(OnData);
        }

        private void OnData(Memory<TIn> buffer)
        {
            var result = Process(buffer.Span, out var selfPublish);
            if (selfPublish == false) Publish(result);
        }


        protected override void InternalDisposeOnce()
        {
            _subscribe.Dispose();
            _onData.OnCompleted();
            _onData.Dispose();
        }

        protected void Publish(TOut value)
        {
            _onData.OnNext(value);
        }

        public IDisposable Subscribe(IObserver<TOut> observer)
        {
            return _onData.Subscribe(observer);
        }

        protected abstract TOut Process(ReadOnlySpan<TIn> input, out bool selfPublish);
    }
}