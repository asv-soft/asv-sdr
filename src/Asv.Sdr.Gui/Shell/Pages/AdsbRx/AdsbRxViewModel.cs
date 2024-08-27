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
using Asv.IO;
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
    private ILmsStream _txStream;

    public AdsbRxViewModel() : base(WellKnownUri.Shell + ".adsbrx")
    {
        Title = "ADSB RX";
        Icon = MaterialIconKind.ChartFinance;
        ConnectLms = ReactiveCommand.CreateRunInBackground(ConnectLmsImpl);
        StartTxLms = ReactiveCommand.CreateRunInBackground(StartTxLmsImpl);
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

    private async void StartTxLmsImpl()
    {
        var _sampleRate = 8e6;
        var _bandWidth = 4e6;
        var _freq = 1090_000_000;
        var _gain = 0.5;
        await _device.EnableChannel(LmsChannel.Tx, 0, true,default);
        await _device.SetSampleRate(_sampleRate, 0U,default);
        await _device.SetAntenna(LmsChannel.Tx, 0, (uint) LmsPathTx.LMS_PATH_TX1 , default);
        await _device.SetBandWidth(LmsChannel.Tx, 0, _bandWidth, default);
            
        await _device.SetFrequency(LmsChannel.Tx, 0, _freq, default);
        await _device.SetNormalizedGain(LmsChannel.Tx, 0, _gain, default);
        var preamble = new double[] { 1, 0, 1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0 };
            var message = new byte[]
            {
                0x8D,
                0x48,
                0x40,
                0xD6,
                0x20,
                0x2C,
                0xC3,
                0x71,
                0xC3,
                0x2C,
                0xE0,
                0x57,
                0x60,
                0x98,
            };
            var bit = new double[message.Length * 8 * 2 + preamble.Length];
            for (var i = 0; i < preamble.Length; i++)
            {
                bit[i] = preamble[i];
            }
            for (uint i = 0; i < message.Length*8; i++)
            {
                if (BitHelper.GetBitU(message, i, 1) > 0)
                {
                    bit[i * 2 + preamble.Length] = 1;
                    bit[i * 2 + preamble.Length + 1] = 0;
                }
                else
                {
                    bit[i * 2 + preamble.Length] = 0;
                    bit[i * 2 + preamble.Length + 1] = 1;
                }
            }
            
            _txStream = await _device.CreateStream(LmsChannel.Tx, 0, (uint)_sampleRate, throughputVsLatency:1, cancel: default).DisposeItWith(Disposable);
            await _txStream.Start(default);
            Observable.Timer(TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(0.1))
                .Subscribe(x =>
                {
                    var bitLength = (int)(_sampleRate / 2e6);
                    var bufferSize = bit.Length * bitLength*2;
                    var buffer = new float[bufferSize];
                    var bufferMemory = new ReadOnlyMemory<float>(buffer, 0, bufferSize);
                    
                    for (var i = 0; i < bit.Length; i++)
                    {
                        for (int j = 0; j < bitLength; j++)
                        {
                            buffer[i*2 * bitLength + j] = (float)bit[i];
                            buffer[i*2 * bitLength + j + 1] = (float)bit[i];
                        }
                    }
                    _txStream.Write(bufferMemory, 10_000, default).Wait();
                });
        
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
            ThroughputVsLatency = 0,
        };
        var decoder = new AdsbMessageParser();
        decoder.OnMessageRecev.Subscribe(x =>
        {
            Console.WriteLine(x);
        });

        var lime = new LimeReaderIq(_device, cfg)
            .Sample(sampleRate / 10, out var start)
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
    public ReactiveCommand<Unit,Unit> StartTxLms { get; }

    public void InitCharts(AvaPlot plot1, AvaPlot plot2, AvaPlot plot3, AvaPlot plot4)
    {
        _plot1 = plot1;
        _plot2 = plot2;
        _plot3 = plot3;
        _plot4 = plot4;
    }
}

