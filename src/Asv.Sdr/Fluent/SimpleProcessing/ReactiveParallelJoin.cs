using System;
using System.Diagnostics;
using System.Reactive.Subjects;
using System.Threading;
using Asv.Common;

namespace Asv.Sdr
{
    public class ReactiveParallelJoin<TFirst, TSecond, TResult>
        : DisposableOnce,
            IObservable<TResult>
    {
        private readonly IDisposable _firstSubscribe;
        private readonly IDisposable _secondSubscribe;
        private readonly Subject<TResult?> _result = new();
        private TSecond _second;
        private TFirst _first;
        private Barrier _barrier;

        public ReactiveParallelJoin(
            IObservable<TFirst> first,
            IObservable<TSecond> second,
            Func<TFirst?, TSecond?, TResult?> resultSelector
        )
        {
            ArgumentNullException.ThrowIfNull(first);
            ArgumentNullException.ThrowIfNull(second);

            var resultSelector1 =
                resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));

            _barrier = new Barrier(
                2,
                _ =>
                {
                    Debug.WriteLine("RESULT");
                    _result.OnNext(resultSelector1(_first, _second));
                }
            );

            _firstSubscribe = first.Subscribe(OnFirst);
            _secondSubscribe = second.Subscribe(OnSecond);
        }

        private void OnFirst(TFirst first)
        {
            if (IsDisposed)
            {
                return;
            }

            _first = first;
            Debug.WriteLine("FIRST");
            _barrier.SignalAndWait();
        }

        private void OnSecond(TSecond second)
        {
            if (IsDisposed)
            {
                return;
            }

            _second = second;
            Debug.WriteLine("SECOND");
            _barrier.SignalAndWait();
        }

        protected override void InternalDisposeOnce()
        {
            _firstSubscribe.Dispose();
            _secondSubscribe.Dispose();
            _result.Dispose();
            _barrier.Dispose();
        }

        public IDisposable Subscribe(IObserver<TResult> observer)
        {
            return _result.Subscribe(observer);
        }
    }
}
