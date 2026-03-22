using System;
using System.Composition;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using Asv.Cfg;
using Asv.Common;
using Asv.IO;
using Asv.Sdr.LimeSdr;
using Material.Icons;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ZLogger;

namespace Asv.Sdr.Gui;

public class AdsbNonTransponderConfig 
{
    public ulong Frequency { get; set; } = 1090000000;
    public ulong FrequencyOffset { get; set; } = 35000;
    public ulong SampleRate { get; set; } = 40_000_000;
    public LmsPathTx LmsPathTx { get; set; } = LmsPathTx.LMS_PATH_TX1;

    public double Amplitude { get; set; } = 1.0;
    
    public double Gain { get; set; } = 0.69;
    
    public string SerialNumber { get; set; } = string.Empty;
}

[Export(typeof(IShellPage))]
public class AdsbNonTransponderViewModel:ShellPage
{
    private const int IcaoAddress = 0x150777; //0x151F1A;
    private CancellationTokenSource? _cancelStream;
    private ILimeSdrDevice? _device;
    private ILmsStream _txStream;
    private readonly AdsbNonTransponderConfig _cfg;
    private ScottDebugPlot _dataDebug;
    private bool _flag = false;
    private readonly ILogger<AdsbNonTransponderViewModel> _logger;
    private readonly double[] _zeroBuffer = new double[10_000];
    private double[] _bitsId; //airborne
    private double[] _bitsAirbornePosEv;
    private double[] _bitsAirbornePosOdd;
    private double[] _bitsSurfacePosEv;
    private double[] _bitsSurfacePosOdd;
    private double[] _bitsVelOverGround;
    private double[] _bitsVelAirSpeed;
    private ulong _msgIdx;
    private DateTime _lastTime;

