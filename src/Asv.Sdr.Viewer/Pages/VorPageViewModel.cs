using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asv.Sdr.LimeSdr;
using Asv.Tools;
using DynamicData;
using JetBrains.Annotations;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ScottPlot.Avalonia;
using ScottPlot.Plottable;

namespace Asv.Sdr.Viewer
{
    [Export(typeof(IMainShellPage))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class VorPageViewModel:MainShellPageBase
    {
        private SignalPlot _signal001;
        private int _readSamples;
        private int _bufferSize;
        private (AvaPlot AvaPlot00, AvaPlot AvaPlot01, AvaPlot AvaPlot10, AvaPlot AvaPlot11) _plot;
        private int _sampleRate;
        private SignalPlot _signal002;
        private SignalPlot _signal011;
        private SignalPlot _signal012;
        private int _azimuthIndex;
        private SignalPlot _signal101;
        private SignalPlot _signal102;
        private SignalPlot _signal111;
        private SignalPlot _signal112;
        private int _diffPhaseIndex;
        private int _azimuthIndex2;

        public VorPageViewModel() : base("vor")
        {
            Task.Factory.StartNew(() => Init(), TaskCreationOptions.LongRunning);
        }

        [Reactive]
        public string CodeId { get; set; }

        [Reactive]
        public string Azimuth { get; set; }

        [Reactive]
        public string MeasureTime { get; set; }

        [Reactive]
        public string Am30 { get; set; }

        [Reactive]
        public string Rssi { get; set; }

        [Reactive]
        public bool? IsParallel { get; set; }

        private void Init()
        {
            var dev = LimeSdrDevice.GetAvailableDevices().FirstOrDefault();
            var device = new LimeSdrDevice(dev, true);
            
            _sampleRate = 98_000;
            _readSamples = (int)(_sampleRate / 30 * 1);
            _bufferSize = _readSamples * 2; // I + Q buffer
            var skip = 20;
            var lime = new LimeReaderIq(device, new LimeSourceIqConfig
            {
                Frequency = 110_000_000,
                BandWidth = 32_000,
                Gain = 0.69,
                SampleRate = _sampleRate,
                GfirEnable = true,
                GfirBandWidth = 32_000,
                LmsLpfEnable = true,
                LmsLpfBandWidth = 32_000,
                LmsSelfCalibrate = true,
                Path = LmsPathRx.LMS_PATH_LNAL,
            });
            _signal001 = _plot.AvaPlot00.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
            _signal002 = _plot.AvaPlot00.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);

            _signal011 = _plot.AvaPlot01.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
            _signal012 = _plot.AvaPlot01.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);

            _signal101 = _plot.AvaPlot10.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
            _signal102 = _plot.AvaPlot10.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);

