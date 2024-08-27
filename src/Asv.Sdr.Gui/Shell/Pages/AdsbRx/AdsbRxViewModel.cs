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
    private CancellationTokenSource _cancelShStream;
    private LimeSdrDevice _device;
    private AvaPlot _plot1;
    private AvaPlot _plot2;
    private AvaPlot _plot3;
    private AvaPlot _plot4;
    
    private bool _nextTrigger;
    // private readonly AdsbBitDecoder _decoder;
    
    private readonly AdsbMessageParser _decoder;
    private int _cnt;
    


    private byte[] GetMags(byte[] frame)
    {
        var result = new byte[frame.Length * 8 + 16];

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
                result[16 + i * 8 + j] = (byte)((frame[i] & shift) != 0 ? 1 : 0);
                shift >>= 1;
            }
        }

        return result;
    }
    
    public AdsbRxViewModel() : base(WellKnownUri.Shell + ".adsbrx")
    {
        Title = "ADSB RX";
        Icon = MaterialIconKind.ChartFinance;
        // _decoder = new AdsbBitDecoder();

       
        
        _decoder = new AdsbMessageParser();
        // (byte)0x5d, (byte)0x40, (byte)0x74, (byte)0x35, (byte)0x8a, (byte)0xd0, (byte)0x0c

        byte[] buffRx1 = [0xA1, 0x40, 0x8D, 0x40, 0x62, 0x1D, 0x58, 0xC3, 0x82, 0xD6, 0x90, 0xC8, 0xAC, 0x28, 0x63, 0xA7];
        byte[] buffRx2 = [0xA1, 0x40, 0x8D, 0x40, 0x62, 0x1D, 0x58, 0xC3, 0x86, 0x43, 0x5C, 0xC4, 0x12, 0x69, 0x2A, 0xD6];
        var spanRx1 = new ReadOnlySpan<byte>(buffRx1);
        var spanRx2 = new ReadOnlySpan<byte>(buffRx2);
        var df = AdsbHelper.GetDownlinkFormat(spanRx1);
        if (df is 17 or 18)
        {
            var msg1 = new AdsbAirbornePosition(); 
            var msg2 = new AdsbAirbornePosition();
            msg1.Deserialize(ref spanRx1);
            msg2.Deserialize(ref spanRx2);

            // типо msg1 более свежее
            msg1.CalculatePosition(msg2);
            
            var buffTx1 = new byte[buffRx1.Length];
            var buffTx2 = new byte[buffRx2.Length];
            var spanTx1 = new Span<byte>(buffTx1);
            var spanTx2 = new Span<byte>(buffTx2);
            
            msg1.Serialize(ref spanTx1);
            msg2.Serialize(ref spanTx2);

            var eq = true;
            for (var i = 0; i < buffTx1.Length; i++)
            {
                if (buffRx1[i] == buffTx1[i] && buffRx2[i] == buffTx2[i]) continue;
                eq = false;
                break;
            }
        }
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        var mags = GetMags([0x8D, 0x48, 0x40, 0xD6, 0x20, 0x2C, 0xC3, 0x71, 0xC3, 0x2C, 0xE0, 0x57, 0x60, 0x98]);
        foreach (var mag in mags)
        {
            _decoder.ProcessSample(mag);
        }
        
        ConnectLms = ReactiveCommand.CreateRunInBackground(ConnectLmsImpl);
        // _decoder.FrameReceived += (frame, length) =>
        // {
        //     Icao = AdsbBitDecoder.GetICAOAddress(frame).ToString("X") + "   " + length + " " + _cnt++;
        // };
        _decoder.OnMessageRecev
            .Subscribe(_ => Icao = _)
            .DisposeItWith(Disposable);
        NextTrigger = ReactiveCommand.Create(() =>
        {
            _plot2?.Plot.Clear();
            _plot4?.Plot.Clear();
            _nextTrigger = true;
        });
       
        this.WhenAnyValue(x => x.Gain)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Subscribe(x =>
            {
                _device?.SetNormalizedGain(LmsChannel.Rx, 0, x, default).Wait();
            }).DisposeItWith(Disposable);
    }

    [Reactive]
    public string Icao { get; set; }

    private void ConnectLmsImpl()
    {
        LmsNativeDllUsage.Is64BitOperatingSystem = true;
        NativeMethods.Is64BitOperatingSystem = true;
        try
        {
            _cancelShStream = new CancellationTokenSource();
            var dev = LimeSdrDevice.GetAvailableDevices().FirstOrDefault();
            if (dev == null) throw new Exception("LMS device not found");
            _device = new LimeSdrDevice(dev, true);
            var sampleRate = 8_000_000;
            var bitLength = (int)(sampleRate / 2e6);
            var frequencyHz = 1090_000_000;
            var cfg = new LimeSourceIqConfig
            {
                Frequency = frequencyHz,
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
            var lime = new LimeReaderIq(_device,cfg );
            
            var bufferSize = 1024*1024;
            var stopwach = new Stopwatch();
            var fill = new double[bufferSize / 2];
            var stepLine = new double[bufferSize];
            
            var found = false;
            
            

            var proc = new AdsbSignalProcessor(sampleRate, (string name, double[] data, DataType type, PlotNumber num) =>
            {
                _nextTrigger = false;
                var plot = num switch
                {
                    PlotNumber.Plot1 => _plot2,
                    PlotNumber.Plot2 => _plot4,
                    _ => throw new ArgumentOutOfRangeException()
                };
                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    switch (type)
                    {
                        case DataType.Bar:
                            var a = plot.Plot.Add.Scatter(Enumerable.Range(0, data.Length).ToArray(),data);
                            a.LegendText = name;
                            a.ConnectStyle = ConnectStyle.StepHorizontal;
                            break;
                        case DataType.Signal:
                            var b = plot.Plot.Add.Signal(data);
                            b.LegendText = name;
                            break;
                        case DataType.HorizontalLine:
                            plot.Plot.Add.HorizontalLine(data[0]).LegendText = name;
                            break;
                        case DataType.VerticalLine:
                            plot.Plot.Add.VerticalLine(data[0]).LegendText = name;
                            break;
                        case DataType.Clear:
                            plot.Plot.Clear();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(type), type, null);
                    }
                    plot.Plot.Axes.AutoScale();
                    plot.Refresh();
                });

               
                
               
            });

            proc.OnMessage.Subscribe(x =>
            {
                DecodedBits = JsonConvert.SerializeObject(x, Formatting.Indented);
            });
            proc.OnMessageText.Subscribe(x =>
            {
                
            });
            
            var source = lime
                .Sample(sampleRate/10, out var start)
                .Magnitude()
                .Subscribe(data =>
                {
                    for (int i = 0; i < data.Length; i+=2)
                    {
                        proc.Process(data.Span[i]);
                    }
                    /*if (_nextTrigger == false) return;
                    
                    found = false;
                    var firstItem = 0;
                    var tcs = new TaskCompletionSource();
                    var plotData = data.Span;
                    var bitSequence = new double[bufferSize/2];
                    var buffer = data.Span;
                    var corrPlot = new double[buffer.Length / 2];
                    
                    for (int i = 0; i < fill.Length; i++)
                    {
                        var val = plotData[i * 2];
                        proc.Process(val);
                        fill[i] = val;
                        var corr = correlation.Process(buffer[i*2]);
                        corrPlot[i] = corr;
                        if (corr > 0)
                        {
                            stepLine[i*2] = 1;
                            stepLine[i*2+1] = 1;
                            if (found == false)
                            {
                                firstItem = i;
                            }
                            bitSequence[i-firstItem] = 1;
                            found = true;
                        }
                        else
                        {
                            stepLine[i*2] = 0;
                            stepLine[i*2+1] = 0;
                            if (found == true)
                            {
                                bitSequence[i-firstItem] = 0;    
                            }
                        }
                    }
                    
                   
                    
                    if (found == false) return;
                    var str = new StringBuilder();
                    
                    for (int i = 0; i < bitSequence.Length; i+=bitLength)
                    {
                        var cnt = 0;
                        for (int j = 0; j < bitLength; j++)
                        {
                            if (bitSequence[i + j] > 0)
                            {
                                cnt++;
                            }
                        }

                        var value = cnt >= (bitLength / 2);
                        str.Append(value?"1":"0");
                        _decoder.ProcessSample(value ? (byte)1 : (byte)0);
                    }


                    Task.Factory.StartNew(() =>
                    {
                        var level = lime.GetLevel(default).Result.ToString("F0") + " dBm";
                        RxApp.MainThreadScheduler.Schedule(() =>
                        {
                            _plot3.Plot.Add.Annotation(level);
                        });
                        
                    });
                    // DecodedBits = str.ToString();
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        _plot1.Plot.Clear();
                        
                        /*_plot1.Plot.Add.HorizontalLine(Threshold);#1#
                        _plot1.Plot.Add.Signal(fill).LegendText = "RAW signal";
                        _plot1.Plot.Axes.AutoScale();
                        _plot1.Refresh();
                        
                        
                        
                        
                       
                        _plot3.Plot.Clear();
                        _plot3.Plot.Add.Signal(corrPlot).LegendText = "Correlation function";
                        
                        _plot3.Plot.Axes.AutoScale();
                        _plot3.Refresh();    
                        tcs.SetResult();
                    });
                    tcs.Task.Wait();
                    stopwach.Restart();
                    _nextTrigger = false;*/
                });
            start();
        }
        catch (Exception e)
        {
            
        }
    }

    [Reactive]
    public string Level { get; set; }

    [Reactive]
    public string DecodedBits { get; set; }

    public ReactiveCommand<Unit,Unit> ConnectLms { get; set; }
    [Reactive] public double Threshold { get; set; } = 0.3;
    
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

