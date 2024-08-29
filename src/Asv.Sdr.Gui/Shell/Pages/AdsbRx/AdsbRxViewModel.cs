using System;
using System.Composition;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using Asv.Common;
using Asv.IO;
using Asv.Sdr.LimeSdr;
using Material.Icons;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ScottPlot.Avalonia;

namespace Asv.Sdr.Gui;

[Export(typeof(IShellPage))]
public class AdsbRxViewModel:ShellPage
{
    private LimeSdrDevice _device;
    private AvaPlot _plot1;
    private AvaPlot _plot2;
    private AvaPlot _plot3;
    private AvaPlot _plot4;
    private AvaPlot _plot5;
    private AvaPlot _plot6;
    private CancellationTokenSource _cancelShStream;
    private ILmsStream _txStream;
    private ScottDebugTriggerPlot _iDebug;
    private ScottDebugTriggerPlot _qDebug;
    private ScottDebugTriggerPlot _magDebug;
    private ScottDebugPlot _dataDebug;
    private ScottDebugPlot _avgDebug;
    private ScottDebugPlot _corrDebug;
    private bool _isPause;



    public AdsbRxViewModel() : base(WellKnownUri.Shell + ".adsbrx")
    {
        Title = "ADSB RX";
        Icon = MaterialIconKind.ChartFinance;
        ConnectLms = ReactiveCommand.CreateRunInBackground(ConnectLmsImpl);
        StartTxLms = ReactiveCommand.CreateRunInBackground(StartTxLmsImpl);
        NextTrigger = ReactiveCommand.Create(() =>
        {
            _isPause = !_isPause;
            if (_corrDebug!= null) _corrDebug.IsPlotEnabled = _isPause;
            if (_avgDebug!= null) _avgDebug.IsPlotEnabled = _isPause;
            if (_iDebug!= null) _iDebug.IsPlotEnabled = _isPause;
            if (_qDebug!= null) _qDebug.IsPlotEnabled = _isPause;
            if (_magDebug!= null) _magDebug.IsPlotEnabled = _isPause;
            if (_dataDebug!= null) _dataDebug.IsPlotEnabled = _isPause;
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
            BandWidth = 3e6,
            Gain = 0.0,
            SampleRate = sampleRate,
            GfirEnable = false,
            GfirBandWidth = 2.8e6,
            LmsLpfEnable = true,
            LmsLpfBandWidth = 3e6,
            Channel = 0,
            AmountDataRssi = 1,
            LmsSelfCalibrate = true,
            Path = LmsPathRx.LMS_PATH_LNAH,
            ThroughputVsLatency = 1,
        };
        var decoder = new AdsbMessageParser();
        decoder.Register(()=>new AdsbAirbornePosition());
        //decoder.Register(()=>new AdsbAircraftIdentification());
        /*decoder.Register(()=>new AdsbSurfacePosition());*/
        var counter = 0;
        var err = 0;
        var lastOdd = null as AdsbAirbornePosition;
        var lastEven = null as AdsbAirbornePosition;
        decoder.OnMessage.Subscribe(x =>
        {
            var curr = (AdsbAirbornePosition)x;
            if (curr.CprFormat == CprFormatEnum.Even)
            {
                lastEven = curr;
            }

            if (curr.CprFormat == CprFormatEnum.Odd)
            {
                lastOdd = curr;
            }
            if (lastEven != null && lastOdd!= null)
            {
                lastEven.CalculatePosition(lastOdd);
                Console.WriteLine($"{DateTime.Now:O} {counter} => {lastEven.Latitude} {lastEven.Longitude} {lastEven.Altitude}");
            }
        });
        decoder.OnMessageRecev.Subscribe(x =>
        {
            Console.WriteLine($"{DateTime.Now:O} {counter++} => {x}");
            
        });
        decoder.OnError.Subscribe(x =>
        {
            Console.WriteLine($"{DateTime.Now:O} {err++} => {x}");
        });

        _corrDebug = new ScottDebugPlot(_plot4);
        _avgDebug = new ScottDebugPlot(_plot5);
        _dataDebug = new ScottDebugPlot(_plot6);
        /*_iDebug = new ScottDebugTriggerPlot(_plot1,_corrDebug.OnTrigger);
        _qDebug = new ScottDebugTriggerPlot(_plot2,_corrDebug.OnTrigger);*/
        _magDebug = new ScottDebugTriggerPlot(_plot3,_corrDebug.OnTrigger);
        var show = false;
        var lime = new LimeReaderIq(_device, cfg)
            .Sample(sampleRate / 5, out var start)
            //.PreviewPlotI("I",_iDebug)
            //.PreviewPlotQ("Q",_qDebug)
            .Magnitude()
            .PreviewPlotI("Magnitude",_magDebug)
            .AdsbPulseDetector(sampleRate,_corrDebug)
            .AdsbNormalize(sampleRate,_avgDebug)
            .AdsbPulseTruncate(sampleRate, _dataDebug )
            .Subscribe(data =>
            {
                foreach (var d in data.Span)
                {
                    decoder.ProcessSample(d);
                }
                decoder.Reset();
            });
            
                 
        start();
    }

    [Reactive]
    public string DecodedBits { get; set; }
    public ReactiveCommand<Unit,Unit> ConnectLms { get; set; }
   
    [Reactive] public double Gain { get; set; } = 0.0;
    public ReactiveCommand<Unit,Unit> NextTrigger { get; }
    public ReactiveCommand<Unit,Unit> StartTxLms { get; }

    public void InitCharts(AvaPlot plot1, AvaPlot plot2, AvaPlot plot3, AvaPlot plot4,AvaPlot plot5,AvaPlot plot6)
    {
        _plot1 = plot1;
        _plot2 = plot2;
        _plot3 = plot3;
        _plot4 = plot4;
        _plot5 = plot5;
        _plot6 = plot6;
    }
}

