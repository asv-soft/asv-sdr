using System;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Asv.Common;
using Asv.Sdr.LimeSdr;
using Material.Icons;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;

namespace Asv.Sdr.Gui;

[Export(typeof(IShellPage))]
public class AdsbRxViewModel:ShellPage
{
    private CancellationTokenSource _cancelShStream;
    private LimeSdrDevice _device;
    private AvaPlot _plotLeft;
    private AvaPlot _plotRight;
    private Signal _signal;
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


        byte[] buffRx = [0xA1, 0x40, 0x8D, 0x48, 0x40, 0xD6, 0x20, 0x2C, 0xC3, 0x71, 0xC3, 0x2C, 0xE0, 0x57, 0x60, 0x98];
        var spanRx = new ReadOnlySpan<byte>(buffRx);
        var df = AdsbHelper.GetDownlinkFormat(spanRx);
        if (df is 17 or 18)
        {
            var id = new AdsbAircraftIdentification();
            id.Deserialize(ref spanRx);

            var buffTx = new byte[buffRx.Length];
            var spanTx = new Span<byte>(buffTx);
            id.Serialize(ref spanTx);

            var eq = true;
            for (var i = 0; i < buffRx.Length; i++)
            {
                if (buffTx[i] == buffRx[i]) continue;
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
            _nextTrigger = true;
        });
        this.WhenAnyValue(x => x.Threshold).Subscribe(x =>
        {
            _plotLeft?.Plot.Remove<HorizontalLine>();
            _plotLeft?.Plot.Add.HorizontalLine(x);
            _plotLeft?.Refresh();
        });
    }

    [Reactive]
    public string Icao { get; set; }

    private void ConnectLmsImpl()
    {
        LmsNativeDllUsage.Is64BitOperatingSystem = true;
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
                BandWidth = 4e6,
                Gain = 0.69,
                SampleRate = sampleRate,
                GfirEnable = true,
                GfirBandWidth = 4e6,
                LmsLpfEnable = true,
                LmsLpfBandWidth = 4e6,
                Channel = 0,
                AmountDataRssi = 1,
                //LmsSelfCalibrate = true,
                Path = LmsPathRx.LMS_PATH_LNAH,
            };
            var lime = new LimeReaderIq(_device,cfg );
            var bufferSize = 4_000;
            var stopwach = new Stopwatch();
            var fill = new double[bufferSize / 2];
            var stepLine = new double[bufferSize];
            
            var found = false;
            
            
            var source = lime
                .Sample(bufferSize, out var start)
                .Magnitude().Subscribe(data =>
                {
                    if (_nextTrigger == false) return;
                    found = false;
                    var firstItem = 0;
                    var tcs = new TaskCompletionSource();
                    var plotData = data.Span;
                    var bitSequence = new double[bufferSize/2];
                    for (int i = 0; i < fill.Length; i++)
                    {
                        var val = plotData[i * 2];
                        fill[i] = val;
                        if (val > Threshold)
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

                    // DecodedBits = str.ToString();
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        _plotLeft.Plot.Clear();
                        _plotRight.Plot.Clear();
                        _plotLeft.Plot.Add.HorizontalLine(Threshold);
                        _plotLeft.Plot.Add.Signal(fill);
                        _plotRight.Plot.Add.Signal(stepLine);
                        _plotLeft.Refresh();
                        _plotRight.Refresh();
                        tcs.SetResult();
                    });
                    tcs.Task.Wait();
                    stopwach.Restart();
                    _nextTrigger = false;
                });
            start();
        }
        catch (Exception e)
        {
            
        }
    }

    [Reactive]
    public string DecodedBits { get; set; }

    public ReactiveCommand<Unit,Unit> ConnectLms { get; set; }
    [Reactive] public double Threshold { get; set; } = 0.3;
    public ReactiveCommand<Unit,Unit> NextTrigger { get; }

    public void InitCharts(AvaPlot plotLeft, AvaPlot plotRight)
    {
        _plotLeft = plotLeft;
        _plotRight = plotRight;
    }
}