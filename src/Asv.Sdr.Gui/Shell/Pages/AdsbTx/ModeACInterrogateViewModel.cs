using System;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asv.Cfg;
using Asv.Common;
using Asv.Sdr.LimeSdr;
using Material.Icons;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ScottPlot.Avalonia;
using ZLogger;

namespace Asv.Sdr.Gui;



public class ModeACInterrogateConfig 
{
    public ulong TxFrequency { get; set; } = 1030000000;
    public ulong RxFrequency { get; set; } = 1090000000;
    public ulong FrequencyOffset { get; set; } = 35000;
    public ulong SampleRate { get; set; } = 40000000;
    public LmsPathTx LmsPathTx { get; set; } = LmsPathTx.LMS_PATH_TX2;
    public LmsPathRx LmsPathRx { get; set; } = LmsPathRx.LMS_PATH_LNAW;

    public double Amplitude { get; set; } = 1.0;
    
    public int TxGain { get; set; } = -30;
    public int RxGain { get; set; } = -30;
    
    public string SerialNumber { get; set; } = string.Empty;
    public double DtCal { get; set; } = 0.0;
}

[Export(typeof(IShellPage))]
public class ModeACInterrogateViewModel : ShellPage
{
    private AvaPlot _avaPlotLms;
    private AvaPlot _avaPlotSh;
    private CancellationTokenSource? _cancelStream;
    private ILimeSdrDevice? _device;
    private ILmsStream _txStream;
    private ILmsStream _rxStream;
    private readonly ModeACInterrogateConfig _cfg;
    private readonly ILogger<ModeACInterrogateViewModel> _logger;
    private ScottDebugPlot _dataDebug;
    private ScottDebugTriggerPlot _magDebug;
    private bool _flag = false;
    private int _p2 = 0;
    private int _msgIdx = 0;
    private int _magnPlotCounter;
    private int _rxTimeStampCnt;
    private int _txTimeStampCnt;
    private ulong _rxNowSamples;


    private void GetMessage(byte[] buffer)
    {
        for (var i = 0; i < buffer.Length; i++)
        {
            Console.Write($@"0x{buffer[i]:X2} ");
        }
    }
    public ModeACInterrogateViewModel() : base(WellKnownUri.Shell + ".modeACInter")
    {
        Title = "Mode A/C Interrogate";
        Icon = MaterialIconKind.ChartFinance;

        ConnectLms = ReactiveCommand.CreateRunInBackground(ConnectLmsImpl);
        DisconnectLms = ReactiveCommand.CreateRunInBackground(DisconnectLmsImpl);
    }
    
    [ImportingConstructor]
    public ModeACInterrogateViewModel(IConfiguration cfg, ILoggerFactory loggerFactory) : this()
    {
        _logger = loggerFactory.CreateLogger<ModeACInterrogateViewModel>();
        _cfg = cfg.Get<ModeACInterrogateConfig>();
        TxGain = _cfg.TxGain;
        RxGain = _cfg.RxGain;
        Disposable.AddAction(() =>
        {
            _cancelStream?.Cancel(false);
            _device?.Dispose();
        });
        
        this.WhenAnyValue(x => x.TxGain)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Subscribe(x =>
            {
                var gain = (uint)(x + 69);
                _device?.SetNormalizedGainDbm(LmsChannel.Tx, 0, gain, CancellationToken.None).Wait();
                _cfg.TxGain = x;
                cfg.Set(_cfg);

            }).DisposeItWith(Disposable);
        this.WhenAnyValue(x => x.RxGain)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Subscribe(x =>
            {
                var gain = (uint)(x + 69);
                _device?.SetNormalizedGainDbm(LmsChannel.Rx, 0, gain, CancellationToken.None).Wait();
                _cfg.RxGain = x;
                cfg.Set(_cfg);

            }).DisposeItWith(Disposable);
        this.WhenAnyValue(x => x.OnOffP2)
            .ObserveOn(Scheduler.Default)
            .Subscribe(isOn =>
            {
                _p2 = isOn ? 1 : 0;
            }).DisposeItWith(Disposable);
    }
    
