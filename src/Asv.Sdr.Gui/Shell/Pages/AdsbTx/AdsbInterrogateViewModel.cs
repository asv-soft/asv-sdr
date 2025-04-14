using System;
using System.Collections.Generic;
using System.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Asv.Cfg;
using Asv.Common;
using Asv.Sdr.LimeSdr;
using Avalonia.Threading;
using Material.Icons;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ScottPlot.Avalonia;
using ZLogger;

namespace Asv.Sdr.Gui;


public class AdsbInterrogateConfig 
{
    public ulong Frequency { get; set; } = 1030000000;
    public ulong FrequencyOffset { get; set; } = 35000;
    public ulong SampleRate { get; set; } = 16000000;
    public LmsPathTx LmsPathTx { get; set; } = LmsPathTx.LMS_PATH_TX1;

    public double Amplitude { get; set; } = 1.0;
    
    public double Gain { get; set; } = 0.69;
    
    public string SerialNumber { get; set; } = string.Empty;
}

[Export(typeof(IShellPage))]
public class AdsbInterrogateViewModel : ShellPage
{
    private const int IcaoAddress = 0x150777; //0x151F1A;
    private readonly ILoggerFactory _loggerFactory;
    private AvaPlot _avaPlotLms;
    private AvaPlot _avaPlotSh;
    private CancellationTokenSource? _cancelStream;
    private ILimeSdrDevice? _device;
    private ILimeSdrDevice _adsbReplyDevice;
    private ILmsStream _txStream;
    private readonly AdsbInterrogateConfig _cfg;
    private readonly ILogger<AdsbInterrogateViewModel> _logger;
    private ScottDebugPlot _dataDebug;
    private bool _flag = false;


    public AdsbInterrogateViewModel() : base(WellKnownUri.Shell + ".adsbInter")
    {
        Title = "ADS-B Interrogate";
        Icon = MaterialIconKind.ChartFinance;
        ConnectLms = ReactiveCommand.CreateRunInBackground(ConnectLmsImpl);
        DisconnectLms = ReactiveCommand.CreateRunInBackground(DisconnectLmsImpl);
    }

    [ImportingConstructor]
    public AdsbInterrogateViewModel(IConfiguration cfg, ILoggerFactory loggerFactory) : this()
    {
        
        
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<AdsbInterrogateViewModel>();
        _cfg = cfg.Get<AdsbInterrogateConfig>();
        TxGain = _cfg.Gain;
        Disposable.AddAction(() =>
        {
            _cancelStream?.Cancel(false);
            _device?.Dispose();
        });
        
        this.WhenAnyValue(x => x.TxGain)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Subscribe(x =>
            {
                _device?.SetNormalizedGain(LmsChannel.Tx, 0, x, default).Wait();
                _cfg.Gain = x;
                cfg.Set(_cfg);

            }).DisposeItWith(Disposable);
    }

