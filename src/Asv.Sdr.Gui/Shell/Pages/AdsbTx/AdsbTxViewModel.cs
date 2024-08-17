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
using ReactiveUI;
using ScottPlot.Avalonia;

namespace Asv.Sdr.Gui;

[Export(typeof(IShellPage))]
public class AdsbTxViewModel:ShellPage
{
    private AvaPlot _avaPlotLms;
    private AvaPlot _avaPlotSh;
    private readonly CancellationTokenSource _cancelStream;
    private LimeSdrDevice _device;
    private ILmsStream _txStream;

    public AdsbTxViewModel() : base(WellKnownUri.Shell + ".adsbtx")
    {
        Title = "ADSB Tx";
        Icon = MaterialIconKind.ChartFinance;
        ConnectLms = ReactiveCommand.CreateRunInBackground(ConnectLmsImpl);
    }

    private async void ConnectLmsImpl()
    {
        LmsNativeDllUsage.Is64BitOperatingSystem = true;
        var _sampleRate = 8e6;
        var _bandWidth = 1e6;
        var _freq = 1090_000_000;
        var _gain = 0.69;
        try
        {
            var dev = LimeSdrDevice.GetAvailableDevices().FirstOrDefault();
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
            
            _txStream = await _device.CreateStream(LmsChannel.Tx, 0, (uint)_sampleRate, throughputVsLatency:0, cancel: default).DisposeItWith(Disposable);
            await _txStream.Start(default);
            Observable.Timer(TimeSpan.FromSeconds(0.5),TimeSpan.FromSeconds(0.5))
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