using System;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Asv.Common;
using Asv.Sdr.LimeSdr;
using Material.Icons;
using Newtonsoft.Json;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;

namespace Asv.Sdr.Gui;

[Export(typeof(IShellPage))]
public class AdsbRxViewModel:ShellPage
{
    private LimeSdrDevice _device;
    private AvaPlot _plot1;
    private AvaPlot _plot2;
    private AvaPlot _plot3;
    private AvaPlot _plot4;
    private bool _nextTrigger;
    private CancellationTokenSource _cancelShStream;

    public AdsbRxViewModel() : base(WellKnownUri.Shell + ".adsbrx")
    {
        Title = "ADSB RX";
        Icon = MaterialIconKind.ChartFinance;
        ConnectLms = ReactiveCommand.CreateRunInBackground(ConnectLmsImpl);
        NextTrigger = ReactiveCommand.Create(() =>
        {
            _nextTrigger = true;
        });
       
        this.WhenAnyValue(x => x.Gain)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Subscribe(x =>
            {
                _device?.SetNormalizedGain(LmsChannel.Rx, 0, x, default).Wait();
            }).DisposeItWith(Disposable);
    }
    private void ConnectLmsImpl()
    {
        LmsNativeDllUsage.Is64BitOperatingSystem = true;
        NativeMethods.Is64BitOperatingSystem = true;
        _cancelShStream = new CancellationTokenSource();
        var dev = LimeSdrDevice.GetAvailableDevices().FirstOrDefault();
        if (dev == null) throw new Exception("LMS device not found");
        _device = new LimeSdrDevice(dev, true);
        var sampleRate = 8_000_000;
        var cfg = new LimeSourceIqConfig
        {
            Frequency = 1090_000_000,
            BandWidth = 2.5e6,
            Gain = 0.69,
            SampleRate = sampleRate,
            GfirEnable = true,
            GfirBandWidth = 2.8e6,
            LmsLpfEnable = true,
            LmsLpfBandWidth = 1.4e6,
            Channel = 0,
            AmountDataRssi = 1,
            LmsSelfCalibrate = true,
            Path = LmsPathRx.LMS_PATH_LNAH,
            ThroughputVsLatency = 1,
        };
        var decoder = new AdsbMessageParser();
        decoder.OnMessageRecev.Subscribe(x =>
        {
            Console.WriteLine(x);
        });

        var lime = new LimeReaderIq(_device, cfg)
            .Sample(sampleRate / 100, out var start)
            .Magnitude()
            .AdsbPulseDetector(sampleRate)
            .Preview(PlotRawSignal)
            .AdsbNormalize(sampleRate)
            .Preview(PlotNormalizedSignal)
            .AdsbPulseTruncate(sampleRate)
            .Subscribe(data => decoder.ProcessSample(data));
            
                 
        start();
    }
    private void PlotRawSignal(ReadOnlySpan<double> input)
    {
        var inputArr = input.ToArray();
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            _plot3.Plot.Clear();
            _plot3.Plot.Add.Signal(inputArr);
        });
    }
    private void PlotNormalizedSignal(ReadOnlySpan<double> input)
    {
        var inputArr = input.ToArray();
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            _plot3.Plot.Add.Signal(inputArr);
            _plot3.Refresh();
        });
    }

   


    [Reactive]
    public string DecodedBits { get; set; }
    public ReactiveCommand<Unit,Unit> ConnectLms { get; set; }
   
    [Reactive] public double Gain { get; set; } = 0.69;
    public ReactiveCommand<Unit,Unit> NextTrigger { get; }

    public void InitCharts(AvaPlot plot1, AvaPlot plot2, AvaPlot plot3, AvaPlot plot4)
    {
        _plot1 = plot1;
        _plot2 = plot2;
        _plot3 = plot3;
        _plot4 = plot4;
    }
}