    private ILimeSdrDevice? CreateDevice()
    {
        
        _device?.Dispose();
        
        var dev = !string.IsNullOrWhiteSpace(_cfg.SerialNumber)
            ? LimeSdrDevice.GetAvailableDevices().FirstOrDefault(id => id.Contains(_cfg.SerialNumber))
            : LimeSdrDevice.GetAvailableDevices().FirstOrDefault();
        if (dev == null) throw new Exception("LMS device not found");
            
        _logger.ZLogInformation($"Create LMS device {dev}");
        _device = new LimeSdrDevice(dev, true, _logger);
        
        return _device;
    }

    private void DisconnectLmsImpl()
    {
        try
        {
            if (_cancelStream is { Token.CanBeCanceled: true })
            {
                _cancelStream.Cancel(false);
            }

            _cancelStream = null;
            _device?.Dispose();
            _device = null;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async void ConnectLmsImpl()
    {

        if (Environment.Is64BitProcess)
        {
            _logger.LogInformation($"LMS use 64 bit native library");
            LmsNativeDllUsage.Is64BitOperatingSystem = true;
            NativeMethods.Is64BitOperatingSystem = true;
        }
        else
        {
            _logger.LogInformation($"LMS use 32 bit native library");
        }

        var sampleRate = _cfg.SampleRate;
        const double bandWidth = 1e6;
        var txFreq = _cfg.TxFrequency; // - _cfg.FrequencyOffset;
        var rxFreq = _cfg.RxFrequency; // - _cfg.FrequencyOffset;
        var gain = _cfg.TxGain;
        try
        {
            DisconnectLmsImpl();
            _device = CreateDevice();
            _cancelStream = new CancellationTokenSource();

            await _device.EnableChannel(LmsChannel.Tx, 0, true, CancellationToken.None);
            await _device.EnableChannel(LmsChannel.Rx, 0, true, CancellationToken.None);
            await _device.SetSampleRate(sampleRate, 1U, CancellationToken.None);
            await _device.SetFrequency(LmsChannel.Tx, 0, txFreq, CancellationToken.None);
            await _device.SetFrequency(LmsChannel.Rx, 0, rxFreq, CancellationToken.None);
            await _device.SetAntenna(LmsChannel.Tx, 0, (uint)LmsPathTx.LMS_PATH_TX2, CancellationToken.None);
            await _device.SetAntenna(LmsChannel.Rx, 0, (uint)LmsPathRx.LMS_PATH_LNAW, CancellationToken.None);
            await _device.SetBandWidth(LmsChannel.Tx, 0, bandWidth, CancellationToken.None);
            await _device.SetBandWidth(LmsChannel.Rx, 0, bandWidth, CancellationToken.None);
            
            await _device.SetNormalizedGain(LmsChannel.Tx, 0, gain, CancellationToken.None);

            var modeA = new float[2][];
            modeA[0] = ModeACGenerator.GenerateModeAQuery(sampleRate);
            modeA[1] = ModeACGenerator.GenerateModeAWithP2Query(sampleRate);
            
            var modeC = new float[2][];
            modeC[0] = ModeACGenerator.GenerateModeCQuery(sampleRate);
            modeC[1] = ModeACGenerator.GenerateModeCWithP2Query(sampleRate);
            var zeroBuffer = new float[200_000];
            var guardSamples = (ulong)(0.010 * sampleRate);
            _p2 = OnOffP2 ? 1 : 0;
            _msgIdx = 0;
            
            _rxStream = await _device.CreateStream(LmsChannel.Rx, 0, 1024*1024, 0.25f, true, true, _cancelStream!.Token).DisposeItWith(Disposable);
            _txStream = await _device.CreateStream(LmsChannel.Tx, 0, (uint)1024*1024, 0.25f, false, true, _cancelStream!.Token).DisposeItWith(Disposable);
            
            await _rxStream.Start(_cancelStream.Token);
            await _txStream.Start(_cancelStream.Token);

            _dataDebug = new ScottDebugPlot(_avaPlotSh);
            _magDebug = new ScottDebugTriggerPlot(_avaPlotLms, _dataDebug.OnTrigger);


            var magBuff = new (ulong Timestamp, double Data)[100400];
            var txTimeStampBuffer = new ulong[120000];
            
            
            var rxBuffer = new Memory<float>(new float[200_000]); // 62.5 µs
            await _rxStream.Read(rxBuffer, 1000, _cancelStream.Token).ConfigureAwait(false);
            
            var rxTimeStamp = _rxStream.Metadata.timestamp;
            var txTimeStamp = _rxStream.Metadata.timestamp + 80_000;
            var lastTxTimeStamp = txTimeStamp - 1;
            
            var sw = Stopwatch.StartNew();
            var lastLog = TimeSpan.Zero;
            ulong frames = 0;
            
            var rxThread = new Thread(() =>
            {
                while (_cancelStream is { Token.IsCancellationRequested: false })
                {
                    var cnt = _rxStream.Read(rxBuffer, 1000, _cancelStream.Token).Result;
                    rxTimeStamp = _rxStream.Metadata.timestamp;
                    
                    var rxNow = _rxStream.Metadata.timestamp + (ulong)cnt;
                    Interlocked.Exchange(ref _rxNowSamples, rxNow);
                    
                    // if (rxTimeStamp >= txTimeStamp || Math.Abs((decimal)txTimeStamp - rxTimeStamp) > 9223372036854775807m)
                    // {
                    //     txTimeStamp = rxTimeStamp + 40_000_000;
                    // }
                    var k = DetectSignal(rxBuffer.Span, txTimeStampBuffer, magBuff, _rxStream.Metadata.timestamp, ref _rxTimeStampCnt, cnt);
                    var dtRaw = k == 0 ? 0.0 : (double)k / _cfg.SampleRate;
                    Dt = dtRaw - _cfg.DtCal;
                }
            });
            rxThread.Start();
            
            // var txThread = new Thread(() =>
            // {
            //     while (_cancelStream is { Token.IsCancellationRequested: false })
            //     {
            //         if (lastTxTimeStamp == txTimeStamp)
            //         {
            //             // _txStream.Write(zeroBuffer, 1_500, _cancelStream.Token).Wait();
            //             Task.Delay(100).Wait();
            //             continue;
            //         }
            //
            //         lastTxTimeStamp = txTimeStamp;
            //         var stamp = lastTxTimeStamp;
            //         _txStream.EditMetadata(t => t with { timestamp = stamp });
            //         var expectedSamples = modeA[_p2].Length / 2;
            //         var written = _txStream.Write(_msgIdx == 0 ? modeA[_p2] : modeC[_p2], 1_500, _cancelStream.Token).Result;
            //         _msgIdx = Modes ? (++_msgIdx % 2) : 0;
            //         txTimeStampBuffer[_txTimeStampCnt++] = _txStream.Metadata.timestamp;
            //         
            //         if (written != expectedSamples)
            //         {
            //             _logger.LogWarning(
            //                 "TX partial write: written={Written}, expected={Expected}",
            //                 written,
            //                 expectedSamples);
            //         }
            //
            //         frames++;
            //
            //         if (sw.Elapsed - lastLog > TimeSpan.FromSeconds(1))
            //         {
            //             lastLog = sw.Elapsed;
            //
            //             var status = _txStream.GetStatus(_cancelStream.Token).Result;
            //
            //             _logger.LogInformation(
            //                 "TX frames={Frames}, FIFO={Fifo}/{FifoSize}, underrun={Underrun}, dropped={Dropped}, linkRate={Rate}",
            //                 frames,
            //                 status.fifoFilledCount,
            //                 status.fifoSize,
            //                 status.underrun,
            //                 status.droppedPackets,
            //                 status.linkRate);
            //         }
            //     }
            // });
            var txThread = new Thread(() =>
            {
                var token = _cancelStream.Token;

                while (!token.IsCancellationRequested)
                {
                    var rxNow = Interlocked.Read(ref _rxNowSamples);

                    if (rxNow == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    var selectedFrame = _msgIdx == 0
                        ? modeA[_p2]
                        : modeC[_p2];

                    var frameStartTimestamp = rxNow + guardSamples;
                    var queryTimestamp = frameStartTimestamp + 80_000;

                    _txStream.EditMetadata(t => t with
                    {
                        timestamp = (ulong)frameStartTimestamp,
                        waitForTimestamp = true,
                        flushPartialPacket = false
                    });

                    var expectedSamples = selectedFrame.Length / 2;

                    var written = _txStream
                        .Write(selectedFrame, 1500, token)
                        .Result;

                    if (written != expectedSamples)
                    {
                        _logger.LogWarning(
                            "TX partial write: written={Written}, expected={Expected}",
                            written,
                            expectedSamples);
                    }

                    txTimeStampBuffer[_txTimeStampCnt++] = (ulong)queryTimestamp;

                    _msgIdx = Modes ? (++_msgIdx % 2) : 0;

                    var status = _txStream.GetStatus(token).Result;

                    _logger.LogInformation(
                        "TX timestamp={Timestamp}, query={QueryTimestamp}, written={Written}, FIFO={Fifo}/{FifoSize}, underrun={Underrun}, dropped={Dropped}, linkRate={Rate}",
                        frameStartTimestamp,
                        queryTimestamp,
                        written,
                        status.fifoFilledCount,
                        status.fifoSize,
                        status.underrun,
                        status.droppedPackets,
                        status.linkRate);

                    Task.Delay(1000, token).Wait(token); // период запросов, а не hardware timestamp на 1 сек.
                }
            });
            txThread.Start();
            
            
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
        }
    }

    private static readonly double[] ResponseCorrelator = ModeACGenerator.GenerateModeACResponseCorrelator(40000000);

    private ulong DetectSignal(Span<float> rxBuffer, ulong[] txTimeStamp, (ulong Timestamp, double Data)[] magBuff, ulong rxTimeStamp, ref int rxTimeStampCnt, int cnt)
    {
        
        var arr = new double[cnt];
        for (var i = 0; i < cnt; i++)
        {
            arr[i] = rxBuffer[i*2] * rxBuffer[i*2] + rxBuffer[i*2+1] * rxBuffer[i*2+1];
        }
        var stepCnt = cnt - ResponseCorrelator.Length;
        var maxIndex = 0;
        var maxCorr = double.MinValue;
        for (var i = 0; i <= stepCnt; i++)
        {
            var v = 0.0;
            for (var j = i; j < ResponseCorrelator.Length + i; j++)
            {
                v += arr[j] * ResponseCorrelator[j - i];
            }

            if (!(v > maxCorr)) continue;
            maxCorr = v;
            maxIndex = i;
        }

        
        if (rxTimeStampCnt + ResponseCorrelator.Length > magBuff.Length)
        {
            return 0;
        }

        var dataLenght = ResponseCorrelator.Length + 174; // 174 - SPI
        // var figure = new double[dataLenght];
        var endIndex = Math.Min(magBuff.Length, rxTimeStampCnt + dataLenght);
        for (var i = rxTimeStampCnt; i < endIndex; i++)
        {
            if (i - rxTimeStampCnt + maxIndex >= arr.Length)
            {
                endIndex = i;
                break;
            }
            magBuff[i] = (rxTimeStamp + (ulong)maxIndex, arr[i - rxTimeStampCnt + maxIndex]);
            // figure[i - rxTimeStampCnt] = arr[i - rxTimeStampCnt + maxIndex];
        }

        rxTimeStampCnt = endIndex;
        // _magDebug.Begin();
        // _magDebug.AddSignal("Magnitude",figure);
        // _magDebug.AddAnnotation("Index", $"Index: {Interlocked.Increment(ref _magnPlotCounter)}");
        // _magDebug.End();
        
        var txTimeStamp1 = txTimeStamp.LastOrDefault(t => t <= rxTimeStamp);
        if (txTimeStamp1 == 0) return 0;
        var delay = rxTimeStamp + (ulong)maxIndex - txTimeStamp1;
        return delay;
    }

    public ReactiveCommand<Unit, Unit> ConnectLms { get; set; }

    public ReactiveCommand<Unit, Unit> DisconnectLms { get; set; }
    
    [Reactive] public int TxGain { get; set; } = -30;
    [Reactive] public int RxGain { get; set; } = -30;
    [Reactive] public bool OnOffP2 { get; set; }
    [Reactive] public bool Modes { get; set; }
    [Reactive] public double Dt { get; set; }

    public void InitCharts(AvaPlot avaPlotLms, AvaPlot avaPlotSh)
    {
        _avaPlotLms = avaPlotLms;
        _avaPlotSh = avaPlotSh;
    }
}