    public AdsbNonTransponderViewModel() : base(WellKnownUri.Shell + ".adsbNonTransp")
    {
        Title = "ADS-B Non Transponder";
        Icon = MaterialIconKind.ChartFinance;
        ConnectLms = ReactiveCommand.CreateRunInBackground(ConnectLmsImpl);
        DisconnectLms = ReactiveCommand.CreateRunInBackground(DisconnectLmsImpl);
    }

    
    [ImportingConstructor]
    public AdsbNonTransponderViewModel(IConfiguration cfg, ILoggerFactory loggerFactory):this()
    {
        _logger = loggerFactory.CreateLogger<AdsbNonTransponderViewModel>();
        _cfg = cfg.Get<AdsbNonTransponderConfig>();
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
                _device?.SetNormalizedGain(LmsChannel.Tx, 0, x, CancellationToken.None).Wait();
                _cfg.Gain = x;
                cfg.Set(_cfg);

            }).DisposeItWith(Disposable);
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
            
            var preamble = new double[] { 1, 0, 1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0 };
            var point = new GeoPoint(55.3001280, 61.5430650, 202.720);
            var df17IdBuff = new byte[14];
            var df17Id = new AdsbAircraftIdentification
            {
                AircraftAddress = IcaoAddress,
                Capability = CapabilityEnum.Level2OnGroundOrAirborne,
                SquitterType = SquitterTypeEnum.WithTransponder,
                AircraftIdentification = "CURSIR",
                AircraftCategory = AircraftCategoryEnum.Large
            };
            var df17IdSpan = new Span<byte>(df17IdBuff);
            df17Id.Serialize(ref df17IdSpan);

            AdsbExtendedSquitterBase airbornePosEven = new AdsbAirbornePositionWithBaroAlt
            {
                AircraftAddress = IcaoAddress,
                Capability = CapabilityEnum.Level2OnGroundOrAirborne,
                SquitterType = SquitterTypeEnum.WithTransponder,
                AirbornePositionType = AirbornePositionBaroAltTypeCode.BasicPositionHighUpdateRate,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                Altitude = point.Altitude,
                IsSingleAntenna = true,
                CprFormat = CprFormatEnum.Even
            };
            AdsbExtendedSquitterBase airbornePosOdd = new AdsbAirbornePositionWithBaroAlt
            {
                AircraftAddress = IcaoAddress,
                Capability = CapabilityEnum.Level2OnGroundOrAirborne,
                SquitterType = SquitterTypeEnum.WithTransponder,
                AirbornePositionType = AirbornePositionBaroAltTypeCode.BasicPositionHighUpdateRate,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                Altitude = point.Altitude,
                IsSingleAntenna = true,
                CprFormat = CprFormatEnum.Odd
            };
            
            AdsbExtendedSquitterBase surfacePosEven = new AdsbSurfacePosition
            {
                AircraftAddress = IcaoAddress,
                Capability = CapabilityEnum.Level2OnGroundOrAirborne,
                SquitterType = SquitterTypeEnum.WithTransponder, 
                Movement = 15,
                GroundTrackStatus = GroundTrackStatusEnum.Valid, 
                GroundTrack = 175,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                CprFormat = CprFormatEnum.Even
            };
            
            AdsbExtendedSquitterBase surfacePosOdd = new AdsbSurfacePosition
            {
                AircraftAddress = IcaoAddress,
                Capability = CapabilityEnum.Level2OnGroundOrAirborne,
                SquitterType = SquitterTypeEnum.WithTransponder,
                Movement = 15,
                GroundTrackStatus = GroundTrackStatusEnum.Valid, 
                GroundTrack = 175,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                CprFormat = CprFormatEnum.Odd
            };

            var velocityOverGround = new AdsbGroundSpeed
            {
                AircraftAddress = IcaoAddress,
                Capability = CapabilityEnum.Level2OnGroundOrAirborne,
                SquitterType = SquitterTypeEnum.WithTransponder,
                SubType = VelocitySubTypeEnum.SubType1,
                GroundSpeed = 15,
                GroundTrackAngle = 270,
                VrSrc = VerticalRateSourceEnum.Gnss,
                GnssBaroAltDiff = 0,
                VerticalRate = 0,
                NavigationUncertaintyCategory = NavigationUncertaintyCategoryEnum.AdsbVersion0
            };
            
            var velocityAirSpeed = new AdsbGroundSpeed
            {
                AircraftAddress = IcaoAddress,
                Capability = CapabilityEnum.Level2OnGroundOrAirborne,
                SquitterType = SquitterTypeEnum.WithTransponder,
                SubType = VelocitySubTypeEnum.SubType3,
                GroundSpeed = 15,
                GroundTrackAngle = 270,
                VrSrc = VerticalRateSourceEnum.Gnss,
                GnssBaroAltDiff = 0,
                VerticalRate = 0,
                NavigationUncertaintyCategory = NavigationUncertaintyCategoryEnum.AdsbVersion0
            };
            
            var airbornePosEvenBuff = new byte[14];
            var airbornePosOddBuff = new byte[14];
            var surfacePosEvenBuff = new byte[14];
            var surfacePosOddBuff = new byte[14];
            var velocityOverGroundBuff = new byte[14];
            var velocityAirSpeedBuff = new byte[14];
            var airbornePosEvenSpan = new Span<byte>(airbornePosEvenBuff);
            var airbornePosOddSpan = new Span<byte>(airbornePosOddBuff);
            var surfacePosEvenSpan = new Span<byte>(surfacePosEvenBuff);
            var surfacePosOddSpan = new Span<byte>(surfacePosOddBuff);
            var velocityOverGroundSpan = new Span<byte>(velocityOverGroundBuff);
            var velocityAirSpeedSpan = new Span<byte>(velocityAirSpeedBuff);
            airbornePosEven.Serialize(ref airbornePosEvenSpan);
            airbornePosOdd.Serialize(ref airbornePosOddSpan);
            surfacePosEven.Serialize(ref surfacePosEvenSpan);
            surfacePosOdd.Serialize(ref surfacePosOddSpan);
            velocityOverGround.Serialize(ref velocityOverGroundSpan);
            velocityAirSpeed.Serialize(ref velocityAirSpeedSpan);
            
            _bitsId = new double[500];
            _bitsAirbornePosEv = new double[500];
            _bitsAirbornePosOdd = new double[500];
            _bitsSurfacePosEv = new double[500];
            _bitsSurfacePosOdd = new double[500];
            _bitsVelOverGround = new double[500];
            _bitsVelAirSpeed = new double[500];
            
            for (var i = 0; i < preamble.Length; i++)
            {
                _bitsId[i] = preamble[i];
                _bitsAirbornePosEv[i] = preamble[i];
                _bitsAirbornePosOdd[i] = preamble[i];
                _bitsSurfacePosEv[i] = preamble[i];
                _bitsSurfacePosOdd[i] = preamble[i];
                _bitsVelOverGround[i] = preamble[i];
                _bitsVelAirSpeed[i] = preamble[i];
            }
            for (uint i = 0; i < df17IdBuff.Length*8; i++)
            {
                if (BitHelper.GetBitU(df17IdBuff, i, 1) > 0)
                {
                    _bitsId[i * 2 + preamble.Length] = 1;
                    _bitsId[i * 2 + preamble.Length + 1] = 0;
                }
                else
                {
                    _bitsId[i * 2 + preamble.Length] = 0;
                    _bitsId[i * 2 + preamble.Length + 1] = 1;
                }
                
                if (BitHelper.GetBitU(airbornePosEvenBuff, i, 1) > 0)
                {
                    _bitsAirbornePosEv[i * 2 + preamble.Length] = 1;
                    _bitsAirbornePosEv[i * 2 + preamble.Length + 1] = 0;
                }
                else
                {
                    _bitsAirbornePosEv[i * 2 + preamble.Length] = 0;
                    _bitsAirbornePosEv[i * 2 + preamble.Length + 1] = 1;
                }
                
                if (BitHelper.GetBitU(airbornePosOddBuff, i, 1) > 0)
                {
                    _bitsAirbornePosOdd[i * 2 + preamble.Length] = 1;
                    _bitsAirbornePosOdd[i * 2 + preamble.Length + 1] = 0;
                }
                else
                {
                    _bitsAirbornePosOdd[i * 2 + preamble.Length] = 0;
                    _bitsAirbornePosOdd[i * 2 + preamble.Length + 1] = 1;
                }
                
                if (BitHelper.GetBitU(surfacePosEvenBuff, i, 1) > 0)
                {
                    _bitsSurfacePosEv[i * 2 + preamble.Length] = 1;
                    _bitsSurfacePosEv[i * 2 + preamble.Length + 1] = 0;
                }
                else
                {
                    _bitsSurfacePosEv[i * 2 + preamble.Length] = 0;
                    _bitsSurfacePosEv[i * 2 + preamble.Length + 1] = 1;
                }
                
                if (BitHelper.GetBitU(surfacePosOddBuff, i, 1) > 0)
                {
                    _bitsSurfacePosOdd[i * 2 + preamble.Length] = 1;
                    _bitsSurfacePosOdd[i * 2 + preamble.Length + 1] = 0;
                }
                else
                {
                    _bitsSurfacePosOdd[i * 2 + preamble.Length] = 0;
                    _bitsSurfacePosOdd[i * 2 + preamble.Length + 1] = 1;
                }
                
                if (BitHelper.GetBitU(velocityOverGroundBuff, i, 1) > 0)
                {
                    _bitsVelOverGround[i * 2 + preamble.Length] = 1;
                    _bitsVelOverGround[i * 2 + preamble.Length + 1] = 0;
                }
                else
                {
                    _bitsVelOverGround[i * 2 + preamble.Length] = 0;
                    _bitsVelOverGround[i * 2 + preamble.Length + 1] = 1;
                }
                
                if (BitHelper.GetBitU(velocityAirSpeedBuff, i, 1) > 0)
                {
                    _bitsVelAirSpeed[i * 2 + preamble.Length] = 1;
                    _bitsVelAirSpeed[i * 2 + preamble.Length + 1] = 0;
                }
                else
                {
                    _bitsVelAirSpeed[i * 2 + preamble.Length] = 0;
                    _bitsVelAirSpeed[i * 2 + preamble.Length + 1] = 1;
                }
                
            }
            
            
            
            _txStream = await _device.CreateStream(LmsChannel.Tx, 0, 10_000,
                throughputVsLatency: 1.0f, cancel: _cancelStream!.Token).DisposeItWith(Disposable);
            await _txStream.Start(_cancelStream.Token);

            _lastTime = DateTime.Now;
            Observable.Timer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.5)).Subscribe(l => SendMessages())
                .DisposeItWith(Disposable);


        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
        }
    }

    private void SendMessages()
    {
        Console.WriteLine((DateTime.Now - _lastTime).TotalMilliseconds);
        _lastTime = DateTime.Now;
        _device.SetAntenna(LmsChannel.Tx, 0, (uint)LmsPathTx.LMS_PATH_TX1, CancellationToken.None).Wait();
        var bitLength = (int)(40_000_000 / 2e6);
        var bufferSize = _bitsId.Length * bitLength * 2;
        var buffer = new float[bufferSize];
        var bufferMemory = new ReadOnlyMemory<float>(buffer, 0, bufferSize);
        
        
        if (_msgIdx % 10 == 0)
        {
            for (var i = 0; i < _bitsId.Length; i++)
            {
                for (var j = 0; j < bitLength; j++)
                {
                    buffer[i*2 * bitLength + j] = (float)_bitsId[i];
                    buffer[i*2 * bitLength + j + 1] = (float)_bitsId[i];
                }
            }
            _txStream.Write(bufferMemory, 10_000, CancellationToken.None).Wait();
            SendZero(bufferMemory, buffer, bitLength);
        }
        
        if (_msgIdx % 2 == 0) // Even
        {
            for (var i = 0; i < _bitsAirbornePosEv.Length; i++)
            {
                for (var j = 0; j < bitLength; j++)
                {
                    buffer[i*2 * bitLength + j] = (float)_bitsAirbornePosEv[i];
                    buffer[i*2 * bitLength + j + 1] = (float)_bitsAirbornePosEv[i];
                }
            }
        }
        else // Odd
        {
            for (var i = 0; i < _bitsAirbornePosOdd.Length; i++)
            {
                for (var j = 0; j < bitLength; j++)
                {
                    buffer[i*2 * bitLength + j] = (float)_bitsAirbornePosOdd[i];
                    buffer[i*2 * bitLength + j + 1] = (float)_bitsAirbornePosOdd[i];
                }
            }
        }
        _txStream.Write(bufferMemory, 10_000, CancellationToken.None).Wait();
        SendZero(bufferMemory, buffer, bitLength);

        if (_msgIdx % 2 == 0) // Even
        {
            for (var i = 0; i < _bitsSurfacePosEv.Length; i++)
            {
                for (var j = 0; j < bitLength; j++)
                {
                    buffer[i*2 * bitLength + j] = (float)_bitsSurfacePosEv[i];
                    buffer[i*2 * bitLength + j + 1] = (float)_bitsSurfacePosEv[i];
                }
            }
        }
        else // Odd
        {
            for (var i = 0; i < _bitsSurfacePosOdd.Length; i++)
            {
                for (var j = 0; j < bitLength; j++)
                {
                    buffer[i*2 * bitLength + j] = (float)_bitsSurfacePosOdd[i];
                    buffer[i*2 * bitLength + j + 1] = (float)_bitsSurfacePosOdd[i];
                }
            }
        }
        _txStream.Write(bufferMemory, 10_000, CancellationToken.None).Wait();
        SendZero(bufferMemory, buffer, bitLength);
        
        for (var i = 0; i < _bitsVelOverGround.Length; i++)
        {
            for (var j = 0; j < bitLength; j++)
            {
                buffer[i*2 * bitLength + j] = (float)_bitsVelOverGround[i];
                buffer[i*2 * bitLength + j + 1] = (float)_bitsVelOverGround[i];
            }
        }
        _txStream.Write(bufferMemory, 10_000, CancellationToken.None).Wait();
        SendZero(bufferMemory, buffer, bitLength);
        
        for (var i = 0; i < _bitsVelAirSpeed.Length; i++)
        {
            for (var j = 0; j < bitLength; j++)
            {
                buffer[i*2 * bitLength + j] = (float)_bitsVelAirSpeed[i];
                buffer[i*2 * bitLength + j + 1] = (float)_bitsVelAirSpeed[i];
            }
        }
        _txStream.Write(bufferMemory, 10_000, CancellationToken.None).Wait();
        SendZero(bufferMemory, buffer, bitLength);
        
        
        _msgIdx++;
        _device.SetAntenna(LmsChannel.Tx, 0, 0, CancellationToken.None).Wait();

    }

    private void SendZero(ReadOnlyMemory<float> bufferMemory, float[] buffer, int bitLength)
    {
        for (var i = 0; i < 500; i++)
        {
            for (var j = 0; j < bitLength; j++)
            {
                buffer[i*2 * bitLength + j] = (float)_zeroBuffer[i];
                buffer[i*2 * bitLength + j + 1] = (float)_zeroBuffer[i];
            }
        }
        _txStream.Write(bufferMemory, 10_000, CancellationToken.None).Wait();
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

    public ReactiveCommand<Unit,Unit> ConnectLms { get; set; }
    public ReactiveCommand<Unit,Unit> DisconnectLms { get; set; } 
    
    
    [Reactive] public double TxGain { get; set; } = 0.0;
}

