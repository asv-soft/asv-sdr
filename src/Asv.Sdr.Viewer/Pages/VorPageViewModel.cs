using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asv.Common;
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
        private int _readSamples;
        private int _bufferSize;
        private int _sampleRate;
        
        private int _azimuthIndex;
        private int _azimuthIndex2;
        private int _diffPhaseIndex;
        private bool _clear;
        
        private string _rssi;
        
        private AvaPlot _plot00;
        private SignalPlot _signal001;
        private SignalPlot _signal002;
        private SignalPlot _signal003;
        
        private AvaPlot _plot01;
        private SignalPlot _signal011;
        private SignalPlot _signal012;
        
        private AvaPlot _plot10;
        private SignalPlot _signal101;
        private SignalPlot _signal102;
        
        private AvaPlot _plot11;
        private SignalPlot _signal111;
        private SignalPlot _signal112;
        
        private string _codeId;
        private int _phaseBuffSize;

        public VorPageViewModel() : base("vor")
        {
            Title = "ILS";
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
            _sampleRate = 96_000;
            _readSamples = (int)(_sampleRate / 30.0 * 5);
            _bufferSize = _readSamples * 2; // I + Q buffer
            
            
            var lime = new LimeReaderIq(device, new LimeSourceIqConfig
            {
                Frequency = 110_000_000-16_000,
                BandWidth = 64_000,
                Gain = 1,
                SampleRate = _sampleRate,
                GfirEnable = true,
                GfirBandWidth = 64_000,
                LmsLpfEnable = true,
                LmsLpfBandWidth = 64_000,
                LmsSelfCalibrate = true,
                Channel = 0,
                Path = LmsPathRx.LMS_PATH_LNAH,
            });
            var size = 300;
            _signal001 = _plot00.Plot.AddSignal(new double[size]);
            _signal002 = _plot00.Plot.AddSignal(new double[size]);
            _signal003 = _plot00.Plot.AddSignal(new double[size]);
            
            var factor = _sampleRate / _readSamples;
            _phaseBuffSize = (int)Math.Round(150.0 / factor) * 4;
            
            _signal011 = _plot01.Plot.AddSignal(new double[_phaseBuffSize]);
            _signal012 = _plot01.Plot.AddSignal(new double[_phaseBuffSize]);
            
            _signal101 = _plot10.Plot.AddSignal(new double[_phaseBuffSize]);
            _signal102 = _plot10.Plot.AddSignal(new double[_phaseBuffSize]);
            
            _signal111 = _plot11.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
            _signal112 = _plot11.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);

            var source = lime.Sample(_bufferSize, out var start).Parallel();

            
            
            source
                .Magnitude()
                .Fft1d()
                .GetAm(_sampleRate, 90, 150)
                .AverageFilter(10, 10)
                .RollingBuffer(TimeSpan.FromSeconds(10))
                .Subscribe(_ =>
                {
                    var ddm = _.Select(x=>x.Item2 - x.Item1).Average();
                    var sdm = _.Select(x=>x.Item2 + x.Item1).Average();
                    var ddmDev = Math.Sqrt(_.Select(x=>x.Item2 - x.Item1).Select(x=>(x-ddm)*(x-ddm)).Sum() / (_.Length -1));
                    var sdmDev = Math.Sqrt(_.Select(x=>x.Item2 + x.Item1).Select(x=>(x-sdm)*(x-sdm)).Sum() / (_.Length -1));
                    var ddmAbsErr = Math.Abs(_.Select(x => x.Item2 - x.Item1).Min() - _.Select(x => x.Item2 - x.Item1).Max())/2.0;
                    var sdmAbsErr = Math.Abs(_.Select(x => x.Item2 + x.Item1).Min() - _.Select(x => x.Item2 + x.Item1).Max())/2.0;
                    for (var index = 0; index < _.Length; index++)
                    {
                        var item = _[index];
                        _signal001.Update(index, (item.Item2 - item.Item1)*100);
                    }
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        CustomText = $"DDM[{_.Length}]: {ddm:P2} (dev:{ddmDev:P2}, err:{ddmAbsErr:P2}) \n" +
                                     $"SDM: {sdm:P2} (dev:{sdmDev:P2}, err:{sdmAbsErr:P2})  \nP:{_rssi}";
                                     _plot00.Refresh();
                    });
                });
            Observable.Timer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)).Subscribe(_ =>
            {
                _rssi = lime.GetLevel(CancellationToken.None).Result.ToString("F2");
            });


            var lowPassFilter = source
                //.AddIqFilter(new LowpassFilter(_sampleRate,16000),new LowpassFilter(_sampleRate,16000))
                //.AddIqFilter(new CustomLowPassElliptic8kHzFilter(),new CustomLowPassElliptic8kHzFilter())
                .AddIqFilter(new IlsLowPass(), new IlsLowPass()).Parallel();
            lowPassFilter
                .Magnitude()
                // .HalfOverlap()
                //.WindowFilter(WindowFilterEnum.Cosine)
                // .AddIqFilter(new LowpassFilter(_sampleRate, 1050),new LowpassFilter(_sampleRate, 1050))
                .Fft1d()
                .GetAm(_sampleRate, 90, 150)
                //.KalmanFilter(0.0003, 0.1,0.0003,0.1)
                .AverageFilter(10, 10)
                .RollingBuffer(TimeSpan.FromSeconds(10))
                .Subscribe(_ =>
                {
                    var ddm = _.Select(x=>x.Item2 - x.Item1).Average();
                    var sdm = _.Select(x=>x.Item2 + x.Item1).Average();
                    var ddmDev = Math.Sqrt(_.Select(x=>x.Item2 - x.Item1).Select(x=>(x-ddm)*(x-ddm)).Sum() / (_.Length -1));
                    var sdmDev = Math.Sqrt(_.Select(x=>x.Item2 + x.Item1).Select(x=>(x-sdm)*(x-sdm)).Sum() / (_.Length -1));
                    var ddmAbsErr = Math.Abs(_.Select(x => x.Item2 - x.Item1).Min() - _.Select(x => x.Item2 - x.Item1).Max())/2.0;
                    var sdmAbsErr = Math.Abs(_.Select(x => x.Item2 + x.Item1).Min() - _.Select(x => x.Item2 + x.Item1).Max())/2.0;
                    for (var index = 0; index < _.Length; index++)
                    {
                        var item = _[index];
                        _signal002.Update(index, (item.Item2 - item.Item1)*100);
                    }
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        CustomText2 = $"DDM[{_.Length}]: {ddm:P2} (dev:{ddmDev:P2}, err:{ddmAbsErr:P2}) \n" +
                                      $"SDM: {sdm:P2} (dev:{sdmDev:P2}, err:{sdmAbsErr:P2})";
                        //_plot.Refresh();
                    });
                });

            var highPassFilter = source
                .AddIqFilter(new IlsHighPass(), new IlsHighPass()).Parallel(); 
            
            highPassFilter
                .Magnitude()
                .Fft1d()
                .GetAm(_sampleRate, 90, 150)
                .AverageFilter(10, 10)
                .RollingBuffer(TimeSpan.FromSeconds(10))
                .Subscribe(_ =>
                {
                    var ddm = _.Select(x=>x.Item2 - x.Item1).Average();
                    var sdm = _.Select(x=>x.Item2 + x.Item1).Average();
                    var ddmDev = Math.Sqrt(_.Select(x=>x.Item2 - x.Item1).Select(x=>(x-ddm)*(x-ddm)).Sum() / (_.Length -1));
                    var sdmDev = Math.Sqrt(_.Select(x=>x.Item2 + x.Item1).Select(x=>(x-sdm)*(x-sdm)).Sum() / (_.Length -1));
                    var ddmAbsErr = Math.Abs(_.Select(x => x.Item2 - x.Item1).Min() - _.Select(x => x.Item2 - x.Item1).Max())/2.0;
                    var sdmAbsErr = Math.Abs(_.Select(x => x.Item2 + x.Item1).Min() - _.Select(x => x.Item2 + x.Item1).Max())/2.0;
                    for (var index = 0; index < _.Length; index++)
                    {
                        var item = _[index];
                        _signal003.Update(index, (item.Item2 - item.Item1)*100);
                    }
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        CustomText3 = $"DDM[{_.Length}]: {ddm:P2} (dev:{ddmDev:P2}, err:{ddmAbsErr:P2}) \n" +
                                      $"SDM: {sdm:P2} (dev:{sdmDev:P2}, err:{sdmAbsErr:P2}) \n" +
                                      $"CodeID: {_codeId}";
                        //_plot.Refresh();
                    });
                });
            
            
            
            
            var readSamplesStep = (int)(_sampleRate / 30.0); // 90 Hz and 150 Hz => НОД 30  -  samples per 1/30 sec
            var sampleRateCount = _sampleRate * (30.0 / 1000.0); // 30 ms
            var rate = (int)Math.Floor(sampleRateCount / readSamplesStep) + 1;
            var samplesCnt = rate * readSamplesStep * 2; // I + Q
            
            source
                .SplitSample(samplesCnt)
                .Magnitude()
                .Fft1d()
                .GetAm(_sampleRate, 1020)
                .CodeId(0.05, 0.20, rate * readSamplesStep, _sampleRate)
                .Subscribe(_ =>
                {
                    _codeId = _;
                });

            source
                .FrequencyOffset(16_000)
                .RollingBuffer(TimeSpan.FromSeconds(10))
                .Subscribe(_ =>
            {
                var freqOffset = _.Average();
                var freqOffsetDev = Math.Sqrt(_.Select(x=>(x-freqOffset)*(x-freqOffset)).Sum() / (_.Length - 1));
                var freqOffsetAbsErr = Math.Abs(_.Min() - _.Max()) / 2.0;
                
                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    CustomText4 = $"FreqOffset: {freqOffset:F2} (dev:{freqOffsetDev:F2}, err:{freqOffsetAbsErr:F2})";
                    //_plot.Refresh();
                });
            });
            lowPassFilter
                .FrequencyOffset(16_000)
                .RollingBuffer(TimeSpan.FromSeconds(10))
                .Subscribe(_ =>
                {
                    var freqOffset = _.Average();
                    var freqOffsetDev = Math.Sqrt(_.Select(x=>(x-freqOffset)*(x-freqOffset)).Sum() / (_.Length - 1));
                    var freqOffsetAbsErr = Math.Abs(_.Min() - _.Max()) / 2.0;
                
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        CustomText5 = $"FreqOffset CRS: {freqOffset:F2} (dev:{freqOffsetDev:F2}, err:{freqOffsetAbsErr:F2})";
                        //_plot.Refresh();
                    });
                });
            
            highPassFilter
                .FrequencyOffset(16_000)
                .RollingBuffer(TimeSpan.FromSeconds(10))
                .Subscribe(_ =>
                {
                    var freqOffset = _.Average();
                    var freqOffsetDev = Math.Sqrt(_.Select(x=>(x-freqOffset)*(x-freqOffset)).Sum() / (_.Length - 1));
                    var freqOffsetAbsErr = Math.Abs(_.Min() - _.Max()) / 2.0;
                
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        CustomText6 = $"FreqOffset CLR: {freqOffset:F2} (dev:{freqOffsetDev:F2}, err:{freqOffsetAbsErr:F2})";
                    });
                });

            var clrPhases = lowPassFilter
                .Magnitude()
                .Fft1d()
                .GetPhase(_sampleRate, 90, 150);
            var crsPhases = highPassFilter
                .Magnitude()
                .Fft1d()
                .GetPhase(_sampleRate, 90, 150);
            

            clrPhases
                .ParallelJoin(crsPhases, (clr,crs) => (MathEx.GetDistanceAngleRad(clr.Item1, crs.Item1),
                MathEx.GetDistanceAngleRad(clr.Item2, crs.Item2)))
                .RollingBuffer(TimeSpan.FromSeconds(10))
                .Subscribe(_ =>
                {
                    var diff90 = _.Select(__ => __.Item1).ToArray();
                    var diff150 = _.Select(__ => __.Item2).ToArray();
                    var phi90CrsVsClr = diff90.Average();
                    var phi90CrsVsClrDev =
                        Math.Sqrt(diff90.Select(x => (x - phi90CrsVsClr) * (x - phi90CrsVsClr)).Sum() / (diff90.Length - 1));
                    var phi90CrsVsClrErr = Math.Abs(diff90.Min() - diff90.Max()) / 2.0;
                    
                    var phi150CrsVsClr = diff150.Average();
                    var phi150CrsVsClrDev =
                        Math.Sqrt(diff150.Select(x => (x - phi150CrsVsClr) * (x - phi150CrsVsClr)).Sum() / (diff150.Length - 1));
                    var phi150CrsVsClrErr = Math.Abs(diff150.Min() - diff150.Max()) / 2.0;
                    
                
                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    CustomText7 =
                        $"Phi90CrsVsClr: {phi90CrsVsClr*180/Math.PI:F2} (dev:{phi90CrsVsClrDev*180/Math.PI:F2}, err:{phi90CrsVsClrErr*180/Math.PI:F2}) | Phi150CrsVsClr: {phi150CrsVsClr*180/Math.PI:F2} (dev:{phi150CrsVsClrDev*180/Math.PI:F2}, err:{phi150CrsVsClrErr*180/Math.PI:F2})";
                    //_plot.Refresh();
                });
            });
                
            
            //
            // _azimuthIndex = 0;
            //     source
            //         // .HalfOverlap()
            //         // .WindowFilter(WindowFilterEnum.Cosine)
            //         .AddIqFilter(new LowpassFilter(_sampleRate, 1050),new LowpassFilter(_sampleRate, 1050))
            //         .Magnitude()
            //         .HalfOverlap()
            //         .WindowFilter(WindowFilterEnum.Cosine)
            //         .Fft1d()
            //         .GetAm(_sampleRate, 90, 150)
            //         .AverageFilter(20,20)
            //         .Subscribe(_=>
            //         {
            //             
            //             if (_clear)
            //             {
            //                 _clear = false;
            //                 _azimuthIndex = 0;
            //                 ddmList.Clear();
            //             }
            //             var ddm = _.Item2 - _.Item1;
            //             var sdm = _.Item2 + _.Item1;
            //             _signal001.Update(_azimuthIndex++,ddm*100);
            //             ddmList.Add(ddm);
            //             var ddmAvg = ddmList.Average();
            //             var err = Math.Sqrt(ddmList.Select(__ => (__-ddmAvg)*(__-ddmAvg) ).Sum()/ddmList.Count);
            //             var minmax = Math.Abs(ddmList.Min() - ddmList.Max());
            //             Azimuth = $"DDM:{ddm:P1} (avg:{ddmAvg:P2} err:{err:P2} min-max:{minmax:P2}) SDM:{sdm:P1}";
            //             RxApp.MainThreadScheduler.Schedule(() =>
            //             {
            //                 _plot.AvaPlot00.Refresh();
            //             });
            //         });
                // source
                //     .Fft1d()
                //     .Subscribe(_ =>
                //     {
                //         for (int i = 0; i < _.Length/2; i++)
                //         {
                //             _signal001.Update(i,_.Span[i*2]);
                //             _signal002.Update(i,_.Span[i*2 + 1]);
                //         }
                //
                //         RxApp.MainThreadScheduler.Schedule(() =>
                //         {
                //             _plot.AvaPlot00.Refresh();
                //         });
                //         
                //     });
            

            start();

        }
        
        // private void Init()
        // {
        //     var dev = LimeSdrDevice.GetAvailableDevices().FirstOrDefault();
        //     var device = new LimeSdrDevice(dev, true);
        //     
        //     _sampleRate = 48_000;
        //     _readSamples = (int)(_sampleRate / 30 * 3);
        //     _bufferSize = _readSamples * 2; // I + Q buffer
        //     var skip = 20;
        //     var lime = new LimeReaderIq(device, new LimeSourceIqConfig
        //     {
        //         Frequency = 113_000_000,
        //         BandWidth = 32_000,
        //         Gain = 1,
        //         SampleRate = _sampleRate,
        //         GfirEnable = true,
        //         GfirBandWidth = 32_000,
        //         LmsLpfEnable = true,
        //         LmsLpfBandWidth = 32_000,
        //         LmsSelfCalibrate = true,
        //         Channel = 0,
        //         Path = LmsPathRx.LMS_PATH_LNAH,
        //         
        //
        //     });
        //     _signal001 = _plot.AvaPlot00.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
        //     _signal002 = _plot.AvaPlot00.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
        //
        //     _signal011 = _plot.AvaPlot01.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
        //     _signal012 = _plot.AvaPlot01.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
        //
        //     _signal101 = _plot.AvaPlot10.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
        //     _signal102 = _plot.AvaPlot10.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
        //
        //     _signal111 = _plot.AvaPlot11.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
        //     _signal112 = _plot.AvaPlot11.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
        //
        //     var source = lime.Sample(_bufferSize, out var start)
        //         .Magnitude()
        //         //.HalfOverlap()
        //         //.WindowFilter(WindowFilterEnum.Cosine)
        //         .Parallel();
        //     var phase1Tick = 0;
        //     var phase2Tick = 0;
        //     var diffTIck = 0;
        //
        //     var phase1 = source
        //         .AddIFilter(new BandpassFilter(_sampleRate, 9960))
        //         .CopyIToQ()
        //         .FrequencyShift(_sampleRate, 9960)
        //         .AddIqFilter(new LowpassFilter(_sampleRate, 1050), new LowpassFilter(_sampleRate, 1050))
        //         .MagnitudeAndPhase()
        //         .DiffPhase()
        //         .MoveQToI()
        //         .AddIFilter(new LowpassFilter(_sampleRate, 100))
        //         .Fft1d()
        //         .GetPhase(_sampleRate, 30);
        //     var mainFft = source
        //         .AddIFilter(new LowpassFilter(_sampleRate, 100))
        //         .Fft1d();
        //
        //     Observable.Timer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)).Subscribe(_ =>
        //     {
        //         Rssi = lime.GetLevel(CancellationToken.None).Result.ToString("F2");
        //     } );
        //
        //     var phase2 = mainFft
        //         .GetPhase(_sampleRate, 30);
        //
        //     var am30 = mainFft.GetAm(_sampleRate, 30)
        //         .AverageFilter(10)
        //         .Sample(TimeSpan.FromSeconds(1))
        //         .Subscribe(_=>Am30 = _.ToString("P2"));
        //     
        //
        //     var values = phase2
        //         .ParallelJoin(phase1, DspMathEx.GetDistanceAngleRad)
        //         //.KalmanRadianFilter(0.03,1)
        //         .AverageRadianFilter(10)
        //         .TimeInterval().Publish().RefCount();
        //
        //     var averageTime = values
        //         .TimeInterval()
        //         .Select(_ => _.Interval.TotalMilliseconds)
        //         .AverageFilter(10)
        //         .Sample(TimeSpan.FromSeconds(1))
        //         .Subscribe(_=> MeasureTime = TimeSpan.FromMilliseconds(_).ToString("g"));
        //     var angle = new List<double>();
        //     values
        //         .ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
        //         {
        //             if (_clear)
        //             {
        //                 _clear = false;
        //                 angle.Clear();
        //                 _azimuthIndex = 0;
        //             }
        //             var index = (_azimuthIndex++) % _readSamples;
        //             
        //             var azimuth = _.Value * 180.0 / Math.PI + 2.62; // 11.5;
        //             if (azimuth < 0) azimuth += 360.0;
        //             angle.Add(azimuth);
        //             var avg = DspMathEx.GetAvgAngleDeg(angle);
        //             var err = Math.Sqrt(angle.Select(__ =>
        //                                     DspMathEx.GetDistanceAngleDeg(__, avg) *
        //                                     DspMathEx.GetDistanceAngleDeg(__, avg)).Sum() /
        //                                 angle.Count);
        //             Azimuth = $"{azimuth:F1} (avg:{avg:F1}, dev:{err:F1}, err:{Math.Abs(angle.Min() - angle.Max()):F1})";
        //             _signal001.Update(index, azimuth);
        //             _plot.AvaPlot00.Refresh();
        //         });
        //
        //
        //     // phase2
        //     //     .Zip(phase11, DspMathEx.GetDistanceAngleRad)
        //     //     .AverageRadianFilter(30)
        //     //     .Sample(TimeSpan.FromMilliseconds(100)).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
        //     //     {
        //     //         var index = (_azimuthIndex2++) % _readSamples;
        //     //         var azimuth = _ * 180.0 / Math.PI + 2.62; // 11.5;
        //     //         if (azimuth < 0) azimuth += 360.0;
        //     //         _signal002.Update(index, azimuth);
        //     //         _plot.AvaPlot00.Refresh();
        //     //     });
        //     //
        //     // var magSource = source.Magnitude();
        //     // var filteredSource9960 = magSource.AddIFilter(new BandpassFilter(_sampleRate, 9960)).CopyIToQ();
        //     // magSource.SkipEvery(skip).Fft1d().CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
        //     // {
        //     //     _signal011.Update(data.Item1);
        //     // });
        //     // filteredSource9960.SkipEvery(skip).Fft1d().CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
        //     // {
        //     //     _signal012.Update(data.Item1);
        //     //     _plot.AvaPlot01.Refresh();
        //     // });
        //     // var freqShift9960 = filteredSource9960.FrequencyShift(_sampleRate, 9960);
        //     // freqShift9960.SkipEvery(skip).Fft1d().CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
        //     // {
        //     //     _signal101.Update(data.Item1);
        //     //     
        //     // });
        //     // var freqShiftFiltered = freqShift9960.AddIqFilter(new LowpassFilter(_sampleRate, 1050),
        //     //     new LowpassFilter(_sampleRate, 1050));
        //     // freqShiftFiltered.SkipEvery(skip).Fft1d().CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
        //     // {
        //     //     _signal102.Update(data.Item1);
        //     //     _plot.AvaPlot10.Refresh();
        //     // });
        //     //
        //     // var phi1 = freqShiftFiltered
        //     //     .MagnitudeAndPhase()
        //     //     .DiffPhase()
        //     //     .MoveQToI()
        //     //     .AddIFilter(new LowpassFilter(_sampleRate, 100))
        //     //     .Fft1d()
        //     //     .GetPhase(_sampleRate, 30);
        //     //
        //     // // var am9960 = magPhi9960.Select((iValue, i) => iValue, (qValue, i) => 0)
        //     // //     .Fft1d()
        //     // //     .GetAm(_readSamples, 0);
        //     //     
        //     // var phi2 = source
        //     //     .AddIqFilter(new LowpassFilter(_sampleRate, 100), new LowpassFilter(_sampleRate, 100))
        //     //     .Magnitude()
        //     //     .Fft1d()
        //     //     .GetPhase(_sampleRate, 30);
        //     //
        //     // phi1.Zip(phi2).Sample(TimeSpan.FromMilliseconds(300)).ObserveOn(RxApp.MainThreadScheduler).Subscribe(v =>
        //     // {
        //     //     var index = (_diffPhaseIndex++) % _readSamples;
        //     //     _signal111.Update(index, v.First);
        //     //     _signal112.Update(index, v.Second);
        //     //     _plot.AvaPlot11.Refresh();
        //     // });
        //     //
        //     // phi2.Zip(phi1, DspMathEx.GetDistanceAngleRad).Select(_ =>
        //     // {
        //     //     var azimuthDeg = _ ;
        //     //     
        //     //     return azimuthDeg;
        //     // }).AverageRadianFilter(10).Sample(TimeSpan.FromMilliseconds(100)).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
        //     // {
        //     //     var index = (_azimuthIndex++) % _readSamples;
        //     //     var azimuth = _ * 180.0 / Math.PI + 11.5;
        //     //     if (azimuth < 0) azimuth += 360.0;
        //     //     Azimuth = azimuth.ToString("F2");
        //     //     _signal001.Update(index, azimuth);
        //     //     _plot.AvaPlot00.Refresh();
        //     // });
        //
        //
        //
        //      var amCodeId = source.Fft1d().GetAm(_sampleRate, 1020);
        //     //
        //     // amCodeId.ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
        //     // {
        //     //     var index = (_signal1Index++) % _readSamples;
        //     //     _signal001.Update(index, data);
        //     //     _plot.AvaPlot00.Refresh();
        //     // });
        //      amCodeId.CodeId(0.05, 0.20, _readSamples, _sampleRate).Subscribe(_ => CodeId = _);
        //
        //
        //
        //
        //     start();
        //
        //
        //     // source.Magnitude().SkipEvery(skip).CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
        //     // {
        //     //     _signal001.Update(data.Item1);
        //     // });
        //     // var filtered = source.AddIqFilter(new LowpassFilter(_sampleRate, 1050), new LowpassFilter(_sampleRate, 1050));
        //     // filtered.Magnitude().SkipEvery(skip).CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
        //     // {
        //     //     _signal002.Update(data.Item1);
        //     //     _plot.AvaPlot00.Refresh();
        //     // });
        //     // var shifted = filtered.FrequencyShift(_sampleRate, 1020);
        //     // filtered.Magnitude().SkipEvery(skip).CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
        //     // {
        //     //     _signal011.Update(data.Item1);
        //     //     _plot.AvaPlot10.Refresh();
        //     // });
        //     // var shiftedFiltered = shifted.AddIqFilter(new LowpassFilter(_sampleRate,150), new LowpassFilter(_sampleRate, 150));
        //     // shiftedFiltered.Magnitude().SkipEvery(skip).CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
        //     // {
        //     //     _signal012.Update(data.Item1);
        //     //     _plot.AvaPlot01.Refresh();
        //     // });
        //
        //
        //
        // }
        
        [Reactive]
        public string CustomText7 { get; set; }
        [Reactive]
        public string CustomText6 { get; set; }
        [Reactive]
        public string CustomText5 { get; set; }
        [Reactive]
        public string CustomText4 { get; set; }
        [Reactive]
        public string CustomText3 { get; set; }
        [Reactive]
        public string CustomText2 { get; set; }
        [Reactive]
        public string CustomText { get; set; }
        
        public void ClearCommand()
        {
            _clear = true;
        }

        public void InitGraph(AvaPlot plot00, AvaPlot plot01, AvaPlot plot10, AvaPlot plot11)
        {
            _plot00 = plot00;
            _plot01 = plot01;
            _plot10 = plot10;
            _plot11 = plot11;
        }
    }
    
}