    private Task AdsbSetIsEnabled(bool enabled, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting DME mode to {enabled}");
        return _adsbReplyDevice.WriteFpgaRegisterBits(208, 0, 1, (ushort)(enabled ? 1 : 0), cancel);
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
        
        // var dev1 = devs.FirstOrDefault(id => id.Contains("1D90E8D5A1BB43"));
        // if (dev1 == null) throw new Exception("LMS device not found");
        // _logger.ZLogInformation($"Create LMS device {dev1}");
        // _adsbReplyDevice = new LimeSdrDevice(dev1,true,_logger);

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
        var freq = _cfg.Frequency; // - _cfg.FrequencyOffset;
        var gain = _cfg.Gain;
        try
        {
            DisconnectLmsImpl();
            _device = CreateDevice();
            _cancelStream = new CancellationTokenSource();

            await _device.EnableChannel(LmsChannel.Tx, 0, true, CancellationToken.None);
            await _device.SetSampleRate(sampleRate, 1U, CancellationToken.None);
            await _device.SetAntenna(LmsChannel.Tx, 0, (uint)LmsPathTx.LMS_PATH_TX1, CancellationToken.None);
            await _device.SetBandWidth(LmsChannel.Tx, 0, bandWidth, CancellationToken.None);

            await _device.SetFrequency(LmsChannel.Tx, 0, freq, CancellationToken.None);
            await _device.SetNormalizedGain(LmsChannel.Tx, 0, gain, CancellationToken.None);
            // await _device.Calibrate(LmsChannel.Tx,0,bandWidth, 0, CancellationToken.None);


            var messages = new ReadOnlyMemory<float>[3];

            // Debug message AAAA AAAA 01FD2B
            // var m11 = ModeSGenerator.GenerateModeSQuery([0xAA, 0xAA, 0xAA, 0xAA, 0x01, 0xFD, 0x2B], sampleRate,
            //     _cfg.FrequencyOffset, _cfg.Amplitude);

            var buffU4 = new byte[7];
            var buffU5 = new byte[7];
            var buffU11 = new byte[7];
            var msgU4 = new ModeSUF4
            {
                IcaoAddress = IcaoAddress
            };
            var spanU4 = new Span<byte>(buffU4);
            msgU4.Serialize(ref spanU4);
            
            var msgU5 = new ModeSUF5
            {
                IcaoAddress = IcaoAddress
            };
            var spanU5 = new Span<byte>(buffU5);
            msgU5.Serialize(ref spanU5);
            
            var msgU11 = new ModeSUF11();
            var spanU11 = new Span<byte>(buffU11);
            msgU11.Serialize(ref spanU11);
            
            var m11 = ModeSGenerator.GenerateModeSQuery(buffU11, sampleRate,
                /* _cfg.FrequencyOffset,*/ _cfg.Amplitude);
            var m04 = ModeSGenerator.GenerateModeSQuery(buffU4, sampleRate,
                /* _cfg.FrequencyOffset,*/ _cfg.Amplitude);
            var m05 = ModeSGenerator.GenerateModeSQuery(buffU5, sampleRate,
                /* _cfg.FrequencyOffset,*/ _cfg.Amplitude);
            var message11 = new float[m11.Length * 100];
            var message04 = new float[m04.Length * 100];
            var message05 = new float[m05.Length * 100];
            Array.Copy(m11, message11, m11.Length);
            Array.Copy(m04, message04, m04.Length);
            Array.Copy(m05, message05, m05.Length);
            messages[2] = new ReadOnlyMemory<float>(message11);
            messages[0] = new ReadOnlyMemory<float>(message04);
            messages[1] = new ReadOnlyMemory<float>(message05);
            var msgIdx = 0;

            // Dispatcher.UIThread.Post(() =>   
            // {
            //     var buffer = new double[messages[0].Length / 2];
            //     for (var i = 0; i < buffer.Length; i++)
            //     {
            //         var msg = messages[0].ToArray();
            //         var amplitude = Math.Sqrt(msg[2 * i] * msg[2 * i] +
            //                                   msg[2 * i + 1] * msg[2 * i + 1]);
            //         var phase = Math.Atan2(msg[2 * i + 1], msg[2 * i]);
            //         buffer[i] = amplitude * Math.Cos(phase);
            //     }
            //     _avaPlotSh.IsEnabled = true;
            //     _dataDebug = new ScottDebugPlot(_avaPlotSh);
            //     _dataDebug.Begin();
            //     _dataDebug.AddSignal("UF11", buffer);
            //     _dataDebug.End();
            // });



            var zeroBuff = new float[messages[0].Length];
            _txStream = await _device.CreateStream(LmsChannel.Tx, 0, (uint)messages[0].Length,
                throughputVsLatency: 1.0f, cancel: _cancelStream!.Token).DisposeItWith(Disposable);
            await _txStream.Start(_cancelStream.Token);
            Observable.Timer(TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1))
                .Subscribe(x =>
                {
                    _flag = true;
                    // if (_cancelStream is not { Token.IsCancellationRequested: false }) return;
                    // _txStream.Write(messages[msgIdx], 10_000, _cancelStream.Token).Wait();
                    // msgIdx = ++msgIdx % 3;
                    // for (var i = 0; i < 2 && _cancelStream is { Token.IsCancellationRequested: false }; i++)
                    // {
                    //     _txStream.Write(zeroBuff, 10_000, _cancelStream.Token).Wait();
                    // }

                }).DisposeItWith(Disposable);

            var thread = new Thread(() =>
            {
                while (_cancelStream is { Token.IsCancellationRequested: false })
                {
                    _txStream.Write(messages[msgIdx], 10_000, _cancelStream.Token).Wait();
                    msgIdx = ++msgIdx % 2;
                    for (var i = 0; i < 10 && _cancelStream is { Token.IsCancellationRequested: false }; i++)
                    {
                        _txStream.Write(zeroBuff, 10_000, _cancelStream.Token).Wait();
                    }
            
                    // if (_flag)
                    // {
                    //     _txStream.Write(messages[msgIdx], 10_000, _cancelStream.Token).Wait();
                    //     msgIdx = ++msgIdx % 3;
                    //     _flag = false;
                    // }
                    // else
                    // {
                    //     _txStream.Write(zeroBuff, 10_000, _cancelStream.Token).Wait();
                    // }
                }
            });
            thread.Start();


        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
        }
    }

    public ReactiveCommand<Unit, Unit> ConnectLms { get; set; }

    public ReactiveCommand<Unit, Unit> DisconnectLms { get; set; }
    
    [Reactive] public double TxGain { get; set; } = 0.0;

    public void InitCharts(AvaPlot avaPlotLms, AvaPlot avaPlotSh)
    {
        _avaPlotLms = avaPlotLms;
        _avaPlotSh = avaPlotSh;
    }
}