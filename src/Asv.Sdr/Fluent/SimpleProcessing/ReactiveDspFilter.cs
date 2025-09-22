using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Asv.Common;

namespace Asv.Sdr
{
    public class ReactiveDspFilter : DisposableOnce, IObservable<double>
    {
        private readonly Subject<double> _onData = new();
        private readonly IDisposable _subscribe;

        public ReactiveDspFilter(IObservable<double> src, IDspFilter filter)
        {
            _subscribe = src.Select(filter.Process).Subscribe(_onData);
        }

        protected override void InternalDisposeOnce()
        {
            _onData.OnCompleted();
            _onData.Dispose();
            _subscribe.Dispose();
        }

        public IDisposable Subscribe(IObserver<double> observer)
        {
            return _onData.Subscribe(observer);
        }
    }

    public class ReactiveDsp2ArgsFilter : DisposableOnce, IObservable<(double, double)>
    {
        private readonly Subject<(double, double)> _onData = new();
        private readonly IDisposable _subscribe;

        public ReactiveDsp2ArgsFilter(IObservable<(double, double)> src, IDspFilter filter1, IDspFilter filter2)
        {
            _subscribe = src.Select(_ => (filter1.Process(_.Item1), filter2.Process(_.Item2))).Subscribe(_onData);
        }

        protected override void InternalDisposeOnce()
        {
            _onData.OnCompleted();
            _onData.Dispose();
            _subscribe.Dispose();
        }

        public IDisposable Subscribe(IObserver<(double, double)> observer)
        {
            return _onData.Subscribe(observer);
        }
    }
    
    public class ReactiveDsp3ArgsFilter : DisposableOnce, IObservable<(double, double, double)>
    {
        private readonly Subject<(double, double, double)> _onData = new();
        private readonly IDisposable _subscribe;

        public ReactiveDsp3ArgsFilter(IObservable<(double, double, double)> src, IDspFilter filter1, IDspFilter filter2, IDspFilter filter3)
        {
            _subscribe = src.Select(_ => (filter1.Process(_.Item1), filter2.Process(_.Item2), filter3.Process(_.Item3))).Subscribe(_onData);
        }

        protected override void InternalDisposeOnce()
        {
            _onData.OnCompleted();
            _onData.Dispose();
            _subscribe.Dispose();
        }

        public IDisposable Subscribe(IObserver<(double, double, double)> observer)
        {
            return _onData.Subscribe(observer);
        }
    }
}
