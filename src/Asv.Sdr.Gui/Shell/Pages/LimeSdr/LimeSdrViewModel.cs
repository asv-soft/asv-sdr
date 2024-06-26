using System;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Asv.Sdr.LimeSdr;
using Asv.Sdr.SignalHound;
using Material.Icons;
using ReactiveUI;
using ScottPlot.Avalonia;

namespace Asv.Sdr.Gui;

[Export(typeof(IShellPage))]
public class LimeSdrViewModel:ShellPage
{
    private ShBbDevice _shDevice;
    private CancellationTokenSource _cancelShStream;
    private AvaPlot _plotSh;
    private AvaPlot _plotLms;
    private LimeSdrDevice _device;

    public LimeSdrViewModel() : base(WellKnownUri.Shell + ".lms")
    {
        Title = "LimeSDR mini";
        Icon = MaterialIconKind.ChartFinance;
        ConnectLms = ReactiveCommand.CreateRunInBackground(ConnectLmsImpl);
        ConnectSh = ReactiveCommand.CreateRunInBackground(ConnectShImpl);
        AutoscaleLms = ReactiveCommand.Create(() =>
        {
            _plotLms?.Plot.Axes.AutoScale();
            _plotLms?.Refresh();
        });
    }

    

    private async void ConnectLmsImpl()
    {
        LmsNativeDllUsage.Is64BitOperatingSystem = true;
        
        try
        {
            _cancelShStream = new CancellationTokenSource();
            var dev = LimeSdrDevice.GetAvailableDevices().FirstOrDefault();
            if (dev == null) throw new Exception("LMS device not found");
            _device = new LimeSdrDevice(dev, true);
            var sampleRate = 40_000_000;
            var frequencyHz = 915_000_000;
            var cfg = new LimeSourceIqConfig
            {
                Frequency = frequencyHz,
                BandWidth = 20e6,
                Gain = 0.69,
                SampleRate = sampleRate,
                GfirEnable = false,
                GfirBandWidth = 20e6,
                LmsLpfEnable = false,
                LmsLpfBandWidth = 19e6,
                Channel = 0,
                AmountDataRssi = 1,
                //LmsSelfCalibrate = true,
                Path = LmsPathRx.LMS_PATH_LNAH,
            };
            var lime = new LimeReaderIq(_device,cfg );
            var bufferSize = 400_000;
            var stopwach = new Stopwatch();
            var count = 10;
            var seq = 0;
            var plotData1 = new double[bufferSize/2];
            var plotData2 = new double[bufferSize/2];
            var fill = plotData1;
            var view = plotData2;
            var source = lime
                .Sample(bufferSize, out var start)
                .Fft1d()
                .Magnitude().Subscribe(data =>
                {
                    if (++seq % count == 0)
                    {
                        /*(view, fill) = (fill, view);
                        for (var i = 0; i < fill.Length; i++)
                        {
                            fill[i] = 0;
                        }*/
                        _plotLms?.Plot.Clear();
                    }

                    var tcs = new TaskCompletionSource();
                    var plotData = data.Span;
                    for (int i = 0; i < fill.Length; i++)
                    {
                        var val = plotData[i * 2];
                        fill[i] = val;
                        /*if (val > fill[i])
                        {
                            
                        }*/
                    }
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        _plotLms?.Plot.Add.Signal(fill);
                        _plotLms?.Refresh();
                        tcs.TrySetResult();
                    });
                    tcs.Task.Wait();
                    Console.WriteLine(stopwach.ElapsedMilliseconds);
                    stopwach.Restart();
                    
                });
            start();
        }
        catch (Exception e)
        {
            
        }
    }
    public ReactiveCommand<Unit,Unit> ConnectLms { get; }
    public ReactiveCommand<Unit,Unit> ConnectSh { get; }
    public ReactiveCommand<Unit,Unit> AutoscaleLms { get; }

    public LimeSdrViewModel InitCharts(AvaPlot plotLms, AvaPlot plotSh)
    {
        _plotLms = plotLms;
        _plotSh = plotSh;
        return this;
    }
    
    private async void ConnectShImpl()
    {
        try
        {
            _cancelShStream = new CancellationTokenSource();
            _shDevice = new ShBbDevice(ShBbDevice.GetAvailableDevices().FirstOrDefault());
            await _shDevice.ConfigureLevel(-20);
            var freq = 915_000_000;
            var span = 27.0e6;
            await _shDevice.ConfigureCenterSpan( freq, span);
            await _shDevice.ConfigureSweepCoupling( 3e3, 3.0e3, 0.001, BBRbwShape.Nuttall, BbRejection.NoSpurReject);
            await _shDevice.ConfigureAcquisition(BbDetector.MinAndMax, BbScale.LogScale);
            await _shDevice.ConfigureRealTime(200.0, 4);
            await _shDevice.Init(BBMode.RealTime, 0, CancellationToken.None);
            var traceInfo = await _shDevice.QueryTraceInfo();
            var realTimeInfo = await _shDevice.QueryRealTimeInfo();

            var sweepMin = new float[traceInfo.TraceLen];
            var sweepMax = new float[traceInfo.TraceLen];
            var frame = new float[realTimeInfo.FrameHeight * realTimeInfo.FrameWidth];
            var alphaFrame = new float[realTimeInfo.FrameHeight * realTimeInfo.FrameWidth];
            
            var readThread = new Thread(() =>
            {
                while (_cancelShStream.IsCancellationRequested == false)
                {
                    _shDevice.FetchRealTimeFrame(sweepMin, sweepMax, frame, alphaFrame).Wait();
                    var tcs = new TaskCompletionSource();
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        _plotSh?.Plot.Clear();
                        
                        _plotSh?.Plot.Add.Signal(sweepMin);
                        _plotSh?.Plot.Add.Signal(sweepMax);
                        _plotSh?.Plot.Axes.AutoScale();
                        /*_plot?.Plot.Add.Heatmap(ConvertTo2DArray(alphaFrame, realTimeInfo.FrameHeight,
                            realTimeInfo.FrameWidth));*/
                        
                        
                        _plotSh?.Refresh();
                        tcs.TrySetResult();
                    });
                    tcs.Task.Wait();
                    Thread.Sleep(100);
                }    
            });
            readThread.Start();
        }
        catch (Exception e)
        {
            
        }
    }
}