using System;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Asv.Sdr.LimeSdr;
using Material.Icons;
using ReactiveUI;
using ScottPlot.Avalonia;

namespace Asv.Sdr.Gui;

[Export(typeof(IShellPage))]
public class AdsbTxViewModel:ShellPage
{
    private AvaPlot _avaPlotLms;
    private AvaPlot _avaPlotSh;
    private CancellationTokenSource _cancelShStream;
    private LimeSdrDevice _device;

    public AdsbTxViewModel() : base(WellKnownUri.Shell + ".adsbtx")
    {
        Title = "ADSB Tx";
        Icon = MaterialIconKind.ChartFinance;
        ConnectLms = ReactiveCommand.CreateRunInBackground(ConnectLmsImpl);
    }

    private void ConnectLmsImpl()
    {
        LmsNativeDllUsage.Is64BitOperatingSystem = true;
        
        try
        {
            _cancelShStream = new CancellationTokenSource();
            var dev = LimeSdrDevice.GetAvailableDevices().FirstOrDefault();
            if (dev == null) throw new Exception("LMS device not found");
            _device = new LimeSdrDevice(dev, true);
            _device.CreateStream()
                
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

    public ReactiveCommand<Unit,Unit> ConnectLms { get; set; }

    public void InitCharts(AvaPlot avaPlotLms, AvaPlot avaPlotSh)
    {
        _avaPlotLms = avaPlotLms;
        _avaPlotSh = avaPlotSh;
    }
}