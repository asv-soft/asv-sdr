using System;
using System.Composition;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
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


public class AdsbInterrogateConfig 
{
    public ulong Frequency { get; set; } = 1030000000;
    public ulong FrequencyOffset { get; set; } = 35000;
    public ulong SampleRate { get; set; } = 16000000;
    public LmsPathTx LmsPathTx { get; set; } = LmsPathTx.LMS_PATH_TX1;

    public double Amplitude { get; set; } = 1.0;
    
    public int Gain { get; set; } = -30;
    
    public string SerialNumber { get; set; } = string.Empty;
}

[Export(typeof(IShellPage))]
public class AdsbInterrogateViewModel : ShellPage
{
    private const int IcaoAddress = 0x150777; //0x151F1A;
    private AvaPlot _avaPlotLms;
    private AvaPlot _avaPlotSh;
    private CancellationTokenSource? _cancelStream;
    private ILimeSdrDevice? _device;
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
        _logger = loggerFactory.CreateLogger<AdsbInterrogateViewModel>();
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
                var gain = (uint)(x + 69);
                _device?.SetNormalizedGainDbm(LmsChannel.Tx, 0, gain, CancellationToken.None).Wait();
                _cfg.Gain = x;
                cfg.Set(_cfg);

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


            var messages = new ReadOnlyMemory<float>[11];

            // Debug message AAAA AAAA 01FD2B
            // var m11 = ModeSGenerator.GenerateModeSQuery([0xAA, 0xAA, 0xAA, 0xAA, 0x01, 0xFD, 0x2B], sampleRate,
            //     _cfg.FrequencyOffset, _cfg.Amplitude);

            var buffU4RR17 = new byte[7];
            var buffU4RR18 = new byte[7];
            var buffU4RR20 = new byte[7];
            var buffU4RR21 = new byte[7];
            var buffU4RR22 = new byte[7];
            var buffU5RR17 = new byte[7];
            var buffU5RR18 = new byte[7];
            var buffU5RR20 = new byte[7];
            var buffU5RR21 = new byte[7];
            var buffU5RR22 = new byte[7];
            var buffU11 = new byte[7];
            
            // var buffU20 = new byte[14];
            // var buffU21 = new byte[14];
            
            var msgU4RR17 = new ModeSUF4
            {
                IcaoAddress = IcaoAddress,
                RR = 1
            };
            var spanU4RR17 = new Span<byte>(buffU4RR17);
            msgU4RR17.Serialize(ref spanU4RR17);
            
            var msgU4RR18 = new ModeSUF4
            {
                IcaoAddress = IcaoAddress,
                RR = 1
            };
            var spanU4RR18 = new Span<byte>(buffU4RR18);
            msgU4RR18.Serialize(ref spanU4RR18);
            
            var msgU4RR20 = new ModeSUF4
            {
                IcaoAddress = IcaoAddress,
                RR = 1
            };
            var spanU4RR20 = new Span<byte>(buffU4RR20);
            msgU4RR20.Serialize(ref spanU4RR20);
            
            var msgU4RR21 = new ModeSUF4
            {
                IcaoAddress = IcaoAddress,
                RR = 1
            };
            var spanU4RR21 = new Span<byte>(buffU4RR21);
            msgU4RR21.Serialize(ref spanU4RR21);
            
            var msgU4RR22 = new ModeSUF4
            {
                IcaoAddress = IcaoAddress,
                RR = 1
            };
            var spanU4RR22 = new Span<byte>(buffU4RR22);
            msgU4RR22.Serialize(ref spanU4RR22);
            
            var msgU5RR17 = new ModeSUF5
            {
                IcaoAddress = IcaoAddress,
                RR = 1
            };
            var spanU5RR17 = new Span<byte>(buffU5RR17);
            msgU5RR17.Serialize(ref spanU5RR17);
            
            var msgU5RR18 = new ModeSUF5
            {
                IcaoAddress = IcaoAddress,
                RR = 1
            };
            var spanU5RR18 = new Span<byte>(buffU5RR18);
            msgU5RR18.Serialize(ref spanU5RR18);
            
            var msgU5RR20 = new ModeSUF5
            {
                IcaoAddress = IcaoAddress,
                RR = 1
            };
            var spanU5RR20 = new Span<byte>(buffU5RR20);
            msgU5RR20.Serialize(ref spanU5RR20);
            
            var msgU5RR21 = new ModeSUF5
            {
                IcaoAddress = IcaoAddress,
                RR = 1
            };
            var spanU5RR21 = new Span<byte>(buffU5RR21);
            msgU5RR21.Serialize(ref spanU5RR21);
            
            var msgU5RR22 = new ModeSUF5
            {
                IcaoAddress = IcaoAddress,
                RR = 1
            };
            var spanU5RR22 = new Span<byte>(buffU5RR22);
            msgU5RR22.Serialize(ref spanU5RR22);
            
            // var msgU20 = new ModeSUF20
            // {
            //     IcaoAddress = IcaoAddress
            // };
            // var spanU20 = new Span<byte>(buffU20);
            // msgU20.Serialize(ref spanU20);
            //
            // var msgU21 = new ModeSUF21
            // {
            //     IcaoAddress = IcaoAddress
            // };
            // var spanU21 = new Span<byte>(buffU21);
            // msgU21.Serialize(ref spanU21);
            
