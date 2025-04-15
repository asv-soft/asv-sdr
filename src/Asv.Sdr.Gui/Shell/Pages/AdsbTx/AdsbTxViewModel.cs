using System;
using System.Buffers;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asv.Common;
using Asv.IO;
using Asv.Sdr.LimeSdr;
using Material.Icons;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ScottPlot.Avalonia;

namespace Asv.Sdr.Gui;

[Export(typeof(IShellPage))]
public class AdsbTxViewModel:ShellPage
{
    private readonly ILoggerFactory _loggerFactory;
    private AvaPlot _avaPlotLms;
    private AvaPlot _avaPlotSh;
    private readonly CancellationTokenSource _cancelStream;
    private LimeSdrDevice _device;
    private ILmsStream _txStream;
    private readonly ILogger<AdsbTxViewModel> _logger;


    public AdsbTxViewModel() : base(WellKnownUri.Shell + ".adsbtx")
    {
        Title = "ADSB Tx";
        Icon = MaterialIconKind.ChartFinance;
        ConnectLms = ReactiveCommand.CreateRunInBackground(ConnectLmsImpl);
    }

    [ImportingConstructor]
    public AdsbTxViewModel(ILoggerFactory loggerFactory):this()
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<AdsbTxViewModel>();
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
        
        var _sampleRate = 8e6;
        var _bandWidth = 1e6;
        var _freq = 1090_000_000;
        var _gain = 0.59;
        try
        {
            var dev = LimeSdrDevice.GetAvailableDevices().FirstOrDefault(x => x.Contains("1D90E8D5A1BB43"));
            if (dev == null) throw new Exception("LMS device not found");
            _device = new LimeSdrDevice(dev, true);
            await _device.EnableChannel(LmsChannel.Tx, 0, true,default);
            await _device.SetSampleRate(_sampleRate, 0U,default);
            await _device.SetAntenna(LmsChannel.Tx, 0, (uint) LmsPathTx.LMS_PATH_TX1 , default);
            await _device.SetBandWidth(LmsChannel.Tx, 0, _bandWidth, default);
            
            await _device.SetFrequency(LmsChannel.Tx, 0, _freq, default);
            await _device.SetNormalizedGain(LmsChannel.Tx, 0, _gain, default);
            await _device.Calibrate(LmsChannel.Tx,0,_bandWidth, 0, default);

            var preamble = new double[] { 1, 0, 1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0 };

            var messages = new byte[3][];
            
            messages[0] =
            [
                0x8F, 0x15, 0x1F, 0x1A, 0x23, 0x55, 0x40, 0x75, 0xE7, 0x38, 0x20, 0x42, 0x6A, 0x2C
            ];
            messages[1] =
            [
                0x8F, 0x15, 0x1F, 0x1A, 0x59, 0x05, 0xE0, 0xDD, 0xE5, 0x9F, 0xF3, 0x63, 0x5F, 0x41
            ];
            messages[2] =
            [
                0x8F, 0x15, 0x1F, 0x1A, 0x59, 0x05, 0xE4, 0x40, 0x97, 0x48, 0x6B, 0xB4, 0xB0, 0x90
            ];

            var bits = new double[3][];
            bits[0] = new double[messages[0].Length * 8 * 2 + preamble.Length];
            bits[1] = new double[messages[1].Length * 8 * 2 + preamble.Length];
            bits[2] = new double[messages[2].Length * 8 * 2 + preamble.Length];
            for (var i = 0; i < preamble.Length; i++)
            {
                bits[0][i] = preamble[i];
                bits[1][i] = preamble[i];
                bits[2][i] = preamble[i];
            }
            for (uint i = 0; i < messages[0].Length*8; i++)
            {
                if (BitHelper.GetBitU(messages[0], i, 1) > 0)
                {
                    bits[0][i * 2 + preamble.Length] = 1;
                    bits[0][i * 2 + preamble.Length + 1] = 0;
                }
                else
                {
                    bits[0][i * 2 + preamble.Length] = 0;
                    bits[0][i * 2 + preamble.Length + 1] = 1;
                }
                
                if (BitHelper.GetBitU(messages[1], i, 1) > 0)
                {
                    bits[1][i * 2 + preamble.Length] = 1;
                    bits[1][i * 2 + preamble.Length + 1] = 0;
                }
                else
                {
                    bits[1][i * 2 + preamble.Length] = 0;
                    bits[1][i * 2 + preamble.Length + 1] = 1;
                }
                
                if (BitHelper.GetBitU(messages[2], i, 1) > 0)
                {
                    bits[2][i * 2 + preamble.Length] = 1;
                    bits[2][i * 2 + preamble.Length + 1] = 0;
                }
                else
                {
                    bits[2][i * 2 + preamble.Length] = 0;
                    bits[2][i * 2 + preamble.Length + 1] = 1;
                }
            }

            var msgIdx = 0;
            
            // _txStream = await _device.CreateStream(LmsChannel.Tx, 0, (uint)_sampleRate, throughputVsLatency:0, cancel: default).DisposeItWith(Disposable);
            _txStream = await _device.CreateStream(LmsChannel.Tx, 0, (uint)bits[0].Length * 8, throughputVsLatency:0, cancel: default).DisposeItWith(Disposable);
            await _txStream.Start(default);
            Observable.Timer(TimeSpan.FromSeconds(0.5),TimeSpan.FromSeconds(0.5))
                .Subscribe(x =>
                {
                    var bitLength = (int)(_sampleRate / 2e6);
                    var bufferSize = bits[msgIdx].Length * bitLength*2;
                    var buffer = new float[bufferSize];
                    var bufferMemory = new ReadOnlyMemory<float>(buffer, 0, bufferSize);
                    
                    for (var i = 0; i < bits[msgIdx].Length; i++)
                    {
                        for (int j = 0; j < bitLength; j++)
                        {
                            buffer[i*2 * bitLength + j] = (float)bits[msgIdx][i];
                            buffer[i*2 * bitLength + j + 1] = (float)bits[msgIdx][i];
                        }
                    }
                    _txStream.Write(bufferMemory, 10_000, default).Wait();
                    
                    for (var i = 0; i < bits[msgIdx].Length; i++)
                    {
                        for (int j = 0; j < bitLength; j++)
                        {
                            buffer[i*2 * bitLength + j] = 0;
                            buffer[i*2 * bitLength + j + 1] = 0;
                        }
                    }

                    _txStream.Write(bufferMemory, 10_000, default).Wait();
                    msgIdx = (msgIdx + 1) % 3;

                });
        
           

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