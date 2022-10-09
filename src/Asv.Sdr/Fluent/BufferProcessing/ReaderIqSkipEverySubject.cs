using System;
using System.Reactive.Subjects;
using System.Threading;
using Asv.Common;

namespace Asv.Sdr
{
    public class ReaderIqSkipEverySubject<TOut> :DisposableOnce, IReaderIqSubject<TOut>
    {
        private readonly int _skipEveryTime;
        private readonly Subject<Memory<TOut>> _subject = new();
        private readonly IDisposable _subscribe;
        private int _value;

        public ReaderIqSkipEverySubject(IReaderIqSubject<TOut> source, int skipEveryTime)
        {
            OutputBufferSize = source.OutputBufferSize;
            _skipEveryTime = skipEveryTime;
            _subscribe = source.Subscribe(OnNext);
        }

        private void OnNext(Memory<TOut> value)
        {
            if ((Interlocked.Increment(ref _value) % _skipEveryTime) == 0)
            {
                _subject.OnNext(value);
            }
        }
        
        protected override void InternalDisposeOnce()
        {
            _subscribe.Dispose();
            _subject.OnCompleted();
            _subject.Dispose();
        }

        public IDisposable Subscribe(IObserver<Memory<TOut>> observer)
        {
            return _subject.Subscribe(observer);
        }

        public int OutputBufferSize { get; }
    }
}