using System;
using System.Collections.Generic;
using System.Composition;
using System.Globalization;
using System.IO;
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
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ScottPlot.Avalonia;
using ZLogger;

namespace Asv.Sdr.Gui;

public class AdsbReplyConfig
{
    public double RxGain { get; set; } = 0.69;
    public double TxGain { get; set; } = 0.69;

    public string SerialNumber { get; set; } = string.Empty;
}

[Export(typeof(IShellPage))]
public class AdsbReplyViewModel:ShellPage
{
    private AvaPlot _avaPlotLms;
    private AvaPlot _avaPlotSh;
    private readonly ILogger<AdsbReplyViewModel> _logger;
    private readonly AdsbReplyConfig _cfg;
    private ILimeSdrAdsbDevice? _device;
    private readonly IConfiguration _cfgSrv;

    private const int IcaoAddress = 0x150777; //0x112149; //0x151F1A;

    private ulong _DF20Cnt = 0;
    private ulong _DF21Cnt = 0;
    private ulong _prevUF4Stat = 0;
    private ulong _prevUF5Stat = 0;
    private ulong _prevUF20Stat = 0;
    private ulong _prevUF21Stat = 0;
    private ulong _prevUF11Stat = 0;
    private ulong _prevAnyUFStat = 0;
    private ulong _prevDF4Stat = 0;
    private ulong _prevDF5Stat = 0;
    private ulong _prevDF20Stat = 0;
    private ulong _prevDF21Stat = 0;
    private ulong _prevDF11Stat = 0;
    private FileStream _strem;
    private StreamWriter _writer;
    private bool _isZeroWrited;
    
    private bool _isDF4ZeroWrited;
    private bool _isDF5ZeroWrited;
    private bool _isDF20ZeroWrited;
    private bool _isDF21ZeroWrited;
    private bool _isDF17IdZeroWrited;
    private bool _isDF17PosZeroWrited;
    private bool _isDF17VelZeroWrited;
    private bool _isDF11ZeroWrited;
    private int _txGainFlag;
    private int _rxGainFlag;
    private int _readUfFlag;


    public AdsbReplyViewModel() : base(WellKnownUri.Shell + ".adsbReply")
    {
        Title = "ADS-B Reply";
        Icon = MaterialIconKind.ChartFinance;
        ConnectLms = ReactiveCommand.CreateRunInBackground(ConnectLmsImpl);
        DisconnectLms = ReactiveCommand.CreateRunInBackground(DisconnectLmsImpl);
        ApplyIcao = ReactiveCommand.CreateRunInBackground(ApplyIcaoIml);
        ApplyUFType = ReactiveCommand.CreateRunInBackground(ApplyUFTypeIml);
    }

    private void ApplyIcaoIml()
    {
        if (uint.TryParse(MyIcaoStr, NumberStyles.HexNumber | NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var result))
        {
            if (result <= 0xFFFFFF)
                MyIcao = result;
            else
                MyIcaoStr = MyIcao.ToString("X6");
        }
        else
        {
            MyIcaoStr = MyIcao.ToString("X6");
        }
    }
    
