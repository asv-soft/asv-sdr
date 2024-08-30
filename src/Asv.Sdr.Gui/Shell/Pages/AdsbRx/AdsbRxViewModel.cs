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
    private AvaPlot _plot5;
    private AvaPlot _plot6;
    
    private readonly AdsbMessageParser _decoder;
    private int _cnt;
    
    private CancellationTokenSource _cancelShStream;
    private ILmsStream _txStream;
    private ScottDebugTriggerPlot _iDebug;
    private ScottDebugTriggerPlot _qDebug;
    private ScottDebugTriggerPlot _magDebug;
    private ScottDebugPlot _dataDebug;
    private ScottDebugPlot _avgDebug;
    private ScottDebugPlot _corrDebug;
    private bool _isPause;

    private byte[] GetMags(byte[] frame)
    {
        var result = new byte[frame.Length * 8 * 2 + 16];

        byte shift = 0x80; 
        for (var i = 0; i < 8; i++)
        {
            result[i] = (byte)((AdsbHelper.Preamble[0] & shift) != 0 ? 1 : 0);
            result[i + 8] = (byte)((AdsbHelper.Preamble[1] & shift) != 0 ? 1 : 0);
            shift >>= 1;
        }
        
        for (var i = 0; i < frame.Length; i++)
        {
            shift = 0x80;
            for (var j = 0; j < 8; j++)
            {
                var mag = frame[i] & shift;
                if (mag != 0)
                {
                    result[16 + i * 16 + j * 2] = 1;
                    result[16 + i * 16 + j * 2 + 1] = 0;
                }
                else
                {
                    result[16 + i * 16 + j * 2] = 0;
                    result[16 + i * 16 + j * 2 + 1] = 1;
                }
                
                shift >>= 1;
            }
        }

        return result;
    }
    
    public AdsbRxViewModel() : base(WellKnownUri.Shell + ".adsbrx")
    {
        Title = "ADSB RX";
        Icon = MaterialIconKind.ChartFinance;
     
        
        _decoder = new AdsbMessageParser();
        _decoder.RegisterDefaultMessages();


        var bufferRx = new List<byte[]>();

        bufferRx.Add(GetMags([0x8D, 0x40, 0x62, 0x1D, 0x58, 0xC3, 0x82, 0xD6, 0x90, 0xC8, 0xAC, 0x28, 0x63, 0xA7]));
        bufferRx.Add(GetMags([0x8D, 0x40, 0x62, 0x1D, 0x58, 0xC3, 0x86, 0x43, 0x5C, 0xC4, 0x12, 0x69, 0x2A, 0xD6]));
        bufferRx.Add(GetMags([0x8D, 0x48, 0x40, 0xD6, 0x20, 0x2C, 0xC3, 0x71, 0xC3, 0x2C, 0xE0, 0x57, 0x60, 0x98]));
        bufferRx.Add(GetMags([0x8C, 0x48, 0x41, 0x75, 0x3A, 0xAB, 0x23, 0x87, 0x33, 0xC8, 0xCD, 0x40, 0x20, 0xB1]));
        bufferRx.Add(GetMags([0x8C, 0x48, 0x41, 0x75, 0x3A, 0x8A, 0x35, 0x32, 0x3F, 0xAE, 0xBD, 0xAC, 0x70, 0x2D]));
        bufferRx.Add(GetMags([0x8D, 0x48, 0x50, 0x20, 0x99, 0x44, 0x09, 0x94, 0x08, 0x38, 0x17, 0x5B, 0x28, 0x4F]));
        bufferRx.Add(GetMags([0x8D, 0xA0, 0x5F, 0x21, 0x9B, 0x06, 0xB6, 0xAF, 0x18, 0x94, 0x00, 0xCB, 0xC3, 0x3F]));
        
        
        var lastAirbornePositions = new List<AdsbAirbornePosition>();
        _decoder.Filter<AdsbAirbornePosition>().Subscribe(_ =>
        {
            var last = lastAirbornePositions.FirstOrDefault(p => p.AircraftAddress == _.AircraftAddress);
            if (last == null)
            {
                lastAirbornePositions.Add(_);
                return;
            }

            if (last.CprFormat != _.CprFormat)
            {
                _.CalculatePosition(last);
                Console.WriteLine($"AirbornePosition Latitude: {_.Latitude:F6} Longitude: {_.Longitude:F6}");
            }
            lastAirbornePositions.Remove(last);
            lastAirbornePositions.Add(_);
            
        }).DisposeItWith(Disposable);
        
        var lastSurfacePositions = new List<AdsbSurfacePosition>();
        _decoder.Filter<AdsbSurfacePosition>().Subscribe(_ =>
        {
            var last = lastSurfacePositions.FirstOrDefault(p => p.AircraftAddress == _.AircraftAddress);
            if (last == null)
            {
                lastSurfacePositions.Add(_);
                return;
            }

            if (last.CprFormat != _.CprFormat)
            {
                _.CalculatePosition(last);
                Console.WriteLine($"SurfacePosition Latitude: {_.Latitude:F6} Longitude: {_.Longitude:F6}");
            }
            lastSurfacePositions.Remove(last);
            lastSurfacePositions.Add(_);
            
        }).DisposeItWith(Disposable);

        _decoder.Filter<AdsbAircraftIdentification>().Subscribe(_ =>
        {
            Console.WriteLine($"ID ICAO: {_.AircraftAddress} Identification: {_.AircraftIdentification}");
        }).DisposeItWith(Disposable);

        _decoder.Filter<AdsbGroundSpeed>().Subscribe(_ =>
        {

        }).DisposeItWith(Disposable);
        
        _decoder.Filter<AdsbAirspeed>().Subscribe(_ =>
        {

        }).DisposeItWith(Disposable);
        
        
        foreach (var buffer in bufferRx)
        {
            foreach (var b in buffer)
            {
                _decoder.ProcessSample(b);
            }
        }

        
        ConnectLms = ReactiveCommand.CreateRunInBackground(ConnectLmsImpl);
        StartTxLms = ReactiveCommand.CreateRunInBackground(StartTxLmsImpl);
        StartStopTx = ReactiveCommand.CreateRunInBackground(StartStopTxImpl);
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

    private void StartStopTxImpl()
    {
        EanbleTx = !EanbleTx;
    }

    private async void StartTxLmsImpl()
    {
        var _sampleRate = 8e6;
        var _bandWidth = 8e6;
        var _freq = 1090_000_000;
        var _gain = 0.69;
        if (_device == null)
        {
            var dev = LimeSdrDevice.GetAvailableDevices().FirstOrDefault();
            if (dev == null) throw new Exception("LMS device not found");
            _device = new LimeSdrDevice(dev, true);    
        }
        
        NativeMethods.Is64BitOperatingSystem = true;
        LmsNativeDllUsage.Is64BitOperatingSystem = true;
        
        await _device.EnableChannel(LmsChannel.Tx, 0, true,default);
        await _device.SetSampleRate(_sampleRate, 1U,default);
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
            new Thread(() =>
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

                var zero = new ReadOnlyMemory<float>(new float[(int)_sampleRate / 100]); 
                while (true)
                {
                    _txStream.Write(zero, 10_000, default).Wait();
                    _txStream.Write(bufferMemory, 10_000, default).Wait();
                    _txStream.Write(zero, 10_000, default).Wait();
                    
                }
                
                
            }).Start();
           
        
    }

    public bool EanbleTx { get; set; }

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
        //decoder.Register(()=>new AdsbAirbornePosition());
        decoder.Register(()=>new AdsbAircraftIdentification());
        /*decoder.Register(()=>new AdsbSurfacePosition());*/
        var counter = 0;
        var err = 0;
        var lastOdd = null as AdsbAirbornePosition;
        var lastEven = null as AdsbAirbornePosition;
        /*decoder.OnMessage.Subscribe(x =>
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
        });*/
        decoder.OnMessage.Subscribe(x =>
        {
            Console.WriteLine($"{DateTime.Now:O} {counter} => {JsonConvert.SerializeObject(x)}");
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
        _iDebug = new ScottDebugTriggerPlot(_plot1,_corrDebug.OnTrigger);
        _qDebug = new ScottDebugTriggerPlot(_plot2,_corrDebug.OnTrigger);
        _magDebug = new ScottDebugTriggerPlot(_plot3,_corrDebug.OnTrigger);
        var show = false;
        var lime = new LimeReaderIq(_device, cfg)
            .Sample(sampleRate / 5, out var start)
            .PreviewPlotI("I",_iDebug)
            .PreviewPlotQ("Q",_qDebug)
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
    public ReactiveCommand<Unit,Unit> StartStopTx { get; }

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