            _signal111 = _plot.AvaPlot11.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
            _signal112 = _plot.AvaPlot11.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);

            var source = lime.Sample(_bufferSize, out var start).Magnitude().Parallel();

            var phase1 = source
                .AddIFilter(new BandpassFilter(_sampleRate, 9960))
                .CopyIToQ()
                .FrequencyShift(_sampleRate, 9960)
                .AddIqFilter(new LowpassFilter(_sampleRate, 1050), new LowpassFilter(_sampleRate, 1050))
                .MagnitudeAndPhase()
                .DiffPhase()
                .MoveQToI()
                .AddIFilter(new LowpassFilter(_sampleRate, 100))
                .Fft1d()
                .GetPhase(_sampleRate, 30);
            var mainFft = source
                .AddIFilter(new LowpassFilter(_sampleRate, 100))
                .Fft1d();

            Observable.Timer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)).Subscribe(_ =>
            {
                Rssi = lime.GetLevel(CancellationToken.None).Result.ToString("F2");
            } );

            var phase2 = mainFft.GetPhase(_sampleRate, 30);

            var am30 = mainFft.GetAm(_sampleRate, 30).AverageFilter(10)
                .Sample(TimeSpan.FromSeconds(1))
                .Subscribe(_=>Am30 = _.ToString("P2"));
            

            var values = phase2
                .Zip(phase1, DspMathEx.GetDistanceAngleRad)
                .AverageRadianFilter(30)
                .TimeInterval();

            var averageTime = values
                .TimeInterval()
                .Select(_ => _.Interval.TotalMilliseconds)
                .AverageFilter(10)
                .Sample(TimeSpan.FromSeconds(1))
                .Subscribe(_=> MeasureTime = TimeSpan.FromMilliseconds(_).ToString("g"));

            values
                .Sample(TimeSpan.FromMilliseconds(100)).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
                {
                    var index = (_azimuthIndex++) % _readSamples;
                    var azimuth = _.Value * 180.0 / Math.PI + 2.62; // 11.5;
                    if (azimuth < 0) azimuth += 360.0;
                    Azimuth = azimuth.ToString("F2");
                    _signal001.Update(index, azimuth);
                    _plot.AvaPlot00.Refresh();
                });


            // phase2
            //     .Zip(phase11, DspMathEx.GetDistanceAngleRad)
            //     .AverageRadianFilter(30)
            //     .Sample(TimeSpan.FromMilliseconds(100)).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
            //     {
            //         var index = (_azimuthIndex2++) % _readSamples;
            //         var azimuth = _ * 180.0 / Math.PI + 2.62; // 11.5;
            //         if (azimuth < 0) azimuth += 360.0;
            //         _signal002.Update(index, azimuth);
            //         _plot.AvaPlot00.Refresh();
            //     });
            //
            // var magSource = source.Magnitude();
            // var filteredSource9960 = magSource.AddIFilter(new BandpassFilter(_sampleRate, 9960)).CopyIToQ();
            // magSource.SkipEvery(skip).Fft1d().CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
            // {
            //     _signal011.Update(data.Item1);
            // });
            // filteredSource9960.SkipEvery(skip).Fft1d().CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
            // {
            //     _signal012.Update(data.Item1);
            //     _plot.AvaPlot01.Refresh();
            // });
            // var freqShift9960 = filteredSource9960.FrequencyShift(_sampleRate, 9960);
            // freqShift9960.SkipEvery(skip).Fft1d().CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
            // {
            //     _signal101.Update(data.Item1);
            //     
            // });
            // var freqShiftFiltered = freqShift9960.AddIqFilter(new LowpassFilter(_sampleRate, 1050),
            //     new LowpassFilter(_sampleRate, 1050));
            // freqShiftFiltered.SkipEvery(skip).Fft1d().CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
            // {
            //     _signal102.Update(data.Item1);
            //     _plot.AvaPlot10.Refresh();
            // });
            //
            // var phi1 = freqShiftFiltered
            //     .MagnitudeAndPhase()
            //     .DiffPhase()
            //     .MoveQToI()
            //     .AddIFilter(new LowpassFilter(_sampleRate, 100))
            //     .Fft1d()
            //     .GetPhase(_sampleRate, 30);
            //
            // // var am9960 = magPhi9960.Select((iValue, i) => iValue, (qValue, i) => 0)
            // //     .Fft1d()
            // //     .GetAm(_readSamples, 0);
            //     
            // var phi2 = source
            //     .AddIqFilter(new LowpassFilter(_sampleRate, 100), new LowpassFilter(_sampleRate, 100))
            //     .Magnitude()
            //     .Fft1d()
            //     .GetPhase(_sampleRate, 30);
            //
            // phi1.Zip(phi2).Sample(TimeSpan.FromMilliseconds(300)).ObserveOn(RxApp.MainThreadScheduler).Subscribe(v =>
            // {
            //     var index = (_diffPhaseIndex++) % _readSamples;
            //     _signal111.Update(index, v.First);
            //     _signal112.Update(index, v.Second);
            //     _plot.AvaPlot11.Refresh();
            // });
            //
            // phi2.Zip(phi1, DspMathEx.GetDistanceAngleRad).Select(_ =>
            // {
            //     var azimuthDeg = _ ;
            //     
            //     return azimuthDeg;
            // }).AverageRadianFilter(10).Sample(TimeSpan.FromMilliseconds(100)).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
            // {
            //     var index = (_azimuthIndex++) % _readSamples;
            //     var azimuth = _ * 180.0 / Math.PI + 11.5;
            //     if (azimuth < 0) azimuth += 360.0;
            //     Azimuth = azimuth.ToString("F2");
            //     _signal001.Update(index, azimuth);
            //     _plot.AvaPlot00.Refresh();
            // });



            // var amCodeId = source.Magnitude().Fft1d().GetAm(_sampleRate, 1020);
            //
            // amCodeId.ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
            // {
            //     var index = (_signal1Index++) % _readSamples;
            //     _signal001.Update(index, data);
            //     _plot.AvaPlot00.Refresh();
            // });
            // amCodeId.CodeId(0.05, 0.20, _readSamples, _sampleRate).Subscribe(_ => CodeId = _);




            start();


            // source.Magnitude().SkipEvery(skip).CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
            // {
            //     _signal001.Update(data.Item1);
            // });
            // var filtered = source.AddIqFilter(new LowpassFilter(_sampleRate, 1050), new LowpassFilter(_sampleRate, 1050));
            // filtered.Magnitude().SkipEvery(skip).CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
            // {
            //     _signal002.Update(data.Item1);
            //     _plot.AvaPlot00.Refresh();
            // });
            // var shifted = filtered.FrequencyShift(_sampleRate, 1020);
            // filtered.Magnitude().SkipEvery(skip).CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
            // {
            //     _signal011.Update(data.Item1);
            //     _plot.AvaPlot10.Refresh();
            // });
            // var shiftedFiltered = shifted.AddIqFilter(new LowpassFilter(_sampleRate,150), new LowpassFilter(_sampleRate, 150));
            // shiftedFiltered.Magnitude().SkipEvery(skip).CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
            // {
            //     _signal012.Update(data.Item1);
            //     _plot.AvaPlot01.Refresh();
            // });



        }



        public void InitGraph((AvaPlot AvaPlot00, AvaPlot AvaPlot01, AvaPlot AvaPlot10, AvaPlot AvaPlot11) plot)
        {
            _plot = plot;
        }
    }
    
}