    private void ApplyUFTypeIml()
    {
        if (byte.TryParse(AnyUFTypeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            if (result is 4 or 5 or 11 or 20 or 21)
                AnyUFType = result;
            else
                AnyUFTypeStr = AnyUFType.ToString();
        }
        else
        {
            AnyUFTypeStr = AnyUFType.ToString();
        }
    }

    [ImportingConstructor]
    public AdsbReplyViewModel(IConfiguration cfg, ILoggerFactory loggerFactory):this()
    {
        var m = new ModeSUF4();
        
        _cfgSrv = cfg;
        _logger = loggerFactory.CreateLogger<AdsbReplyViewModel>();
        _cfg = cfg.Get<AdsbReplyConfig>();

        TxGain = _cfg.TxGain;
        RxGain = _cfg.RxGain;
        
        this.WhenAnyValue(x => x.TxGain)
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Subscribe(x =>
            {
                if (Interlocked.CompareExchange(ref _txGainFlag, 1, 0) != 0) return;
                try
                {
                    var gain = Math.Round(x, 2);
                    _device?.SetNormalizedGain(LmsChannel.Tx, 0, gain, CancellationToken.None).Wait();
                    _cfg.TxGain = gain;
                    _cfgSrv.Set(_cfg);
                    TxGainStr = gain.ToString("F2", CultureInfo.InvariantCulture);
                }
                finally
                {
                    Interlocked.Exchange(ref _txGainFlag, 0);
                }
                
            }).DisposeItWith(Disposable);
        
        this.WhenAnyValue(x => x.RxGain)
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Subscribe(x =>
            {
                if (Interlocked.CompareExchange(ref _rxGainFlag, 1, 0) != 0) return;
                try
                {
                    var gain = Math.Round(x, 2);
                    _device?.SetNormalizedGain(LmsChannel.Rx, 0, gain, CancellationToken.None).Wait();
                    _cfg.RxGain = gain;
                    _cfgSrv.Set(_cfg);
                    RxGainStr = gain.ToString("F2", CultureInfo.InvariantCulture);
                }
                finally
                {
                    Interlocked.Exchange(ref _rxGainFlag, 0);
                }
                
            }).DisposeItWith(Disposable);

        this.WhenAnyValue(x => x.DfDelay)
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Subscribe(x =>
            {
                var delay = Math.Round(x / 0.025) * 0.025;
                _device?.SetDfDelay(delay).Wait();
                DfDelayStr = delay.ToString("F3", CultureInfo.InvariantCulture);
            }).DisposeItWith(Disposable);

        // ******* DF17 ********
        this.WhenAnyValue(x => x.OnOffDf17Id)
            .ObserveOn(Scheduler.Default)
            .Subscribe(isOn =>
            {
                UpdateMessageDF17Id(CancellationToken.None).Wait();
                _device?.SetDf17IdIsEnabled(isOn).Wait();
            })
            .DisposeItWith(Disposable);
        this.WhenAnyValue(x => x.OnOffDf17Pos)
            .ObserveOn(Scheduler.Default)
            .Subscribe(isOn =>
            {
                UpdateMessageDF17Position(CancellationToken.None).Wait();
                _device?.SetDf17PositionIsEnabled(isOn).Wait();
            })
            .DisposeItWith(Disposable);
        this.WhenAnyValue(x => x.OnOffDf17Vel)
            .ObserveOn(Scheduler.Default)
            .Subscribe(isOn =>
            {
                UpdateMessageDF17Velocity(CancellationToken.None).Wait();
                _device?.SetDf17VelocityIsEnabled(isOn).Wait();
            })
            .DisposeItWith(Disposable);
        // *********************
        
        // ******* DFXX ********
        this.WhenAnyValue(x => x.OnOffDf11Reply)
            .ObserveOn(Scheduler.Default)
            .Subscribe(isOn =>
            {
                UpdateMessageDF11(CancellationToken.None).Wait();
                _device?.SetDf11ReplyIsEnabled(isOn).Wait();
            }).DisposeItWith(Disposable);
        this.WhenAnyValue(x => x.OnOffDf11Squitter)
            .ObserveOn(Scheduler.Default)
            .Subscribe(isOn =>
            {
                _device?.SetDf11BroadcastIsEnabled(isOn).Wait();
            }).DisposeItWith(Disposable);
        this.WhenAnyValue(x => x.OnOffDf4Reply)
            .ObserveOn(Scheduler.Default)
            .Subscribe(isOn =>
            {
                UpdateMessageDF4(CancellationToken.None).Wait();
                _device?.SetDf4IsEnabled(isOn).Wait();
                
            }).DisposeItWith(Disposable);
        this.WhenAnyValue(x => x.OnOffDf5Reply)
            .ObserveOn(Scheduler.Default)
            .Subscribe(isOn =>
            {
                UpdateMessageDF5(CancellationToken.None).Wait();
                _device?.SetDf5IsEnabled(isOn).Wait();
            }).DisposeItWith(Disposable);
        this.WhenAnyValue(x => x.OnOffDf20Reply)
            .ObserveOn(Scheduler.Default)
            .Subscribe(isOn =>
            {
                UpdateBDS10(CancellationToken.None).Wait();
                UpdateBDS20(CancellationToken.None).Wait();
                UpdateBDS40(CancellationToken.None).Wait();
                UpdateBDS50(CancellationToken.None).Wait();
                UpdateBDS60(CancellationToken.None).Wait();
                _device?.SetDf20IsEnabled(isOn).Wait();
            }).DisposeItWith(Disposable);
        this.WhenAnyValue(x => x.OnOffDf21Reply)
            .ObserveOn(Scheduler.Default)
            .Subscribe(isOn =>
            {
                UpdateBDS10(CancellationToken.None).Wait();
                UpdateBDS20(CancellationToken.None).Wait();
                UpdateBDS40(CancellationToken.None).Wait();
                UpdateBDS50(CancellationToken.None).Wait();
                UpdateBDS60(CancellationToken.None).Wait();
                _device?.SetDf21IsEnabled(isOn).Wait();
            }).DisposeItWith(Disposable);
        // *********************
        
        this.WhenAnyValue(x => x.AnyUFType)
            .ObserveOn(Scheduler.Default)
            .Subscribe(format =>
            {
                _device?.SetAnyUFType(format, CancellationToken.None).Wait();
            }).DisposeItWith(Disposable);
        
        
        this.WhenAnyValue(x => x.MyIcao)
            .ObserveOn(Scheduler.Default)
            .Subscribe(icao =>
            {
                UpdateMessageDF11(CancellationToken.None).Wait();
            }).DisposeItWith(Disposable);
        
        Observable.Timer(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(100))
            .Subscribe(UpdateUFMessage)
            .DisposeItWith(Disposable);
        
        // Observable.Timer(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(400))
        //     .Subscribe(UpdateUFMessage)
        //     .DisposeItWith(Disposable);
        
        
        Disposable.AddAction(() =>
        {
            _device?.Dispose();
        });
    }

    #region Update message

    private async Task UpdateMessageDFCnt(CancellationToken cancel)
    {
        if (_device is { IsDisposed: false })
        {
            var statDF4_5 = await _device.GetDF4DF5Stat(cancel).ConfigureAwait(false);
            DF4Cnt += _prevDF4Stat <= statDF4_5[0] ? statDF4_5[0] - _prevDF4Stat : 256U + statDF4_5[0] - _prevDF4Stat;
            DF5Cnt += _prevDF5Stat <= statDF4_5[1] ? statDF4_5[1] - _prevDF5Stat : 256U + statDF4_5[1] - _prevDF5Stat;
            _prevDF4Stat = statDF4_5[0];
            _prevDF5Stat = statDF4_5[1];
        }
        if (_device is { IsDisposed: false })
        {
            var statDF20_21 = await _device.GetDF20DF21Stat(cancel).ConfigureAwait(false);
            _DF20Cnt += _prevDF20Stat <= statDF20_21[0] ? statDF20_21[0] - _prevDF20Stat : 256U + statDF20_21[0] - _prevDF20Stat;
            BDSCnt = _DF20Cnt;
            _DF21Cnt += _prevDF21Stat <= statDF20_21[1] ? statDF20_21[1] - _prevDF21Stat : 256U + statDF20_21[1] - _prevDF21Stat;
            _prevDF20Stat = statDF20_21[0];
            _prevDF21Stat = statDF20_21[1];
        }
        if (_device is { IsDisposed: false })
        {
            var statDF11 = await _device.GetDF11ReserveStat(cancel).ConfigureAwait(false);
            DF11Cnt += _prevDF11Stat <= statDF11[0] ? statDF11[0] - _prevDF11Stat : 256U + statDF11[0] - _prevDF11Stat;
            _prevDF11Stat = statDF11[0];
        }
    }
    
    private async Task UpdateMessageUF11(CancellationToken cancel)
    {
        if (_device == null || _device.IsDisposed) return;
        var statUF11_Any = await _device.GetUF11AnyUFStat(cancel).ConfigureAwait(false);
        var needsToBeUpdatedUF11 = _prevUF11Stat != statUF11_Any[0];

        if (needsToBeUpdatedUF11)
        {
            UF11Cnt += _prevUF11Stat <= statUF11_Any[0] ? statUF11_Any[0] - _prevUF11Stat : 256U + statUF11_Any[0] - _prevUF11Stat;
            _prevUF11Stat = statUF11_Any[0];
            
            if (_device == null || _device.IsDisposed) return;
            var uf11 = await _device.ReadUF11Message(cancel).ConfigureAwait(false);
            
            UF11Message = string.Join("", uf11.Select(x => $"{x:X2}"));
                    
            var msgU11 = new ModeSUF11();
            var spanU11 = new ReadOnlySpan<byte>(uf11);
            msgU11.Deserialize(ref spanU11);
            UF11Icao = msgU11.IcaoAddress.ToString("X6");
            await _writer.WriteLineAsync($"\"{DateTime.UtcNow:u}\" UF4: 0x{UF11Message} 0x{UF11Icao}").ConfigureAwait(false);
        }
    }

    private async Task UpdateMessageDF11(CancellationToken cancel)
    {
        var buffDF11 = new byte[7];
        var msgD11 = new ModeSDF11(0, 0)
        {
            IcaoAddress = MyIcao,
            Capability = CapabilityEnum.Level1,
        };
        var spanDF11 = new Span<byte>(buffDF11);
        msgD11.Serialize(ref spanDF11);
        DF11Message = string.Join("", buffDF11.Select(x => $"{x:X2}"));

        if (_device == null || _device.IsDisposed) return;
        await _device.WriteDF11Message(buffDF11, cancel).ConfigureAwait(false);
    }
    
    private async Task UpdateMessageAnyUF(CancellationToken cancel)
    {
        if (_device == null || _device.IsDisposed) return;
        var statUF11_Any = await _device.GetUF11AnyUFStat(cancel).ConfigureAwait(false);
        var needsToBeUpdatedAnyUF = _prevAnyUFStat != statUF11_Any[1];

        if (needsToBeUpdatedAnyUF)
        {
            AnyUFCnt += _prevAnyUFStat <= statUF11_Any[1] ? statUF11_Any[1] - _prevAnyUFStat : 256U + statUF11_Any[1] - _prevAnyUFStat;
            _prevAnyUFStat = statUF11_Any[1];
            
            if (_device == null || _device.IsDisposed) return;
            var anyUf = await _device.ReadAnyUFMessage(cancel).ConfigureAwait(false);
            
            AnyUFMessage = string.Join("", anyUf.Select(x => $"{x:X2}"));
               
            var spanU11 = new ReadOnlySpan<byte>(anyUf);
            var format = ModeSHelper.GetModeSFormat(spanU11);

            
            ModeSUFormatBase msgAnyUF;
            switch (format)
            {
                case 4:
                    msgAnyUF = new ModeSUF4();
                    break;
                case 5:
                    msgAnyUF = new ModeSUF5();
                    break;
                case 20:
                    msgAnyUF = new ModeSUF20();
                    break;
                case 21:
                    msgAnyUF = new ModeSUF21();
                    break;
                case 11:
                    msgAnyUF = new ModeSUF4();
                    break;
                default:
                    return;
            }
            
            msgAnyUF.Deserialize(ref spanU11);
            AnyUFIcao = msgAnyUF.IcaoAddress.ToString("X6");
            await _writer.WriteLineAsync($"\"{DateTime.UtcNow:u}\" UF{format}: 0x{AnyUFMessage} 0x{AnyUFIcao}").ConfigureAwait(false);
        }
    }

    private async Task UpdateMessageUF4(CancellationToken cancel)
    {
        if (_device == null || _device.IsDisposed) return;
        var statUF4_20 = await _device.GetUF4UF20Stat(cancel).ConfigureAwait(false);
        var needsToBeUpdatedUF4 = _prevUF4Stat != statUF4_20[0];

        if (needsToBeUpdatedUF4)
        {
            UF4Cnt += _prevUF4Stat <= statUF4_20[0] ? statUF4_20[0] - _prevUF4Stat : 256U + statUF4_20[0] - _prevUF4Stat;
            _prevUF4Stat = statUF4_20[0];
            
            if (_device == null || _device.IsDisposed) return;
            var uf4 = await _device.ReadUF4Message(cancel).ConfigureAwait(false);
            
            UF4Message = string.Join("", uf4.Select(x => $"{x:X2}"));
                    
            var msgU4 = new ModeSUF4();
            var spanU4 = new ReadOnlySpan<byte>(uf4);
            msgU4.Deserialize(ref spanU4);
            UF4Icao = msgU4.IcaoAddress.ToString("X6");
            await _writer.WriteLineAsync($"\"{DateTime.UtcNow:u}\" UF4: 0x{UF4Message} 0x{UF4Icao}").ConfigureAwait(false);
        }
    }

    private async Task UpdateMessageDF4(CancellationToken cancel)
    {
        var buffDF4 = new byte[7];
        var msgD4 = new ModeSDF4
        {
            IcaoAddress = MyIcao,
            Altitude = 45.72
        };
        var spanD4 = new Span<byte>(buffDF4);
        msgD4.Serialize(ref spanD4);
        DF4Message = string.Join("", buffDF4.Select(x => $"{x:X2}"));


        if (_device == null || _device.IsDisposed) return;
        await _device.WriteDF4Message(buffDF4, cancel).ConfigureAwait(false);
    }

    private async Task UpdateMessageUF5(CancellationToken cancel)
    {
        if (_device == null || _device.IsDisposed) return;
        var statUF5_21 = await _device.GetUF5UF21Stat(cancel).ConfigureAwait(false);
        var needsToBeUpdatedUF5 = _prevUF5Stat != statUF5_21[0];

        if (needsToBeUpdatedUF5)
        {
            UF5Cnt += _prevUF5Stat <= statUF5_21[0] ? statUF5_21[0] - _prevUF5Stat : 256U + statUF5_21[0] - _prevUF5Stat;
            _prevUF5Stat = statUF5_21[0];
            
            if (_device == null || _device.IsDisposed) return;
            var uf5 = await _device.ReadUF5Message(cancel).ConfigureAwait(false);
            
            UF5Message = string.Join("", uf5.Select(x => $"{x:X2}"));
                    
            var msgUF5 = new ModeSUF5();
            var spanUF5 = new ReadOnlySpan<byte>(uf5);
            msgUF5.Deserialize(ref spanUF5);
            UF5Icao = msgUF5.IcaoAddress.ToString("X6");
            await _writer.WriteLineAsync($"\"{DateTime.UtcNow:u}\" UF5: 0x{UF5Message} 0x{UF5Icao}").ConfigureAwait(false);
        }
    }

    private async Task UpdateMessageDF5(CancellationToken cancel)
    {
        var buffDF5 = new byte[7];
        var msgDF5 = new ModeSDF5
        {
            IcaoAddress = MyIcao,
            Squawk = "0777"
        };
        var spanD4 = new Span<byte>(buffDF5);
        msgDF5.Serialize(ref spanD4);
        DF5Message = string.Join("", buffDF5.Select(x => $"{x:X2}"));

        if (_device == null || _device.IsDisposed) return;
        await _device.WriteDF5Message(buffDF5, cancel).ConfigureAwait(false);
    }
    
    private async Task UpdateMessageUF20(CancellationToken cancel)
    {
        if (_device == null || _device.IsDisposed) return;
        var statUF4_20 = await _device.GetUF4UF20Stat(cancel).ConfigureAwait(false);
        var needsToBeUpdatedUF20 = _prevUF20Stat != statUF4_20[1];

        if (needsToBeUpdatedUF20)
        {
            UF20Cnt += _prevUF20Stat <= statUF4_20[1] ? statUF4_20[1] - _prevUF20Stat : 256U + statUF4_20[1] - _prevUF20Stat;
            _prevUF20Stat = statUF4_20[1];
            
            if (_device == null || _device.IsDisposed) return;
            var uf20 = await _device.ReadUF20Message(cancel).ConfigureAwait(false);
            
            UF20Message = string.Join("", uf20.Select(x => $"{x:X2}"));
                    
            var msgU20 = new ModeSUF20();
            var spanU20 = new ReadOnlySpan<byte>(uf20);
            msgU20.Deserialize(ref spanU20);
            UF20Icao = msgU20.IcaoAddress.ToString("X6");
            await _writer.WriteLineAsync($"\"{DateTime.UtcNow:u}\" UF20: 0x{UF20Message} 0x{UF20Icao}").ConfigureAwait(false);
        }
    }
    
    private async Task UpdateBDS10(CancellationToken cancel)
    {
        var buffBDS10 = new byte[7];
        
        var bds20 = new Bds10();
        var span20 = new Span<byte>(buffBDS10);
        bds20.Serialize(ref span20);
        BDS10Message = string.Join("", buffBDS10.Select(x => $"{x:X2}"));

        if (_device == null || _device.IsDisposed) return;
        await _device.WriteBDS10Message(buffBDS10, cancel).ConfigureAwait(false);
    }
    
    private async Task UpdateBDS20(CancellationToken cancel)
    {
        var buffBDS20 = new byte[7];
        
        var bds20 = new Bds20
        {
            AircraftIdentification = "CURSIR"
        };
        var span20 = new Span<byte>(buffBDS20);
        bds20.Serialize(ref span20);
        BDS20Message = string.Join("", buffBDS20.Select(x => $"{x:X2}"));

        if (_device == null || _device.IsDisposed) return;
        await _device.WriteBDS20Message(buffBDS20, cancel).ConfigureAwait(false);
    }
    
    private async Task UpdateBDS40(CancellationToken cancel)
    {
        var buffBDS40 = new byte[7];
        
        var bds40 = new BdsAny(4, 0);
        var span40 = new Span<byte>(buffBDS40);
        bds40.Serialize(ref span40);
        BDS40Message = string.Join("", buffBDS40.Select(x => $"{x:X2}"));

        if (_device == null || _device.IsDisposed) return;
        await _device.WriteBDS40Message(buffBDS40, cancel).ConfigureAwait(false);
    }
    
    private async Task UpdateBDS50(CancellationToken cancel)
    {
        var buffBDS50 = new byte[7];
        
        var bds50 = new BdsAny(5, 0);
        var span50 = new Span<byte>(buffBDS50);
        bds50.Serialize(ref span50);
        BDS50Message = string.Join("", buffBDS50.Select(x => $"{x:X2}"));

        if (_device == null || _device.IsDisposed) return;
        await _device.WriteBDS50Message(buffBDS50, cancel).ConfigureAwait(false);
    }
    
    private async Task UpdateBDS60(CancellationToken cancel)
    {
        var buffBDS60 = new byte[7];
        
        var bds60 = new BdsAny(6, 0);
        var span60 = new Span<byte>(buffBDS60);
        bds60.Serialize(ref span60);
        BDS60Message = string.Join("", buffBDS60.Select(x => $"{x:X2}"));

        if (_device == null || _device.IsDisposed) return;
        await _device.WriteBDS60Message(buffBDS60, cancel).ConfigureAwait(false);
    }
    
    private async Task UpdateMessageDF17Id(CancellationToken cancel)
    {
        var buffDF17 = new byte[14];
        if (OnOffDf17Id)
        {
            var msgD17 = new AdsbAircraftIdentification
            {
                AircraftAddress = (int)MyIcao,
                Capability = CapabilityEnum.Level1,
                AircraftCategory = AircraftCategoryEnum.Light,
                SquitterType = SquitterTypeEnum.WithTransponder,
                AircraftIdentification = "CURSIR"
                
            };
            var spanD17 = new Span<byte>(buffDF17);
            msgD17.Serialize(ref spanD17);
            DF17IdMessage = string.Join("", buffDF17.Select(x => $"{x:X2}"));
        }
        else
        {
            DF17IdMessage = string.Join("", buffDF17.Select(x => $"{x:X2}"));
        }

        if (_device == null || _device.IsDisposed) return;
        await _device.WriteDF17IdMessage(buffDF17, cancel).ConfigureAwait(false);
    }
    
    private async Task UpdateMessageDF17Position(CancellationToken cancel)
    {
        var buffDF17Even = new byte[14];
        var buffDF17Odd = new byte[14];
        // if (OnOffDf17Pos)
        // {
            var msgD17Even = new AdsbAirbornePositionWithBaroAlt
            {
                AircraftAddress = (int)MyIcao,
                Capability = CapabilityEnum.Level1,
                SquitterType = SquitterTypeEnum.WithTransponder,
                AirbornePositionType = AirbornePositionBaroAltTypeCode.BasicPositionHighUpdateRate, 
                Latitude = 55.300128,
                Longitude = 61.543065,
                Altitude = 45.72,
                IsSingleAntenna = true,
                CprFormat = CprFormatEnum.Even
            };
            
            var msgD17Odd = new AdsbAirbornePositionWithBaroAlt
            {
                AircraftAddress = (int)MyIcao,
                Capability = CapabilityEnum.Level1,
                SquitterType = SquitterTypeEnum.WithTransponder,
                AirbornePositionType = AirbornePositionBaroAltTypeCode.BasicPositionHighUpdateRate, 
                Latitude = 55.300128,
                Longitude = 61.543065,
                Altitude = 45.72,
                IsSingleAntenna = true,
                CprFormat = CprFormatEnum.Odd
            };
            
            var spanD17Even = new Span<byte>(buffDF17Even);
            msgD17Even.Serialize(ref spanD17Even);
            DF17PosEvenMessage = string.Join("", buffDF17Even.Select(x => $"{x:X2}"));
            
            var spanD17Odd = new Span<byte>(buffDF17Odd);
            msgD17Odd.Serialize(ref spanD17Odd);
            DF17PosOddMessage = string.Join("", buffDF17Odd.Select(x => $"{x:X2}"));
        // }
        // else
        // {
        //     DF17PosEvenMessage = string.Join("", buffDF17Even.Select(x => $"{x:X2}"));
        //     DF17PosOddMessage = string.Join("", buffDF17Odd.Select(x => $"{x:X2}"));
        // }

        if (_device == null || _device.IsDisposed) return;
        await _device.WriteDF17PositionMessage(buffDF17Even, buffDF17Odd, cancel).ConfigureAwait(false);
    }
    
    private async Task UpdateMessageDF17Velocity(CancellationToken cancel)
    {
        var buffDF17 = new byte[14];
        // if (OnOffDf17Vel)
        // {
            var msgD17 = new AdsbGroundSpeed
            {
                AircraftAddress = (int)MyIcao,
                Capability = CapabilityEnum.Level1,
                SquitterType = SquitterTypeEnum.WithTransponder,
                SubType = VelocitySubTypeEnum.SubType1,
                VrSrc = VerticalRateSourceEnum.Gnss,
                VerticalRate = 0.0,
                GnssBaroAltDiff = 0.0,
                GroundSpeed = 200.0,
                GroundTrackAngle = 270.0,
                NavigationUncertaintyCategory = NavigationUncertaintyCategoryEnum.AdsbVersion0
            };
            
            var spanD17 = new Span<byte>(buffDF17);
            msgD17.Serialize(ref spanD17);
            DF17VelMessage = string.Join("", buffDF17.Select(x => $"{x:X2}"));
        // }
        // else
        // {
        //     DF17VelMessage = string.Join("", buffDF17.Select(x => $"{x:X2}"));
        // }

        if (_device == null || _device.IsDisposed) return;
        await _device.WriteDF17VelocityMessage(buffDF17, cancel).ConfigureAwait(false);
    }

    #endregion


    private void UpdateUFMessage(long x)
    {
        if (Interlocked.CompareExchange(ref _readUfFlag, 1, 0) != 0) return;
        try
        {
            if (_device == null || _device.IsDisposed) return;
            PeakAmp = _device.AdsbGetPeakAmplitude().Result.ToString();

            if (_device == null || _device.IsDisposed) return;
            var allStat = _device.GetAllStat(CancellationToken.None).Result;

            var needsToBeUpdatedUF4 = _prevDF4Stat != allStat.DF4;
            var needsToBeUpdatedUF5 = needsToBeUpdatedUF4;
            // var needsToBeUpdatedUF5 = _prevDF5Stat != allStat.DF5;
            var needsToBeUpdatedUF20 = _prevDF20Stat != allStat.DF20;
            var needsToBeUpdatedUF21 = needsToBeUpdatedUF20;
            // var needsToBeUpdatedUF21 = _prevDF21Stat != allStat.DF21;
            var needsToBeUpdatedUF11 = _prevDF11Stat != allStat.DF11;

            UF4Cnt += _prevUF4Stat <= allStat.UF4 ? allStat.UF4 - _prevUF4Stat : 256U + allStat.UF4 - _prevUF4Stat;
            _prevUF4Stat = allStat.UF4;
            UF5Cnt += _prevUF5Stat <= allStat.UF5 ? allStat.UF5 - _prevUF5Stat : 256U + allStat.UF5 - _prevUF5Stat;
            _prevUF5Stat = allStat.UF5;
            UF20Cnt += _prevUF20Stat <= allStat.UF20 ? allStat.UF20 - _prevUF20Stat : 256U + allStat.UF20 - _prevUF20Stat;
            _prevUF20Stat = allStat.UF20;
            UF21Cnt += _prevUF21Stat <= allStat.UF21 ? allStat.UF21 - _prevUF21Stat : 256U + allStat.UF21 - _prevUF21Stat;
            _prevUF21Stat = allStat.UF21;
            UF11Cnt += _prevUF11Stat <= allStat.UF11 ? allStat.UF11 - _prevUF11Stat : 256U + allStat.UF11 - _prevUF11Stat;
            _prevUF11Stat = allStat.UF11;
            
            
            if (!needsToBeUpdatedUF4 && !needsToBeUpdatedUF5 && !needsToBeUpdatedUF20 && !needsToBeUpdatedUF21 &&
                !needsToBeUpdatedUF11) return;

            if (_device == null || _device.IsDisposed) return;
            var allMsg = _device.ReadAllUFMessage(CancellationToken.None).Result;

            if (needsToBeUpdatedUF4)
            {
                DF4Cnt += _prevDF4Stat <= allStat.DF4 ? allStat.DF4 - _prevDF4Stat : 256U + allStat.DF4 - _prevDF4Stat;
                _prevDF4Stat = allStat.DF4;

                UF4Message = string.Join("", allMsg.UF4.Select(m => $"{m:X2}"));
                var msgU4 = new ModeSUF4();
                var spanU4 = new ReadOnlySpan<byte>(allMsg.UF4);
                msgU4.Deserialize(ref spanU4);
                UF4Icao = msgU4.IcaoAddress.ToString("X6");
                _writer.WriteLine($"\"{DateTime.UtcNow:u}\" UF4: 0x{UF4Message} 0x{UF4Icao}");
            }

            if (needsToBeUpdatedUF5)
            {
                DF5Cnt += _prevDF5Stat <= allStat.DF5 ? allStat.DF5 - _prevDF5Stat : 256U + allStat.DF5 - _prevDF5Stat;
                _prevDF5Stat = allStat.DF5;

                UF5Message = string.Join("", allMsg.UF5.Select(m => $"{m:X2}"));
                var msgU5 = new ModeSUF5();
                var spanU5 = new ReadOnlySpan<byte>(allMsg.UF5);
                msgU5.Deserialize(ref spanU5);
                UF5Icao = msgU5.IcaoAddress.ToString("X6");
                _writer.WriteLine($"\"{DateTime.UtcNow:u}\" UF5: 0x{UF5Message} 0x{UF5Icao}");
            }

            if (needsToBeUpdatedUF20)
            {
                _DF20Cnt += _prevDF20Stat <= allStat.DF20
                    ? allStat.DF20 - _prevDF20Stat
                    : 256U + allStat.DF20 - _prevDF20Stat;
                _prevDF20Stat = allStat.DF20;
                BDSCnt = _DF20Cnt;
                
                UF20Message = string.Join("", allMsg.UF20.Select(m => $"{m:X2}"));
                var msgU20 = new ModeSUF20();
                var spanU20 = new ReadOnlySpan<byte>(allMsg.UF20);
                msgU20.Deserialize(ref spanU20);
                UF20Icao = msgU20.IcaoAddress.ToString("X6");
                _writer.WriteLine($"\"{DateTime.UtcNow:u}\" UF20: 0x{UF20Message} 0x{UF20Icao}");
            }

            if (needsToBeUpdatedUF21)
            {
                _DF21Cnt += _prevDF21Stat <= allStat.DF21
                    ? allStat.DF21 - _prevDF21Stat
                    : 256U + allStat.DF21 - _prevDF21Stat;
                _prevDF21Stat = allStat.DF21;

                UF21Message = string.Join("", allMsg.UF21.Select(m => $"{m:X2}"));
                var msgU21 = new ModeSUF21();
                var spanU21 = new ReadOnlySpan<byte>(allMsg.UF21);
                msgU21.Deserialize(ref spanU21);
                UF21Icao = msgU21.IcaoAddress.ToString("X6");
                _writer.WriteLine($"\"{DateTime.UtcNow:u}\" UF21: 0x{UF21Message} 0x{UF21Icao}");
            }

            if (needsToBeUpdatedUF11)
            {
                DF11Cnt += _prevDF11Stat <= allStat.DF11
                    ? allStat.DF11 - _prevDF11Stat
                    : 256U + allStat.DF11 - _prevDF11Stat;
                _prevDF11Stat = allStat.DF11;

                UF11Message = string.Join("", allMsg.UF11.Select(m => $"{m:X2}"));
                var msgU11 = new ModeSUF11();
                var spanU11 = new ReadOnlySpan<byte>(allMsg.UF11);
                msgU11.Deserialize(ref spanU11);
                UF11Icao = msgU11.IcaoAddress.ToString("X6");
                _writer.WriteLine($"\"{DateTime.UtcNow:u}\" UF11: 0x{UF11Message} 0x{UF11Icao}");
            }

            _writer?.Flush();
            _strem?.Flush();

            // UpdateMessageUF4(CancellationToken.None).Wait();
            // UpdateMessageUF5(CancellationToken.None).Wait();
            // UpdateMessageUF20(CancellationToken.None).Wait();
            // UpdateMessageUF21(CancellationToken.None).Wait();
            // UpdateMessageUF11(CancellationToken.None).Wait();
            // UpdateMessageAnyUF(CancellationToken.None).Wait();
            // UpdateMessageDFCnt(CancellationToken.None).Wait();
        }
        finally
        {
            Interlocked.Exchange(ref _readUfFlag, 0);
        }


    }


    private class MyClass
    {
        public string rawMessage { get; set; }
    }
    private async void ConnectLmsImpl()
    {
        // var stream = new FileStream("C:\\Users\\lobanov\\Documents\\МПСН\\AllUFMessages.txt", FileMode.Open, FileAccess.Read);
        // var stream = new FileStream("C:\\Users\\lobanov\\Install\\lime_adsb\\20170109_16_anonymized.avro\\msr804_20160519.json", FileMode.Open, FileAccess.Read);
        // var reader = new StreamReader(stream);
        //
        // var df4Msg = new List<ModeSDF4>();
        // var df5Msg = new List<ModeSDF5>();
        // var df20Msg = new List<ModeSDF20>();
        // var df21Msg = new List<ModeSDF21>();
        //
        // while (!reader.EndOfStream)
        // {
        //     try
        //     {
        //         
        //         var m = JsonConvert.DeserializeObject<MyClass>(await reader.ReadLineAsync() ?? string.Empty);
        //         if (m == null || string.IsNullOrEmpty(m.rawMessage)) continue;
        //         var data = new byte[m.rawMessage.Length / 2];
        //         for (var i = 0; i < m.rawMessage.Length / 2; i++)
        //         {
        //             data[i] = byte.Parse(m.rawMessage.AsSpan(2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        //         }
        //         var span = new ReadOnlySpan<byte>(data);
        //         var msgNum = data[0] >> 3;
        //         switch (msgNum)
        //         {
        //             case 4:
        //                 var df4 = new ModeSDF4();
        //                 df4.Deserialize(ref span);
        //                 df4Msg.Add(df4);
        //                 break;
        //             case 5:
        //                 var df5 = new ModeSDF5();
        //                 df5.Deserialize(ref span);
        //                 df5Msg.Add(df5);
        //                 break;
        //             case 20:
        //                 var df20 = new ModeSDF20();
        //                 df20.Deserialize(ref span);
        //                 if (df20.BDS.Bds1 is 0 or 1 or 2 or 3 or 4 or 5 or 6)
        //                     df20Msg.Add(df20);
        //                 break;
        //             case 21:
        //                 var df21 = new ModeSDF21();
        //                 df21.Deserialize(ref span);
        //                 if (df21.BDS.Bds1 is 0 or 1 or 2 or 3 or 4 or 5 or 6)
        //                     df21Msg.Add(df21);
        //                 break;
        //         }
        //     }
        //     catch (Exception e)
        //     {
        //         
        //     }
        //     
        // }
        //
        //
        // var uf4Msg = new List<ModeSUF4>();
        // var uf5Msg = new List<ModeSUF5>();
        //
        // while (!reader.EndOfStream)
        // {
        //     var line = (await reader.ReadLineAsync()).Split(" ");
        //     var type = line[2];
        //     var str = line[3][2..];
        //     var data = new byte[7];
        //     for (var i = 0; i < 7; i++)
        //     {
        //         data[i] = byte.Parse(str.AsSpan(2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        //     }
        //     var span = new ReadOnlySpan<byte>(data);
        //     if (string.Equals(type, "UF4:"))
        //     {
        //         var uf4 = new ModeSUF4();
        //         uf4.Deserialize(ref span);
        //         uf4Msg.Add(uf4);
        //     }
        //     if (string.Equals(type, "UF5:"))
        //     {
        //         var uf5 = new ModeSUF5();
        //         uf5.Deserialize(ref span);
        //         uf5Msg.Add(uf5);
        //     }
        // }
        //
        //
        //
        // var ff4 = uf4Msg.GroupBy(uf4 => uf4.SD);
        // var ff5 = uf5Msg.GroupBy(uf5 => uf5.SD);
        
        
        
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
        
        try
        {
            _logger.ZLogInformation($"LimeSuite API version: {LimeSdrDevice.GetApiVersion()}");
            DisconnectLmsImpl();
            
            UF4Cnt = _prevUF4Stat = 0;
            UF5Cnt = _prevUF5Stat = 0;
            UF20Cnt = _prevUF20Stat = 0;
            UF21Cnt = _prevUF21Stat = 0;
            UF11Cnt = _prevUF11Stat = 0;
            AnyUFCnt = _prevAnyUFStat = 0;
            
            DF4Cnt = _prevDF4Stat = 0;
            DF5Cnt = _prevDF5Stat = 0;
            BDSCnt = _DF20Cnt = _prevDF20Stat = 0;
            _DF21Cnt = _prevDF21Stat = 0;
            DF11Cnt = _prevDF11Stat = 0;
            
            var dev = !string.IsNullOrWhiteSpace(_cfg.SerialNumber)
                ? LimeSdrDevice.GetAvailableDevices().FirstOrDefault(id => id.Contains(_cfg.SerialNumber))
                : LimeSdrDevice.GetAvailableDevices().FirstOrDefault();
            if (dev == null) throw new Exception("LMS device not found");
            
            _logger.ZLogInformation($"Create LMS device {dev}");
            var device = new LimeSdrAdsbRepDevice(dev, new LimeSdrAdsbRepDeviceConfig(), _logger);
            
            // ADS-B
            await device.EnableChannel(LmsChannel.Rx, 0, true, DisposeCancel).ConfigureAwait(false);
            await device.EnableChannel(LmsChannel.Tx, 0, true, DisposeCancel).ConfigureAwait(false);
            await device.SetSampleRate(40_000_000, 1U, DisposeCancel).ConfigureAwait(false);
            await device.SetFrequency(LmsChannel.Rx, 0, 1_030_000_000, DisposeCancel).ConfigureAwait(false);
            await device.SetFrequency(LmsChannel.Tx, 0, 1_090_000_000, DisposeCancel).ConfigureAwait(false);
            await device.SetNormalizedGain(LmsChannel.Rx, 0, _cfg.RxGain, DisposeCancel).ConfigureAwait(false);
            await device.SetNormalizedGain(LmsChannel.Tx, 0, _cfg.TxGain, DisposeCancel).ConfigureAwait(false);
            await device.SetAntenna(LmsChannel.Rx, 0, (uint) LmsPathRx.LMS_PATH_LNAW, DisposeCancel).ConfigureAwait(false);
            await device.SetAntenna(LmsChannel.Tx, 0, (uint) LmsPathTx.LMS_PATH_TX1, DisposeCancel).ConfigureAwait(false);
            await device.AdsbSetIsEnabled(true, DisposeCancel).ConfigureAwait(false);
            
            _device = device;
            await device.SetDf11ReplyIsEnabled(OnOffDf11Reply, DisposeCancel).ConfigureAwait(false);
            await device.SetDf11BroadcastIsEnabled(OnOffDf11Squitter, CancellationToken.None).ConfigureAwait(false);
            await device.SetDf4IsEnabled(OnOffDf4Reply, CancellationToken.None).ConfigureAwait(false);
            await device.SetDf5IsEnabled(OnOffDf5Reply, CancellationToken.None).ConfigureAwait(false);
            await device.SetDf20IsEnabled(OnOffDf20Reply, CancellationToken.None).ConfigureAwait(false);
            await device.SetDf21IsEnabled(OnOffDf21Reply, CancellationToken.None).ConfigureAwait(false);
            await device.SetDf17IdIsEnabled(OnOffDf17Id, CancellationToken.None).ConfigureAwait(false);
            await device.SetDf17PositionIsEnabled(OnOffDf17Pos, CancellationToken.None).ConfigureAwait(false);
            await device.SetDf17VelocityIsEnabled(OnOffDf17Vel, CancellationToken.None).ConfigureAwait(false);
            
            await device.SetAnyUFType(AnyUFType, CancellationToken.None).ConfigureAwait(false);
            await device.SetDfDelay(DfDelay).ConfigureAwait(false);

            await UpdateMessageDF4(CancellationToken.None).ConfigureAwait(false);
            await UpdateMessageDF5(CancellationToken.None).ConfigureAwait(false);
            await UpdateBDS10(CancellationToken.None).ConfigureAwait(false);
            await UpdateBDS20(CancellationToken.None).ConfigureAwait(false);
            await UpdateBDS40(CancellationToken.None).ConfigureAwait(false);
            await UpdateBDS50(CancellationToken.None).ConfigureAwait(false);
            await UpdateBDS60(CancellationToken.None).ConfigureAwait(false);
            await UpdateMessageDF11(CancellationToken.None).ConfigureAwait(false);
            
            _strem = new FileStream("AllUFMessages.txt", FileMode.Append);
            _writer = new StreamWriter(_strem);

        }
        catch (Exception e)
        {
            
        }
    }

    private void DisconnectLmsImpl()
    {
        try
        {
            // _device?.AdsbSetDf17IsEnabled(false, CancellationToken.None);
            _device?.AdsbSetIsEnabled(false,CancellationToken.None).Wait();
            _device?.Dispose();
            _device = null;
            
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
            _strem?.Flush();
            _strem?.Dispose();
            _strem = null;

            
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    
    
    public ReactiveCommand<Unit,Unit> ConnectLms { get; set; }
    
    public ReactiveCommand<Unit,Unit> DisconnectLms { get; set; }
    
    public ReactiveCommand<Unit,Unit> ApplyIcao { get; set; }
    
    public ReactiveCommand<Unit,Unit> ApplyUFType { get; set; }
    
    [Reactive] public double TxGain { get; set; } = 0.0;
    
    [Reactive] public string TxGainStr { get; private set; } = 0.0.ToString("F2", CultureInfo.InvariantCulture);
    [Reactive] public double RxGain { get; set; } = 0.0;
    [Reactive] public string RxGainStr { get; private set; } = 0.0.ToString("F2", CultureInfo.InvariantCulture);
    [Reactive] public string UF4Message { get; set; } = string.Empty;
    [Reactive] public string DF4Message { get; set; } = string.Empty;
    [Reactive] public string UF5Message { get; set; } = string.Empty;
    [Reactive] public string DF5Message { get; set; } = string.Empty;
    [Reactive] public string UF20Message { get; set; } = string.Empty;
    
    [Reactive] public string BDS10Message { get; set; } = string.Empty;
    [Reactive] public string BDS20Message { get; set; } = string.Empty;
    [Reactive] public string BDS40Message { get; set; } = string.Empty;
    [Reactive] public string BDS50Message { get; set; } = string.Empty;
    [Reactive] public string BDS60Message { get; set; } = string.Empty;
    [Reactive] public string UF21Message { get; set; } = string.Empty;
    [Reactive] public string UF11Message { get; set; } = string.Empty;
    [Reactive] public string DF11Message { get; set; } = string.Empty;
    [Reactive] public string DF17IdMessage { get; set; } = string.Empty;
    [Reactive] public string DF17PosEvenMessage { get; set; } = string.Empty;
    [Reactive] public string DF17PosOddMessage { get; set; } = string.Empty;
    [Reactive] public string DF17VelMessage { get; set; } = string.Empty;
    [Reactive] public byte AnyUFType { get; set; } = 4;
    [Reactive] public string AnyUFTypeStr { get; set; } = 4.ToString();
    [Reactive] public string AnyUFMessage { get; set; } = string.Empty;

    [Reactive] public uint MyIcao { get; set; } = IcaoAddress;
    [Reactive] public string MyIcaoStr { get; set; } = IcaoAddress.ToString("X6"); 
    [Reactive] public string UF11Icao { get; set; } = "Unknown";
    [Reactive] public bool OnOffDf4Reply { get; set; }
    [Reactive] public bool OnOffDf5Reply { get; set; }
    [Reactive] public bool OnOffDf20Reply { get; set; }
    [Reactive] public bool OnOffDf21Reply { get; set; }
    [Reactive] public bool OnOffDf17Id { get; set; }
    [Reactive] public bool OnOffDf17Pos { get; set; }
    [Reactive] public bool OnOffDf17Vel { get; set; }
    [Reactive] public bool OnOffDf11Reply { get; set; }
    [Reactive] public bool OnOffDf11Squitter { get; set; }
    
    
    [Reactive] public byte PR { get; set; }
    [Reactive] public byte IC { get; set; }
    [Reactive] public byte CL { get; set; }
    [Reactive] public string PeakAmp { get; set; } = string.Empty;
    [Reactive] public double DfDelay { get; set; }
    [Reactive] public string DfDelayStr { get; private set; } = 0.0.ToString("F2", CultureInfo.InvariantCulture);
    [Reactive] public ulong UF4Cnt { get; set; }
    [Reactive] public ulong UF5Cnt { get; set; }
    [Reactive] public ulong UF20Cnt { get; set; }
    [Reactive] public ulong UF21Cnt { get; set; }
    [Reactive] public ulong UF11Cnt { get; set; }
    [Reactive] public ulong AnyUFCnt { get; set; }
    [Reactive] public ulong DF4Cnt { get; set; }
    [Reactive] public ulong DF5Cnt { get; set; }
    [Reactive] public ulong BDSCnt { get; set; }
    [Reactive] public ulong DF11Cnt { get; set; }
    [Reactive] public string UF4Icao { get; set; }
    [Reactive] public string UF5Icao { get; set; }
    [Reactive] public string UF20Icao { get; set; }
    [Reactive] public string UF21Icao { get; set; }
    [Reactive] public string AnyUFIcao { get; set; }


    public void InitCharts(AvaPlot avaPlotLms, AvaPlot avaPlotSh)
    {
        _avaPlotLms = avaPlotLms;
        _avaPlotSh = avaPlotSh;
    }
}