            var msgU11 = new ModeSUF11();
            var spanU11 = new Span<byte>(buffU11);
            msgU11.Serialize(ref spanU11);
            
            var m11 = ModeSGenerator.GenerateModeSQuery(buffU11, sampleRate,
                /* _cfg.FrequencyOffset,*/ _cfg.Amplitude);
            var m4rr17 = ModeSGenerator.GenerateModeSQuery(buffU4RR17, sampleRate,
                /* _cfg.FrequencyOffset,*/ _cfg.Amplitude);
            var m4rr18 = ModeSGenerator.GenerateModeSQuery(buffU4RR18, sampleRate,
                /* _cfg.FrequencyOffset,*/ _cfg.Amplitude);
            var m4rr20 = ModeSGenerator.GenerateModeSQuery(buffU4RR20, sampleRate,
                /* _cfg.FrequencyOffset,*/ _cfg.Amplitude);
            var m4rr21 = ModeSGenerator.GenerateModeSQuery(buffU4RR21, sampleRate,
                /* _cfg.FrequencyOffset,*/ _cfg.Amplitude);
            var m4rr22 = ModeSGenerator.GenerateModeSQuery(buffU4RR22, sampleRate,
                /* _cfg.FrequencyOffset,*/ _cfg.Amplitude);
            var m5rr17 = ModeSGenerator.GenerateModeSQuery(buffU5RR17, sampleRate,
                /* _cfg.FrequencyOffset,*/ _cfg.Amplitude);
            var m5rr18 = ModeSGenerator.GenerateModeSQuery(buffU5RR18, sampleRate,
                /* _cfg.FrequencyOffset,*/ _cfg.Amplitude);
            var m5rr20 = ModeSGenerator.GenerateModeSQuery(buffU5RR20, sampleRate,
                /* _cfg.FrequencyOffset,*/ _cfg.Amplitude);
            var m5rr21 = ModeSGenerator.GenerateModeSQuery(buffU5RR21, sampleRate,
                /* _cfg.FrequencyOffset,*/ _cfg.Amplitude);
            var m5rr22 = ModeSGenerator.GenerateModeSQuery(buffU5RR22, sampleRate,
                /* _cfg.FrequencyOffset,*/ _cfg.Amplitude);
            
            var message11 = new float[m11.Length * 100];
            var message04rr17 = new float[m4rr17.Length * 100];
            var message04rr18 = new float[m4rr18.Length * 100];
            var message04rr20 = new float[m4rr20.Length * 100];
            var message04rr21 = new float[m4rr21.Length * 100];
            var message04rr22 = new float[m4rr21.Length * 100];
            var message05rr17 = new float[m5rr17.Length * 100];
            var message05rr18 = new float[m5rr18.Length * 100];
            var message05rr20 = new float[m5rr20.Length * 100];
            var message05rr21 = new float[m5rr21.Length * 100];
            var message05rr22 = new float[m5rr22.Length * 100];
            // var message20 = new float[m4.Length * 100];
            // var message21 = new float[m5.Length * 100];
            Array.Copy(m11, message11, m11.Length);
            Array.Copy(m4rr17, message04rr17, m4rr17.Length);
            Array.Copy(m4rr18, message04rr18, m4rr18.Length);
            Array.Copy(m4rr20, message04rr20, m4rr20.Length);
            Array.Copy(m4rr21, message04rr21, m4rr21.Length);
            Array.Copy(m4rr22, message04rr22, m4rr22.Length);
            Array.Copy(m5rr17, message05rr17, m5rr17.Length);
            Array.Copy(m5rr18, message05rr18, m5rr18.Length);
            Array.Copy(m5rr20, message05rr20, m5rr20.Length);
            Array.Copy(m5rr21, message05rr21, m5rr21.Length);
            Array.Copy(m5rr22, message05rr22, m5rr22.Length);
            
            messages[10] = new ReadOnlyMemory<float>(message11);
            messages[0] = new ReadOnlyMemory<float>(message04rr17);
            messages[1] = new ReadOnlyMemory<float>(message05rr17);
            messages[2] = new ReadOnlyMemory<float>(message04rr18);
            messages[3] = new ReadOnlyMemory<float>(message05rr18);
            messages[4] = new ReadOnlyMemory<float>(message04rr20);
            messages[5] = new ReadOnlyMemory<float>(message05rr20);
            messages[6] = new ReadOnlyMemory<float>(message04rr21);
            messages[7] = new ReadOnlyMemory<float>(message05rr21);
            messages[8] = new ReadOnlyMemory<float>(message04rr22);
            messages[9] = new ReadOnlyMemory<float>(message05rr22);
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
            
            var thread = new Thread(() =>
            {
                while (_cancelStream is { Token.IsCancellationRequested: false })
                {
                    _txStream.Write(messages[msgIdx], 10_000, _cancelStream.Token).Wait();
                    msgIdx = ++msgIdx % 11;
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
    
    [Reactive] public int TxGain { get; set; } = -30;

    public void InitCharts(AvaPlot avaPlotLms, AvaPlot avaPlotSh)
    {
        _avaPlotLms = avaPlotLms;
        _avaPlotSh = avaPlotSh;
    }
}