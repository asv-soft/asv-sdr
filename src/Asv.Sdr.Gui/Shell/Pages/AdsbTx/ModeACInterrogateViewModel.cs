using System;
using System.Composition;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
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



public class ModeACInterrogateConfig 
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
public class ModeACInterrogateViewModel : ShellPage
{
    private AvaPlot _avaPlotLms;
    private AvaPlot _avaPlotSh;
    private CancellationTokenSource? _cancelStream;
    private ILimeSdrDevice? _device;
    private ILmsStream _txStream;
    private readonly ModeACInterrogateConfig _cfg;
    private readonly ILogger<ModeACInterrogateViewModel> _logger;
    private ScottDebugPlot _dataDebug;
    private bool _flag = false;
    private int _p2 = 0;
    private int _msgIdx = 0;

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

        byte[] bds17Buff1 = [0xa0, 0x00, 0x10, 0xb8, 0x02, 0x81, 0x01, 0x00, 0x00, 0x00, 0x00, 0xb2, 0xb5, 0xdd];
        var bds17Span1 = new ReadOnlySpan<byte>(bds17Buff1);
        byte[] bds17Buff2 = [0xa0, 0x00, 0x04, 0x14, 0x9f, 0x44, 0x02, 0xc0, 0x00, 0x00, 0x00, 0x5f, 0xcc, 0x50];
        var bds17Span2 = new ReadOnlySpan<byte>(bds17Buff2);
        byte[] bds17Buff3 = [0xaa, 0x00, 0x1f, 0x3b, 0xc2, 0x38, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x8e, 0xc4];
        var bds17Span3 = new ReadOnlySpan<byte>(bds17Buff3);
        byte[] bds17Buff4 = [0xa0, 0x28, 0x01, 0x33, 0xfe, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x59, 0xa6, 0x46];
        var bds17Span4 = new ReadOnlySpan<byte>(bds17Buff4); 
        
        var df20_1 = new ModeSDF20();
        df20_1.Deserialize(ref bds17Span1);
        var df20_2 = new ModeSDF20();
        df20_2.Deserialize(ref bds17Span2);
        var df21_3 = new ModeSDF21();
        df21_3.Deserialize(ref bds17Span3);
        var df20_4 = new ModeSDF20();
        df20_4.Deserialize(ref bds17Span4);


        // var df4_s = new ModeSUF5
        // {
        //     PC = 0, RR = 0, DI = 1, InterrogatorIdentifier = 1, Lockout = false, IcaoAddress = 0xF0F0F0
        // };
        // var b1_4 = new byte[7];
        // var sp1_4t = new Span<byte>(b1_4);
        // var sp1_4s = new ReadOnlySpan<byte>(b1_4);
        // df4_s.Serialize(ref sp1_4t);
        // var df4t = new ModeSUF5();
        // df4t.Deserialize(ref sp1_4s);
        // GetMessage(b1_4);
        
        var df4_s = new ModeSUF4
        {
            RR = 0, DI = 1, BDS2 = 0, InterrogatorIdentifier = 1, Lockout = true, IcaoAddress = 0xF0F0F0
        };
        var b1_4 = new byte[7];
        var sp1_4t = new Span<byte>(b1_4);
        var sp1_4s = new ReadOnlySpan<byte>(b1_4);
        df4_s.Serialize(ref sp1_4t);
        var df4t = new ModeSUF4();
        df4t.Deserialize(ref sp1_4s);
        GetMessage(b1_4);
        
        var b = new byte[7];
        var bufferOut = new Span<byte>(b);
        var bufferIn = new ReadOnlySpan<byte>(b);
        var df4_1 = new ModeSDF4 { Altitude = 3261.36, FS = 0, IcaoAddress = 0xAC3421 };
        df4_1.Serialize(ref bufferOut);
        var df4_2 = new ModeSDF4();
        df4_2.Deserialize(ref bufferIn);
        
        ConnectLms = ReactiveCommand.CreateRunInBackground(ConnectLmsImpl);
        DisconnectLms = ReactiveCommand.CreateRunInBackground(DisconnectLmsImpl);
    }
    
    [ImportingConstructor]
    public ModeACInterrogateViewModel(IConfiguration cfg, ILoggerFactory loggerFactory) : this()
    {
        _logger = loggerFactory.CreateLogger<ModeACInterrogateViewModel>();
        _cfg = cfg.Get<ModeACInterrogateConfig>();
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

    // Преобразование из обычного бинарного в код Грея
    public static int ToGray(int n)
    {
        // n XOR (n сдвинутое вправо на 1)
        return n ^ (n >> 1);
    }

    // Преобразование из кода Грея обратно в бинарное число
    public static int FromGray(int gray)
    {
        int n = gray;
        // Последовательное XOR с уменьшенным значением Gray
        n ^= (n >> 1);
        n ^= (n >> 2);
        n ^= (n >> 4);
        n ^= (n >> 8);
        n ^= (n >> 16);
        return n;
    }
    
    private static void SetBits(out byte x1, out byte x2, out byte x4, byte value)
    {
        if ((value & 0xF8) != 0) throw new ArgumentOutOfRangeException(nameof(value));
        x1 = (byte)(value & 0x1);
        x2 = (byte)((value & 0x2) >> 1);
        x4 = (byte)((value & 0x4) >> 2);
    }
    private static ushort GetSquawk(ushort id)
    {
        var a = (byte)(((id & (1 << 11)) >> 11) | ((id & (1 << 9)) >> 8) | ((id & (1 << 7)) >> 5));
        var b = (byte)(((id & (1 << 5)) >> 5) | ((id & (1 << 3)) >> 2) | ((id & (1 << 1)) << 1));
        var c = (byte)(((id & (1 << 12)) >> 12) | ((id & (1 << 10)) >> 9) | ((id & (1 << 8)) >> 6));
        var d = (byte)(((id & (1 << 4)) >> 4) | ((id & (1 << 2)) >> 1) | ((id & 1) << 2));

        return (ushort)(a * 1000 + b * 100 + c * 10 + d);
    }
    
    private static ushort SetSquawk(uint squawk)
    {
        var a = (byte)((squawk / 1000) % 10);
        var b = (byte)((squawk / 100) % 10);
        var c = (byte)((squawk / 10) % 10);
        var d = (byte)(squawk % 10);

        SetBits(out var a1, out var a2, out var a4, a);
        SetBits(out var b1, out var b2, out var b4, b);
        SetBits(out var c1, out var c2, out var c4, c);
        SetBits(out var d1, out var d2, out var d4, d);

        return (ushort)((c1 << 12) | (a1 << 11) | (c2 << 10) | (a2 << 9) | (c4 << 8) | (a4 << 7) | (b1 << 5) | (d1 << 4) |
                        (b2 << 3) | (d2 << 2) | (b4 << 1) | d4);
    }
    private static uint SetAltitude(double alt)
    {
        if (double.IsNaN(alt)) return 0;
    
        alt *= 3.28084;
        var altNorm = alt switch
        {
            > 126700 => 126700,
            < -1200.0 => -1200,
            _ => (int)Math.Round(alt / 100.0) * 100
        };
    
        
        altNorm = (altNorm + 1200) / 100;
        
        var gr = ToGray(altNorm);
        var q = ToOctal(gr);
        return SetSquawk(q);
    }

    private static uint ToOctal(int quotient)
    {
        var currentIndex = 0;
        var octalNumber = new byte[4];
        while (quotient != 0)
        {
            octalNumber[currentIndex] = (byte)(quotient % 8);
            currentIndex += 1;
            quotient /= 8;
        }
        return (uint)(octalNumber[0] + octalNumber[1] * 10 + octalNumber[2] * 100 + octalNumber[3] * 1000);
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
            await _device.SetAntenna(LmsChannel.Tx, 0, (uint)LmsPathTx.LMS_PATH_TX2, CancellationToken.None);
            await _device.SetBandWidth(LmsChannel.Tx, 0, bandWidth, CancellationToken.None);

            await _device.SetFrequency(LmsChannel.Tx, 0, freq, CancellationToken.None);
            await _device.SetNormalizedGain(LmsChannel.Tx, 0, gain, CancellationToken.None);

            var modeA = new float[2][];
            modeA[0] = ModeACGenerator.GenerateModeAQuery(sampleRate);
            modeA[1] = ModeACGenerator.GenerateModeAWithP2Query(sampleRate);
            
            var modeC = new float[2][];
            modeC[0] = ModeACGenerator.GenerateModeCQuery(sampleRate);
            modeC[1] = ModeACGenerator.GenerateModeCWithP2Query(sampleRate);

            _p2 = OnOffP2 ? 1 : 0;
            _msgIdx = 0;
            _txStream = await _device.CreateStream(LmsChannel.Tx, 0, (uint)modeA[0].Length,
                throughputVsLatency: 1.0f, cancel: _cancelStream!.Token).DisposeItWith(Disposable);
            await _txStream.Start(_cancelStream.Token);

            
            var thread = new Thread(() =>
            {
                while (_cancelStream is { Token.IsCancellationRequested: false })
                {
                    _txStream.Write(_msgIdx == 0 ? modeA[_p2] : modeC[_p2], 10_000, _cancelStream.Token).Wait();
                    _msgIdx = Modes ? (++_msgIdx % 2) : 0;
                    // for (var i = 0; i < 10 && _cancelStream is { Token.IsCancellationRequested: false }; i++)
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
    [Reactive] public bool OnOffP2 { get; set; }
    [Reactive] public bool Modes { get; set; }

    public void InitCharts(AvaPlot avaPlotLms, AvaPlot avaPlotSh)
    {
        _avaPlotLms = avaPlotLms;
        _avaPlotSh = avaPlotSh;
    }
}

public class ModeCGillhamBits : IEquatable<ModeCGillhamBits>
{
    public int A1 { get; }
    public int A2 { get; }
    public int A4 { get; }
    public int B1 { get; }
    public int B2 { get; }
    public int B4 { get; }
    public int C1 { get; }
    public int C2 { get; }
    public int C4 { get; }
    public int D1 { get; }
    public int D2 { get; }
    public int D4 { get; }

    public ModeCGillhamBits(int a1, int a2, int a4,
        int b1, int b2, int b4,
        int c1, int c2, int c4,
        int d1, int d2, int d4)
    {
        A1 = a1;
        A2 = a2;
        A4 = a4;
        B1 = b1;
        B2 = b2;
        B4 = b4;
        C1 = c1;
        C2 = c2;
        C4 = c4;
        D1 = d1;
        D2 = d2;
        D4 = d4;
    }

    public bool Equals(ModeCGillhamBits? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return A1 == other.A1 && A2 == other.A2 && A4 == other.A4 && B1 == other.B1 && B2 == other.B2 &&
               B4 == other.B4 && C1 == other.C1 && C2 == other.C2 && C4 == other.C4 && D1 == other.D1 &&
               D2 == other.D2 && D4 == other.D4;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ModeCGillhamBits)obj);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(A1);
        hashCode.Add(A2);
        hashCode.Add(A4);
        hashCode.Add(B1);
        hashCode.Add(B2);
        hashCode.Add(B4);
        hashCode.Add(C1);
        hashCode.Add(C2);
        hashCode.Add(C4);
        hashCode.Add(D1);
        hashCode.Add(D2);
        hashCode.Add(D4);
        return hashCode.ToHashCode();
    }
}

public static class ModeCGillham
{
    // Gray code: g = n ^ (n >> 1)
    private static int BinToGray(int n) => n ^ (n >> 1);

    private static int PopCount(int x)
    {
        // простой popcount (x здесь максимум 8..9 бит)
        int c = 0;
        while (x != 0)
        {
            c += (x & 1);
            x >>= 1;
        }

        return c;
    }

    /// <summary>
    /// Кодирование Mode C (Gillham) из высоты в футах (pressure altitude, 100-ft increments).
    /// Возвращает биты A1 A2 A4 B1 B2 B4 C1 C2 C4 D1 D2 D4 (D1 всегда 0).
    /// </summary>
    public static ModeCGillhamBits Encode(int altitudeFt)
    {
        // Диапазон по таблицам Gillham обычно: -1200..126700 ft
        if (altitudeFt < -1200 || altitudeFt > 126700)
            throw new ArgumentOutOfRangeException(nameof(altitudeFt),
                "Mode C (Gillham) range is typically -1200..126700 ft.");

        // Mode C задаётся кратно 100 ft
        if (altitudeFt % 100 != 0)
            throw new ArgumentException("Mode C (Gillham) altitude must be a multiple of 100 ft.", nameof(altitudeFt));

        // 1) Выбираем опорный уровень 500 ft так, чтобы остаток был в [-200..+200]
        //    n500 = floor((alt + 200)/500)
        var n500 = (int)Math.Floor((altitudeFt + 200.0) / 500.0);
        var r = altitudeFt - n500 * 500; // r ∈ {-200,-100,0,100,200}

        if (r != -200 && r != -100 && r != 0 && r != 100 && r != 200)
            throw new InvalidOperationException("Unexpected remainder; check input altitude.");

        // 2) 500-ft код: 8-битный Gray от (n500 + 2)
        //    (смещение +2 согласуется с таблицей: -1200 ft -> все нули в 500-ft части).
        var m = n500 + 2;
        if (m is < 0 or > 255) // практически достаточно 8 бит
            throw new InvalidOperationException("Internal 500-ft index out of 8-bit range.");

        var g = BinToGray(m) & 0xFF;

        // Раскладка g по битам (MSB..LSB): D2 D4 A1 A2 A4 B1 B2 B4
        var D2 = (g >> 7) & 1;
        var D4 = (g >> 6) & 1;
        var A1 = (g >> 5) & 1;
        var A2 = (g >> 4) & 1;
        var A4 = (g >> 3) & 1;
        var B1 = (g >> 2) & 1;
        var B2 = (g >> 1) & 1;
        var B4 = (g >> 0) & 1;

        // 3) Чётность (parity) 500-ft кода определяет таблицу C.
        var evenParity = (PopCount(g) % 2) == 0;

        // Таблицы C (C1 C2 C4) для r = -200,-100,0,+100,+200
        // even:  -200=001, -100=011, 0=010, +100=110, +200=100
        // odd:   -200=100, -100=110, 0=010, +100=011, +200=001  (реверс)
        int c3 = evenParity
            ? r switch
            {
                -200 => 0b001,
                -100 => 0b011,
                0 => 0b010,
                100 => 0b110,
                200 => 0b100,
                _ => throw new InvalidOperationException()
            }
            : r switch
            {
                -200 => 0b100,
                -100 => 0b110,
                0 => 0b010,
                100 => 0b011,
                200 => 0b001,
                _ => throw new InvalidOperationException()
            };

        var C1 = (c3 >> 2) & 1;
        var C2 = (c3 >> 1) & 1;
        var C4 = (c3 >> 0) & 1;

        var D1 = 0; // не используется в практических применениях

        return new ModeCGillhamBits(A1, A2, A4, B1, B2, B4, C1, C2, C4, D1, D2, D4);
    }

    public static ValueTuple<int, ModeCGillhamBits>[] Table =
    [
        (-1200, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0)),
        (-1100, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0)),
        (-1000, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0)),
        (-900, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0)),
        (-800, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0)),
        (-700, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0)),
        (-600, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0)),
        (-500, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0)),
        (-400, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 0, 1, 1, 0, 0, 0)),
        (-300, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0)),
        (-200, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0)),
        (-100, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0)),
        (0, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 0, 1, 0, 0, 0, 0)),
        (100, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0)),
        (200, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0)),
        (300, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0)),
        (400, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 1, 1, 0, 0, 0, 0)),
        (500, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0)),
        (600, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 0)),
        (700, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0)),
        (800, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0)),
        (900, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0)),
        (1000, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0)),
        (1100, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 0)),
        (1200, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 1, 0, 0, 0, 0, 0)),
        (1300, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0)),
        (1400, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0)),
        (1500, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0)),
        (1600, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 0, 1, 1, 0, 0, 0)),
        (1700, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0)),
        (1800, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 0, 0, 1, 0, 0, 0)),
        (1900, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 0, 1, 1, 0, 0, 0)),
        (2000, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0)),
        (2100, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 1, 1, 0, 0, 0, 0)),
        (2200, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0)),
        (2300, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0)),
        (2400, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 0, 0)),
        (2500, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0)),
        (2600, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0)),
        (2700, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0)),
        (2800, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0)),
        (2900, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0)),
        (3000, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0)),
        (3100, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0)),
        (3200, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0)),
        (3300, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 0, 0)),
        (3400, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 1, 1, 0, 0, 0, 0)),
        (3500, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 0, 1, 0, 0, 0, 0)),
        (3600, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0)),
        (3700, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 0, 0, 1, 0, 0, 0)),
        (3800, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0)),
        (3900, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 0, 1, 1, 0, 0, 0)),
        (4000, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0)),
        (4100, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0)),
        (4200, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0)),
        (4300, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0, 0)),
        (4400, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 1, 1, 0, 0, 0, 0)),
        (4500, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0)),
        (4600, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0)),
        (4700, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0)),
        (4800, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0)),
        (4900, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 0, 1, 1, 0, 0, 0)),
        (5000, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0)),
        (5100, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0)),
        (5200, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0, 0)),
        (5300, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 1, 0, 0, 0, 0, 0)),
        (5400, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 1, 1, 0, 0, 0, 0)),
        (5500, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 0, 0)),
        (5600, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 0, 1, 1, 0, 0, 0)),
        (5700, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 0, 0, 1, 0, 0, 0)),
        (5800, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0)),
        (5900, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 0)),
        (6000, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 0, 1, 0, 0, 0, 0)),
        (6100, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0)),
        (6200, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0)),
        (6300, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0)),
        (6400, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0)),
        (6500, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0)),
        (6600, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0)),
        (6700, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0)),
        (6800, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0)),
        (6900, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0)),
        (7000, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0)),
        (7100, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0)),
        (7200, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0)),
        (7300, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0)),
        (7400, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0)),
        (7500, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 0, 1, 0, 0, 0, 0)),
        (7600, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 0)),
        (7700, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0)),
        (7800, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 0)),
        (7900, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 0, 0)),
        (8000, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 0, 1, 0, 0, 0, 0)),
        (8100, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 1, 1, 0, 0, 0, 0)),
        (8200, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 1, 0, 0, 0, 0, 0)),
        (8300, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 1, 0, 0, 0, 0, 0)),
        (8400, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0)),
        (8500, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0)),
        (8600, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 0)),
        (8700, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0)),
        (8800, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0)),
        (8900, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0)),
        (9000, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0)),
        (9100, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 1, 1, 0, 0, 0, 0)),
        (9200, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0, 0)),
        (9300, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0)),
        (9400, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0)),
        (9500, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0)),
        (9600, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 0, 1, 1, 0, 0, 0)),
        (9700, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0)),
        (9800, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 0, 0, 1, 0, 0, 0)),
        (9900, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0)),
        (10000, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 0, 1, 0, 0, 0, 0)),
        (10100, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 1, 1, 0, 0, 0, 0)),
        (10200, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 1, 0, 0, 0, 0, 0)),
        (10300, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0)),
        (10400, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0)),
        (10500, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0)),
        (10600, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0)),
        (10700, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0)),
        (10800, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0)),
        (10900, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0)),
        (11000, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0)),
        (11100, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 1, 1, 0, 0, 0, 0)),
        (11200, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0)),
        (11300, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0)),
        (11400, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 1, 1, 0, 0, 0, 0)),
        (11500, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0)),
        (11600, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 0, 1, 1, 0, 0, 0)),
        (11700, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 0, 0, 1, 0, 0, 0)),
        (11800, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0)),
        (11900, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 0, 1, 1, 0, 0, 0)),
        (12000, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0)),
        (12100, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0)),
        (12200, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0)),
        (12300, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 1, 0, 0, 0, 0, 0)),
        (12400, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 1, 1, 0, 0, 0, 0)),
        (12500, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0)),
        (12600, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0)),
        (12700, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0)),
        (12800, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0)),
        (12900, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 0, 1, 1, 0, 0, 0)),
        (13000, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0)),
        (13100, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 0, 0)),
        (13200, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0)),
        (13300, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0)),
        (13400, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0)),
        (13500, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 0, 1, 0, 0, 0, 0)),
        (13600, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0)),
        (13700, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0)),
        (13800, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0)),
        (13900, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 0, 1, 1, 0, 0, 0)),
        (14000, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0)),
        (14100, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0)),
        (14200, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0)),
        (14300, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0)),
        (14400, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0)),
        (14500, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0)),
        (14600, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0)),
        (14700, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0)),
        (14800, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0)),
        (14900, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0)),
        (15000, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0)),
        (15100, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0)),
        (15200, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0)),
        (15300, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0)),
        (15400, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0)),
        (15500, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0)),
        (15600, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 0, 1, 1, 0, 0, 0)),
        (15700, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0)),
        (15800, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0)),
        (15900, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0)),
        (16000, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 0, 0)),
        (16100, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0)),
        (16200, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0)),
        (16300, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0)),
        (16400, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 0, 0)),
        (16500, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0)),
        (16600, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 0, 1, 1, 0, 0, 0)),
        (16700, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0)),
        (16800, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0)),
        (16900, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0)),
        (17000, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0)),
        (17100, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 1, 1, 0, 0, 0, 0)),
        (17200, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 1, 0, 0, 0, 0, 0)),
        (17300, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0)),
        (17400, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0)),
        (17500, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0)),
        (17600, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 0, 1, 1, 0, 0, 0)),
        (17700, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0)),
        (17800, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 0, 0, 1, 0, 0, 0)),
        (17900, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 0, 1, 1, 0, 0, 0)),
        (18000, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0)),
        (18100, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 1, 1, 0, 0, 0, 0)),
        (18200, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0)),
        (18300, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0)),
        (18400, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 0, 0)),
        (18500, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0)),
        (18600, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0)),
        (18700, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0)),
        (18800, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0)),
        (18900, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0)),
        (19000, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0)),
        (19100, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0)),
        (19200, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0)),
        (19300, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 1, 0, 0, 0, 0, 0)),
        (19400, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 1, 1, 0, 0, 0, 0)),
        (19500, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 0, 1, 0, 0, 0, 0)),
        (19600, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0)),
        (19700, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 0, 0, 1, 0, 0, 0)),
        (19800, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0)),
        (19900, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 0, 0)),
        (20000, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0)),
        (20100, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0)),
        (20200, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0)),
        (20300, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0, 0)),
        (20400, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 1, 1, 0, 0, 0, 0)),
        (20500, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0)),
        (20600, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0)),
        (20700, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0)),
        (20800, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0)),
        (20900, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 0)),
        (21000, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0)),
        (21100, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0)),
        (21200, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 1, 0, 0, 0, 0, 0)),
        (21300, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 1, 0, 0, 0, 0, 0)),
        (21400, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 1, 1, 0, 0, 0, 0)),
        (21500, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 0, 1, 0, 0, 0, 0)),
        (21600, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 0, 1, 1, 0, 0, 0)),
        (21700, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 0)),
        (21800, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0)),
        (21900, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 0)),
        (22000, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 0, 1, 0, 0, 0, 0)),
        (22100, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0)),
        (22200, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0)),
        (22300, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0)),
        (22400, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0)),
        (22500, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0)),
        (22600, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0)),
        (22700, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0)),
        (22800, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0)),
        (22900, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0)),
        (23000, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0)),
        (23100, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0)),
        (23200, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0)),
        (23300, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0)),
        (23400, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0)),
        (23500, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 0, 1, 0, 0, 0, 0)),
        (23600, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 0)),
        (23700, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0)),
        (23800, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 0, 0, 1, 0, 0, 0)),
        (23900, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 0, 1, 1, 0, 0, 0)),
        (24000, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 0, 1, 0, 0, 0, 0)),
        (24100, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 1, 1, 0, 0, 0, 0)),
        (24200, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 1, 0, 0, 0, 0, 0)),
        (24300, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0, 0)),
        (24400, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0)),
        (24500, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0)),
        (24600, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 0, 1, 1, 0, 0, 0)),
        (24700, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0)),
        (24800, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0)),
        (24900, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0)),
        (25000, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0)),
        (25100, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 1, 1, 0, 0, 0, 0)),
        (25200, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0, 0)),
        (25300, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0)),
        (25400, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0)),
        (25500, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0)),
        (25600, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 0, 1, 1, 0, 0, 0)),
        (25700, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0)),
        (25800, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 0, 0)),
        (25900, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0)),
        (26000, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 0, 1, 0, 0, 0, 0)),
        (26100, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 1, 1, 0, 0, 0, 0)),
        (26200, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 1, 0, 0, 0, 0, 0)),
        (26300, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0)),
        (26400, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0)),
        (26500, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0)),
        (26600, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0)),
        (26700, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0)),
        (26800, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0)),
        (26900, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0)),
        (27000, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0)),
        (27100, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 1, 1, 0, 0, 0, 0)),
        (27200, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0)),
        (27300, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0)),
        (27400, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 1, 1, 0, 0, 0, 0)),
        (27500, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0)),
        (27600, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 0, 1, 1, 0, 0, 0)),
        (27700, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 0, 0, 1, 0, 0, 0)),
        (27800, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0)),
        (27900, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 0, 1, 1, 0, 0, 0)),
        (28000, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0)),
        (28100, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0)),
        (28200, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0)),
        (28300, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 1, 0, 0, 0, 0, 0)),
        (28400, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 0)),
        (28500, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0)),
        (28600, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0)),
        (28700, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0)),
        (28800, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0)),
        (28900, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 0)),
        (29000, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0)),
        (29100, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 1, 1, 0, 0, 0, 0)),
        (29200, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0)),
        (29300, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0)),
        (29400, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0)),
        (29500, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 0, 1, 0, 0, 0, 0)),
        (29600, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0)),
        (29700, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0)),
        (29800, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0)),
        (29900, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 0, 1, 1, 0, 0, 0)),
        (30000, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0)),
        (30100, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0)),
        (30200, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0)),
        (30300, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0)),
        (30400, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0)),
        (30500, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0)),
        (30600, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0)),
        (30700, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0)),
        (30800, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1)),
        (30900, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 1)),
        (31000, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1)),
        (31100, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 1)),
        (31200, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)),
        (31300, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1)),
        (31400, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1)),
        (31500, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1)),
        (31600, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 0, 1, 1, 0, 0, 1)),
        (31700, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1)),
        (31800, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 1)),
        (31900, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 1)),
        (32000, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 0, 1, 0, 0, 0, 1)),
        (32100, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1)),
        (32200, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1)),
        (32300, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 1)),
        (32400, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 1, 1, 0, 0, 0, 1)),
        (32500, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1)),
        (32600, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1)),
        (32700, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1)),
        (32800, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 1)),
        (32900, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1)),
        (33000, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 1)),
        (33100, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 1)),
        (33200, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 1, 0, 0, 0, 0, 1)),
        (33300, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1)),
        (33400, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1)),
        (33500, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 0, 1, 0, 0, 0, 1)),
        (33600, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 0, 1, 1, 0, 0, 1)),
        (33700, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 1)),
        (33800, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 0, 0, 1, 0, 0, 1)),
        (33900, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 0, 1, 1, 0, 0, 1)),
        (34000, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 1)),
        (34100, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 1, 1, 0, 0, 0, 1)),
        (34200, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 1, 0, 0, 0, 0, 1)),
        (34300, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 1)),
        (34400, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 1, 1, 0, 0, 0, 1)),
        (34500, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1)),
        (34600, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 1)),
        (34700, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 1)),
        (34800, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 1)),
        (34900, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1)),
        (35000, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 1)),
        (35100, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1)),
        (35200, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 1)),
        (35300, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 1, 0, 0, 0, 0, 1)),
        (35400, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 1, 1, 0, 0, 0, 1)),
        (35500, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 0, 1, 0, 0, 0, 1)),
        (35600, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 1)),
        (35700, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 0, 1)),
        (35800, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 0, 0, 1, 0, 0, 1)),
        (35900, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 0, 1, 1, 0, 0, 1)),
        (36000, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 0, 1, 0, 0, 0, 1)),
        (36100, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1)),
        (36200, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1)),
        (36300, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0, 1)),
        (36400, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 1, 1, 0, 0, 0, 1)),
        (36500, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 1)),
        (36600, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1)),
        (36700, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 0, 0, 1, 0, 0, 1)),
        (36800, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 0, 0, 1, 0, 0, 1)),
        (36900, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1)),
        (37000, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 0, 1, 0, 0, 0, 1)),
        (37100, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 1, 1, 0, 0, 0, 1)),
        (37200, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0, 1)),
        (37300, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 1, 0, 0, 0, 0, 1)),
        (37400, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 1, 1, 0, 0, 0, 1)),
        (37500, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 0, 1, 0, 0, 0, 1)),
        (37600, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1)),
        (37700, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1)),
        (37800, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1)),
        (37900, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1)),
        (38000, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 0, 1, 0, 0, 0, 1)),
        (38100, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1)),
        (38200, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 1, 0, 0, 0, 0, 1)),
        (38300, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1)),
        (38400, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1)),
        (38500, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1)),
        (38600, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1)),
        (38700, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1)),
        (38800, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1)),
        (38900, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1)),
        (39000, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1)),
        (39100, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1)),
        (39200, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1)),
        (39300, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 1)),
        (39400, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1)),
        (39500, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 0, 1, 0, 0, 0, 1)),
        (39600, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1)),
        (39700, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1)),
        (39800, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1)),
        (39900, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1)),
        (40000, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 0, 1, 0, 0, 0, 1)),
        (40100, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 1, 1, 0, 0, 0, 1)),
        (40200, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 1, 0, 0, 0, 0, 1)),
        (40300, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 1, 0, 0, 0, 0, 1)),
        (40400, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 1)),
        (40500, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 0, 1, 0, 0, 0, 1)),
        (40600, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1)),
        (40700, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 0, 0, 1, 0, 0, 1)),
        (40800, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 0, 0, 1, 0, 0, 1)),
        (40900, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1)),
        (41000, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0, 1)),
        (41100, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 1, 1, 0, 0, 0, 1)),
        (41200, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0, 1)),
        (41300, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1)),
        (41400, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1)),
        (41500, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 1)),
        (41600, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 0, 1)),
        (41700, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 0, 0, 1, 0, 0, 1)),
        (41800, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 0, 0, 1, 0, 0, 1)),
        (41900, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 0, 1, 1, 0, 0, 1)),
        (42000, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 0, 1, 0, 0, 0, 1)),
        (42100, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 1, 1, 0, 0, 0, 1)),
        (42200, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 1, 0, 0, 0, 0, 1)),
        (42300, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 1)),
        (42400, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1)),
        (42500, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 1)),
        (42600, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1)),
        (42700, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 0, 0, 1, 0, 0, 1)),
        (42800, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 1)),
        (42900, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 1)),
        (43000, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1)),
        (43100, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 0, 1)),
        (43200, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 1)),
        (43300, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 1)),
        (43400, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 1, 1, 0, 0, 0, 1)),
        (43500, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 0, 1, 0, 0, 0, 1)),
        (43600, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 0, 1, 1, 0, 0, 1)),
        (43700, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 0, 0, 1, 0, 0, 1)),
        (43800, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 0, 0, 1, 0, 0, 1)),
        (43900, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 0, 1, 1, 0, 0, 1)),
        (44000, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 0, 1, 0, 0, 0, 1)),
        (44100, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1)),
        (44200, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1)),
        (44300, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 1, 0, 0, 0, 0, 1)),
        (44400, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 1, 1, 0, 0, 0, 1)),
        (44500, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 0, 1)),
        (44600, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1)),
        (44700, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 0, 0, 1, 0, 0, 1)),
        (44800, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1)),
        (44900, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1)),
        (45000, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1)),
        (45100, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 0, 1)),
        (45200, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 1, 0, 0, 0, 0, 1)),
        (45300, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1)),
        (45400, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1)),
        (45500, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 0, 1)),
        (45600, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 0, 1, 1, 0, 0, 1)),
        (45700, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 1)),
        (45800, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1)),
        (45900, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 0, 1, 1, 0, 0, 1)),
        (46000, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1)),
        (46100, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1)),
        (46200, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1)),
        (46300, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)),
        (46400, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 1)),
        (46500, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1)),
        (46600, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 0, 1, 1, 0, 0, 1)),
        (46700, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1)),
        (46800, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1)),
        (46900, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 0, 1, 1, 0, 0, 1)),
        (47000, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1)),
        (47100, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 1)),
        (47200, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)),
        (47300, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1)),
        (47400, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1)),
        (47500, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1)),
        (47600, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 0, 1, 1, 0, 0, 1)),
        (47700, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1)),
        (47800, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 1)),
        (47900, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 0, 1, 1, 0, 0, 1)),
        (48000, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 0, 1, 0, 0, 0, 1)),
        (48100, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1)),
        (48200, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1)),
        (48300, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 1, 0, 0, 0, 0, 1)),
        (48400, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 0, 1)),
        (48500, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1)),
        (48600, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1)),
        (48700, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1)),
        (48800, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 0, 0, 1, 0, 0, 1)),
        (48900, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1)),
        (49000, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 0, 1, 0, 0, 0, 1)),
        (49100, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 1, 1, 0, 0, 0, 1)),
        (49200, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 1, 0, 0, 0, 0, 1)),
        (49300, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1)),
        (49400, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1)),
        (49500, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 0, 1, 0, 0, 0, 1)),
        (49600, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 0, 1, 1, 0, 0, 1)),
        (49700, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 0, 0, 1, 0, 0, 1)),
        (49800, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 0, 0, 1, 0, 0, 1)),
        (49900, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 0, 1, 1, 0, 0, 1)),
        (50000, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 0, 1, 0, 0, 0, 1)),
        (50100, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 1, 1, 0, 0, 0, 1)),
        (50200, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 1)),
        (50300, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 1)),
        (50400, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 1, 1, 0, 0, 0, 1)),
        (50500, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1)),
        (50600, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 1)),
        (50700, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 1)),
        (50800, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 0, 0, 1, 0, 0, 1)),
        (50900, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1)),
        (51000, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 1)),
        (51100, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1)),
        (51200, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 1)),
        (51300, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 1, 0, 0, 0, 0, 1)),
        (51400, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 1, 1, 0, 0, 0, 1)),
        (51500, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 0, 1, 0, 0, 0, 1)),
        (51600, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 0, 1, 1, 0, 0, 1)),
        (51700, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 0, 0, 1, 0, 0, 1)),
        (51800, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 0, 0, 1, 0, 0, 1)),
        (51900, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 0, 1, 1, 0, 0, 1)),
        (52000, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 1)),
        (52100, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1)),
        (52200, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1)),
        (52300, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0, 1)),
        (52400, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 1, 1, 0, 0, 0, 1)),
        (52500, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0, 1)),
        (52600, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1)),
        (52700, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 0, 0, 1, 0, 0, 1)),
        (52800, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 0, 0, 1, 0, 0, 1)),
        (52900, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1)),
        (53000, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 0, 1, 0, 0, 0, 1)),
        (53100, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 1)),
        (53200, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 1, 0, 0, 0, 0, 1)),
        (53300, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 1, 0, 0, 0, 0, 1)),
        (53400, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 1, 1, 0, 0, 0, 1)),
        (53500, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 0, 1, 0, 0, 0, 1)),
        (53600, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1)),
        (53700, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1)),
        (53800, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1)),
        (53900, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1)),
        (54000, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 0, 1, 0, 0, 0, 1)),
        (54100, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1)),
        (54200, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 1)),
        (54300, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1)),
        (54400, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1)),
        (54500, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1)),
        (54600, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1)),
        (54700, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1)),
        (54800, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1)),
        (54900, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1)),
        (55000, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1)),
        (55100, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1)),
        (55200, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1)),
        (55300, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 1, 0, 0, 0, 0, 1)),
        (55400, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1)),
        (55500, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 0, 1, 0, 0, 0, 1)),
        (55600, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1)),
        (55700, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1)),
        (55800, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1)),
        (55900, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1)),
        (56000, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 0, 1)),
        (56100, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 1, 1, 0, 0, 0, 1)),
        (56200, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 1, 0, 0, 0, 0, 1)),
        (56300, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0, 1)),
        (56400, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 1, 1, 0, 0, 0, 1)),
        (56500, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 0, 1, 0, 0, 0, 1)),
        (56600, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1)),
        (56700, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 0, 0, 1, 0, 0, 1)),
        (56800, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 0, 0, 1, 0, 0, 1)),
        (56900, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1)),
        (57000, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 1)),
        (57100, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 1, 1, 0, 0, 0, 1)),
        (57200, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0, 1)),
        (57300, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1)),
        (57400, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1)),
        (57500, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 0, 1, 0, 0, 0, 1)),
        (57600, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 0, 1, 1, 0, 0, 1)),
        (57700, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 0, 0, 1, 0, 0, 1)),
        (57800, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 0, 0, 1, 0, 0, 1)),
        (57900, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 1)),
        (58000, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 0, 1, 0, 0, 0, 1)),
        (58100, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 1, 1, 0, 0, 0, 1)),
        (58200, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 0, 1)),
        (58300, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 1)),
        (58400, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1)),
        (58500, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 1)),
        (58600, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1)),
        (58700, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 1)),
        (58800, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 1)),
        (58900, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 1)),
        (59000, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1)),
        (59100, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 0, 1)),
        (59200, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 1)),
        (59300, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 1, 0, 0, 0, 0, 1)),
        (59400, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 1, 1, 0, 0, 0, 1)),
        (59500, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 1)),
        (59600, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 0, 1, 1, 0, 0, 1)),
        (59700, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 0, 0, 1, 0, 0, 1)),
        (59800, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 1)),
        (59900, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 0, 1, 1, 0, 0, 1)),
        (60000, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 0, 1, 0, 0, 0, 1)),
        (60100, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1)),
        (60200, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1)),
        (60300, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 1, 0, 0, 0, 0, 1)),
        (60400, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 1)),
        (60500, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 1)),
        (60600, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1)),
        (60700, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 1)),
        (60800, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1)),
        (60900, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1)),
        (61000, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1)),
        (61100, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 1, 1, 0, 0, 0, 1)),
        (61200, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 1)),
        (61300, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 1)),
        (61400, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1)),
        (61500, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 0, 1, 0, 0, 0, 1)),
        (61600, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 1)),
        (61700, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 1)),
        (61800, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1)),
        (61900, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 0, 1, 1, 0, 0, 1)),
        (62000, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1)),
        (62100, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1)),
        (62200, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1)),
        (62300, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)),
        (62400, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 1)),
        (62500, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1)),
        (62600, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 1)),
        (62700, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1)),
        (62800, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1)),
        (62900, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 1, 1)),
        (63000, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1)),
        (63100, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 1, 1)),
        (63200, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1)),
        (63300, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1)),
        (63400, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1)),
        (63500, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 1, 1)),
        (63600, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 0, 1, 1, 0, 1, 1)),
        (63700, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 1, 1)),
        (63800, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1)),
        (63900, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 0, 1, 1, 0, 1, 1)),
        (64000, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1)),
        (64100, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1)),
        (64200, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1)),
        (64300, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1, 1)),
        (64400, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1)),
        (64500, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1)),
        (64600, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1)),
        (64700, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 1)),
        (64800, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 0, 0, 1, 0, 1, 1)),
        (64900, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1)),
        (65000, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 1, 1)),
        (65100, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 1, 1)),
        (65200, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 1, 0, 0, 0, 1, 1)),
        (65300, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1)),
        (65400, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 1, 1)),
        (65500, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 0, 1, 0, 0, 1, 1)),
        (65600, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 0, 1, 1, 0, 1, 1)),
        (65700, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 0, 0, 1, 0, 1, 1)),
        (65800, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 0, 0, 1, 0, 1, 1)),
        (65900, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 0, 1, 1, 0, 1, 1)),
        (66000, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 0, 1, 0, 0, 1, 1)),
        (66100, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 1, 1, 0, 0, 1, 1)),
        (66200, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 1, 0, 0, 0, 1, 1)),
        (66300, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1, 1)),
        (66400, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 1)),
        (66500, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 1)),
        (66600, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 1, 1)),
        (66700, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 1, 1)),
        (66800, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 1, 1)),
        (66900, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 0, 1, 1, 0, 1, 1)),
        (67000, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 1, 1)),
        (67100, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1)),
        (67200, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 1, 1)),
        (67300, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 1, 1)),
        (67400, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 1, 1, 0, 0, 1, 1)),
        (67500, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 0, 1, 0, 0, 1, 1)),
        (67600, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 1)),
        (67700, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1)),
        (67800, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 0, 0, 1, 0, 1, 1)),
        (67900, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1)),
        (68000, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 0, 1, 0, 0, 1, 1)),
        (68100, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1)),
        (68200, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1)),
        (68300, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 1, 0, 0, 0, 1, 1)),
        (68400, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 1, 1, 0, 0, 1, 1)),
        (68500, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 1, 1)),
        (68600, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 0, 1, 1, 0, 1, 1)),
        (68700, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 0, 0, 1, 0, 1, 1)),
        (68800, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 0, 0, 1, 0, 1, 1)),
        (68900, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 0, 1, 1, 0, 1, 1)),
        (69000, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 0, 1, 0, 0, 1, 1)),
        (69100, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 1, 1, 0, 0, 1, 1)),
        (69200, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 1, 1)),
        (69300, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 1, 0, 0, 0, 1, 1)),
        (69400, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 1, 1, 0, 0, 1, 1)),
        (69500, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 1)),
        (69600, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1)),
        (69700, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 0, 0, 1, 0, 1, 1)),
        (69800, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 1, 1)),
        (69900, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 1)),
        (70000, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 0, 1, 0, 0, 1, 1)),
        (70100, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 1)),
        (70200, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 1, 0, 0, 0, 1, 1)),
        (70300, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1)),
        (70400, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1)),
        (70500, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 1, 1)),
        (70600, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 1, 1)),
        (70700, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 1, 1)),
        (70800, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 0, 0, 1, 0, 1, 1)),
        (70900, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 1, 1)),
        (71000, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 1, 1)),
        (71100, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1)),
        (71200, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1)),
        (71300, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1, 1)),
        (71400, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 1, 1, 0, 0, 1, 1)),
        (71500, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 0, 1, 0, 0, 1, 1)),
        (71600, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 1)),
        (71700, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 1, 1)),
        (71800, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 1, 1)),
        (71900, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1)),
        (72000, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 0, 1, 0, 0, 1, 1)),
        (72100, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 1, 1, 0, 0, 1, 1)),
        (72200, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 1, 0, 0, 0, 1, 1)),
        (72300, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 1, 0, 0, 0, 1, 1)),
        (72400, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 1, 1)),
        (72500, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 0, 1, 0, 0, 1, 1)),
        (72600, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 1, 1)),
        (72700, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 0, 0, 1, 0, 1, 1)),
        (72800, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 0, 0, 1, 0, 1, 1)),
        (72900, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 1, 1)),
        (73000, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 0, 1, 0, 0, 1, 1)),
        (73100, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 1, 1, 0, 0, 1, 1)),
        (73200, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 1, 0, 0, 0, 1, 1)),
        (73300, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1)),
        (73400, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1)),
        (73500, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 0, 1, 0, 0, 1, 1)),
        (73600, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1)),
        (73700, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 0, 0, 1, 0, 1, 1)),
        (73800, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1)),
        (73900, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 0, 1, 1, 0, 1, 1)),
        (74000, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 0, 1, 0, 0, 1, 1)),
        (74100, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 1, 1, 0, 0, 1, 1)),
        (74200, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 1, 0, 0, 0, 1, 1)),
        (74300, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 1, 1)),
        (74400, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1)),
        (74500, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 0, 1, 0, 0, 1, 1)),
        (74600, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 0, 1, 1, 0, 1, 1)),
        (74700, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 0, 0, 1, 0, 1, 1)),
        (74800, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 0, 0, 1, 0, 1, 1)),
        (74900, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 0, 1, 1, 0, 1, 1)),
        (75000, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 0, 1, 0, 0, 1, 1)),
        (75100, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 1)),
        (75200, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 1, 0, 0, 0, 1, 1)),
        (75300, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 1, 0, 0, 0, 1, 1)),
        (75400, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 1, 1, 0, 0, 1, 1)),
        (75500, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 0, 1, 0, 0, 1, 1)),
        (75600, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 0, 1, 1, 0, 1, 1)),
        (75700, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 0, 0, 1, 0, 1, 1)),
        (75800, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 0, 0, 1, 0, 1, 1)),
        (75900, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 0, 1, 1, 0, 1, 1)),
        (76000, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 0, 1, 0, 0, 1, 1)),
        (76100, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 1, 1, 0, 0, 1, 1)),
        (76200, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1)),
        (76300, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 1, 0, 0, 0, 1, 1)),
        (76400, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1, 1)),
        (76500, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1, 1)),
        (76600, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1)),
        (76700, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 0, 0, 1, 0, 1, 1)),
        (76800, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 0, 0, 1, 0, 1, 1)),
        (76900, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1)),
        (77000, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1)),
        (77100, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1)),
        (77200, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 1, 0, 0, 0, 1, 1)),
        (77300, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1)),
        (77400, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1)),
        (77500, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1)),
        (77600, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 0, 1, 1, 0, 1, 1)),
        (77700, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1)),
        (77800, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 0, 0, 1, 0, 1, 1)),
        (77900, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 0, 1, 1, 0, 1, 1)),
        (78000, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 1, 1)),
        (78100, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1)),
        (78200, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1)),
        (78300, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1)),
        (78400, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1, 1)),
        (78500, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1)),
        (78600, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 0, 1, 1, 0, 1, 1)),
        (78700, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1)),
        (78800, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1)),
        (78900, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 0, 1, 1, 0, 1, 1)),
        (79000, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1)),
        (79100, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1, 1)),
        (79200, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1)),
        (79300, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1)),
        (79400, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1)),
        (79500, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 0, 1, 0, 0, 1, 1)),
        (79600, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 0, 1, 1, 0, 1, 1)),
        (79700, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 0, 0, 1, 0, 1, 1)),
        (79800, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1)),
        (79900, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 0, 1, 1, 0, 1, 1)),
        (80000, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1)),
        (80100, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1)),
        (80200, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1)),
        (80300, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 1, 0, 0, 0, 1, 1)),
        (80400, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1)),
        (80500, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1)),
        (80600, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1)),
        (80700, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 0, 0, 1, 0, 1, 1)),
        (80800, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 0, 0, 1, 0, 1, 1)),
        (80900, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1)),
        (81000, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1, 1)),
        (81100, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1, 1)),
        (81200, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 1, 0, 0, 0, 1, 1)),
        (81300, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1)),
        (81400, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 1, 1, 0, 0, 1, 1)),
        (81500, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 0, 1, 0, 0, 1, 1)),
        (81600, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 0, 1, 1, 0, 1, 1)),
        (81700, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 0, 0, 1, 0, 1, 1)),
        (81800, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 0, 0, 1, 0, 1, 1)),
        (81900, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 0, 1, 1, 0, 1, 1)),
        (82000, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 0, 1, 0, 0, 1, 1)),
        (82100, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 1, 1, 0, 0, 1, 1)),
        (82200, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 1, 1)),
        (82300, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 1, 0, 0, 0, 1, 1)),
        (82400, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 1)),
        (82500, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 0, 1, 0, 0, 1, 1)),
        (82600, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 0, 1, 1, 0, 1, 1)),
        (82700, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 0, 0, 1, 0, 1, 1)),
        (82800, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 0, 0, 1, 0, 1, 1)),
        (82900, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 0, 1, 1, 0, 1, 1)),
        (83000, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 0, 1, 0, 0, 1, 1)),
        (83100, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1)),
        (83200, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 1, 0, 0, 0, 1, 1)),
        (83300, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 1, 0, 0, 0, 1, 1)),
        (83400, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 1, 1, 0, 0, 1, 1)),
        (83500, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 0, 1, 0, 0, 1, 1)),
        (83600, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 0, 1, 1, 0, 1, 1)),
        (83700, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1)),
        (83800, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 0, 0, 1, 0, 1, 1)),
        (83900, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1)),
        (84000, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 0, 1, 0, 0, 1, 1)),
        (84100, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1)),
        (84200, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1)),
        (84300, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 1, 1)),
        (84400, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 1, 1, 0, 0, 1, 1)),
        (84500, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 0, 1, 0, 0, 1, 1)),
        (84600, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 0, 1, 1, 0, 1, 1)),
        (84700, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 0, 0, 1, 0, 1, 1)),
        (84800, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 0, 0, 1, 0, 1, 1)),
        (84900, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 0, 1, 1, 0, 1, 1)),
        (85000, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 0, 1, 0, 0, 1, 1)),
        (85100, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 1, 1, 0, 0, 1, 1)),
        (85200, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 1, 0, 0, 0, 1, 1)),
        (85300, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 1, 0, 0, 0, 1, 1)),
        (85400, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 1, 1, 0, 0, 1, 1)),
        (85500, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 0, 1, 0, 0, 1, 1)),
        (85600, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1)),
        (85700, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 0, 0, 1, 0, 1, 1)),
        (85800, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 0, 0, 1, 0, 1, 1)),
        (85900, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 1)),
        (86000, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 0, 1, 0, 0, 1, 1)),
        (86100, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 1, 1, 0, 0, 1, 1)),
        (86200, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1, 1)),
        (86300, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1)),
        (86400, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1)),
        (86500, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 0, 1, 0, 0, 1, 1)),
        (86600, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 0, 1, 1, 0, 1, 1)),
        (86700, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 0, 0, 1, 0, 1, 1)),
        (86800, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 0, 0, 1, 0, 1, 1)),
        (86900, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 0, 1, 1, 0, 1, 1)),
        (87000, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 1, 1)),
        (87100, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1)),
        (87200, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1)),
        (87300, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 1, 0, 0, 0, 1, 1)),
        (87400, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 1)),
        (87500, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 0, 1, 0, 0, 1, 1)),
        (87600, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 1)),
        (87700, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 0, 0, 1, 0, 1, 1)),
        (87800, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 0, 0, 1, 0, 1, 1)),
        (87900, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1)),
        (88000, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 1)),
        (88100, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 1, 1, 0, 0, 1, 1)),
        (88200, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 1, 0, 0, 0, 1, 1)),
        (88300, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 1, 0, 0, 0, 1, 1)),
        (88400, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 1, 1, 0, 0, 1, 1)),
        (88500, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 0, 1, 0, 0, 1, 1)),
        (88600, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 0, 1, 1, 0, 1, 1)),
        (88700, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 0, 0, 1, 0, 1, 1)),
        (88800, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 0, 0, 1, 0, 1, 1)),
        (88900, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 0, 1, 1, 0, 1, 1)),
        (89000, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 0, 1, 0, 0, 1, 1)),
        (89100, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 1, 1, 0, 0, 1, 1)),
        (89200, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 1, 0, 0, 0, 1, 1)),
        (89300, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1)),
        (89400, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1)),
        (89500, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 0, 1, 0, 0, 1, 1)),
        (89600, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1)),
        (89700, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 0, 0, 1, 0, 1, 1)),
        (89800, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1)),
        (89900, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 1)),
        (90000, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 0, 1, 0, 0, 1, 1)),
        (90100, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 1, 1, 0, 0, 1, 1)),
        (90200, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 1, 0, 0, 0, 1, 1)),
        (90300, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 1, 0, 0, 0, 1, 1)),
        (90400, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1)),
        (90500, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 0, 1, 0, 0, 1, 1)),
        (90600, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 0, 1, 1, 0, 1, 1)),
        (90700, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 0, 0, 1, 0, 1, 1)),
        (90800, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 0, 0, 1, 0, 1, 1)),
        (90900, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 0, 1, 1, 0, 1, 1)),
        (91000, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 1)),
        (91100, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 1)),
        (91200, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1, 1)),
        (91300, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 1, 0, 0, 0, 1, 1)),
        (91400, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 1, 1, 0, 0, 1, 1)),
        (91500, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 0, 1, 0, 0, 1, 1)),
        (91600, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 0, 1, 1, 0, 1, 1)),
        (91700, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 0, 0, 1, 0, 1, 1)),
        (91800, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 1, 1)),
        (91900, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 0, 1, 1, 0, 1, 1)),
        (92000, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 0, 1, 0, 0, 1, 1)),
        (92100, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 1, 1, 0, 0, 1, 1)),
        (92200, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1)),
        (92300, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 1, 0, 0, 0, 1, 1)),
        (92400, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 1, 1, 0, 0, 1, 1)),
        (92500, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 1, 1)),
        (92600, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1)),
        (92700, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 1, 1)),
        (92800, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 1)),
        (92900, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1)),
        (93000, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1)),
        (93100, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1)),
        (93200, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1, 1)),
        (93300, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1)),
        (93400, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1)),
        (93500, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1)),
        (93600, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 0, 1, 1, 0, 1, 1)),
        (93700, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1)),
        (93800, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 0, 0, 1, 0, 1, 1)),
        (93900, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 0, 1, 1, 0, 1, 1)),
        (94000, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 1, 1)),
        (94100, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1)),
        (94200, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1)),
        (94300, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1)),
        (94400, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 1, 1, 0, 0, 1, 1)),
        (94500, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1)),
        (94600, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 0, 1, 1, 0, 1, 1)),
        (94700, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1)),
        (94800, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0)),
        (94900, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 0, 1, 1, 0, 1, 0)),
        (95000, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0)),
        (95100, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 1, 1, 0, 0, 1, 0)),
        (95200, new ModeCGillhamBits(1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0)),
        (95300, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 1, 0)),
        (95400, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 1, 0)),
        (95500, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 1, 0)),
        (95600, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 0, 1, 1, 0, 1, 0)),
        (95700, new ModeCGillhamBits(1, 0, 0, 0, 0, 1, 0, 0, 1, 0, 1, 0)),
        (95800, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 0, 0, 1, 0, 1, 0)),
        (95900, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 0, 1, 1, 0, 1, 0)),
        (96000, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 0, 1, 0, 0, 1, 0)),
        (96100, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 0)),
        (96200, new ModeCGillhamBits(1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1, 0)),
        (96300, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1, 0)),
        (96400, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 1, 1, 0, 0, 1, 0)),
        (96500, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0)),
        (96600, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 0)),
        (96700, new ModeCGillhamBits(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0)),
        (96800, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 1, 0)),
        (96900, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0)),
        (97000, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0)),
        (97100, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 1, 1, 0, 0, 1, 0)),
        (97200, new ModeCGillhamBits(1, 0, 0, 1, 1, 0, 1, 0, 0, 0, 1, 0)),
        (97300, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 0)),
        (97400, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 1, 1, 0, 0, 1, 0)),
        (97500, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 0, 1, 0, 0, 1, 0)),
        (97600, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 0, 1, 1, 0, 1, 0)),
        (97700, new ModeCGillhamBits(1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 1, 0)),
        (97800, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 0, 0, 1, 0, 1, 0)),
        (97900, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 0, 1, 1, 0, 1, 0)),
        (98000, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 0, 1, 0, 0, 1, 0)),
        (98100, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 1, 1, 0, 0, 1, 0)),
        (98200, new ModeCGillhamBits(1, 0, 0, 1, 0, 1, 1, 0, 0, 0, 1, 0)),
        (98300, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1, 0)),
        (98400, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0)),
        (98500, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 0)),
        (98600, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 0, 1, 1, 0, 1, 0)),
        (98700, new ModeCGillhamBits(1, 0, 0, 1, 0, 0, 0, 0, 1, 0, 1, 0)),
        (98800, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 0, 0, 1, 0, 1, 0)),
        (98900, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 0, 1, 1, 0, 1, 0)),
        (99000, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 0, 1, 0, 0, 1, 0)),
        (99100, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 0)),
        (99200, new ModeCGillhamBits(1, 0, 1, 1, 0, 0, 1, 0, 0, 0, 1, 0)),
        (99300, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 1, 0, 0, 0, 1, 0)),
        (99400, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 1, 1, 0, 0, 1, 0)),
        (99500, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 0, 1, 0, 0, 1, 0)),
        (99600, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 0)),
        (99700, new ModeCGillhamBits(1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 0)),
        (99800, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 0, 0, 1, 0, 1, 0)),
        (99900, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 0, 1, 1, 0, 1, 0)),
        (100000, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 0, 1, 0, 0, 1, 0)),
        (100100, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 1, 1, 0, 0, 1, 0)),
        (100200, new ModeCGillhamBits(1, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 0)),
        (100300, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 1, 0, 0, 0, 1, 0)),
        (100400, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 1, 1, 0, 0, 1, 0)),
        (100500, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 0, 1, 0, 0, 1, 0)),
        (100600, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 0, 1, 1, 0, 1, 0)),
        (100700, new ModeCGillhamBits(1, 0, 1, 1, 1, 0, 0, 0, 1, 0, 1, 0)),
        (100800, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 0, 0, 1, 0, 1, 0)),
        (100900, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 0, 1, 1, 0, 1, 0)),
        (101000, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 0, 1, 0, 0, 1, 0)),
        (101100, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 1, 1, 0, 0, 1, 0)),
        (101200, new ModeCGillhamBits(1, 0, 1, 0, 1, 0, 1, 0, 0, 0, 1, 0)),
        (101300, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 1, 0, 0, 0, 1, 0)),
        (101400, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 1, 1, 0, 0, 1, 0)),
        (101500, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0)),
        (101600, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 0, 1, 1, 0, 1, 0)),
        (101700, new ModeCGillhamBits(1, 0, 1, 0, 1, 1, 0, 0, 1, 0, 1, 0)),
        (101800, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 0, 0, 1, 0, 1, 0)),
        (101900, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0)),
        (102000, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 0, 1, 0, 0, 1, 0)),
        (102100, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0)),
        (102200, new ModeCGillhamBits(1, 0, 1, 0, 0, 1, 1, 0, 0, 0, 1, 0)),
        (102300, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0)),
        (102400, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 1, 0)),
        (102500, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 1, 0)),
        (102600, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 0, 1, 1, 0, 1, 0)),
        (102700, new ModeCGillhamBits(1, 0, 1, 0, 0, 0, 0, 0, 1, 0, 1, 0)),
        (102800, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 0, 0, 1, 0, 1, 0)),
        (102900, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 0, 1, 1, 0, 1, 0)),
        (103000, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 0, 1, 0, 0, 1, 0)),
        (103100, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 0)),
        (103200, new ModeCGillhamBits(1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0)),
        (103300, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1, 0)),
        (103400, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0)),
        (103500, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 0, 1, 0, 0, 1, 0)),
        (103600, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0)),
        (103700, new ModeCGillhamBits(1, 1, 1, 0, 0, 1, 0, 0, 1, 0, 1, 0)),
        (103800, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 0, 0, 1, 0, 1, 0)),
        (103900, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 0)),
        (104000, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0)),
        (104100, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 1, 1, 0, 0, 1, 0)),
        (104200, new ModeCGillhamBits(1, 1, 1, 0, 1, 1, 1, 0, 0, 0, 1, 0)),
        (104300, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 1, 0, 0, 0, 1, 0)),
        (104400, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 1, 1, 0, 0, 1, 0)),
        (104500, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 0, 1, 0, 0, 1, 0)),
        (104600, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 0, 1, 1, 0, 1, 0)),
        (104700, new ModeCGillhamBits(1, 1, 1, 0, 1, 0, 0, 0, 1, 0, 1, 0)),
        (104800, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 0, 0, 1, 0, 1, 0)),
        (104900, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 0, 1, 1, 0, 1, 0)),
        (105000, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 0, 1, 0, 0, 1, 0)),
        (105100, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 1, 1, 0, 0, 1, 0)),
        (105200, new ModeCGillhamBits(1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 1, 0)),
        (105300, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 0)),
        (105400, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 0)),
        (105500, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 0, 1, 0, 0, 1, 0)),
        (105600, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 0)),
        (105700, new ModeCGillhamBits(1, 1, 1, 1, 1, 1, 0, 0, 1, 0, 1, 0)),
        (105800, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 0, 0, 1, 0, 1, 0)),
        (105900, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 0, 1, 1, 0, 1, 0)),
        (106000, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 0, 1, 0, 0, 1, 0)),
        (106100, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 1, 1, 0, 0, 1, 0)),
        (106200, new ModeCGillhamBits(1, 1, 1, 1, 0, 1, 1, 0, 0, 0, 1, 0)),
        (106300, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 1, 0, 0, 0, 1, 0)),
        (106400, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 0)),
        (106500, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 0, 1, 0, 0, 1, 0)),
        (106600, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 0, 1, 1, 0, 1, 0)),
        (106700, new ModeCGillhamBits(1, 1, 1, 1, 0, 0, 0, 0, 1, 0, 1, 0)),
        (106800, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 0, 0, 1, 0, 1, 0)),
        (106900, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 0, 1, 1, 0, 1, 0)),
        (107000, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 0, 1, 0, 0, 1, 0)),
        (107100, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0)),
        (107200, new ModeCGillhamBits(1, 1, 0, 1, 0, 0, 1, 0, 0, 0, 1, 0)),
        (107300, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 1, 0)),
        (107400, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 1, 1, 0, 0, 1, 0)),
        (107500, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 0, 1, 0, 0, 1, 0)),
        (107600, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 0, 1, 1, 0, 1, 0)),
        (107700, new ModeCGillhamBits(1, 1, 0, 1, 0, 1, 0, 0, 1, 0, 1, 0)),
        (107800, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 0, 0, 1, 0, 1, 0)),
        (107900, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 0, 1, 1, 0, 1, 0)),
        (108000, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 0, 1, 0, 0, 1, 0)),
        (108100, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 1, 1, 0, 0, 1, 0)),
        (108200, new ModeCGillhamBits(1, 1, 0, 1, 1, 1, 1, 0, 0, 0, 1, 0)),
        (108300, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 1, 0, 0, 0, 1, 0)),
        (108400, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1, 0)),
        (108500, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0)),
        (108600, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0)),
        (108700, new ModeCGillhamBits(1, 1, 0, 1, 1, 0, 0, 0, 1, 0, 1, 0)),
        (108800, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0)),
        (108900, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 0, 1, 1, 0, 1, 0)),
        (109000, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0)),
        (109100, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 0)),
        (109200, new ModeCGillhamBits(1, 1, 0, 0, 1, 0, 1, 0, 0, 0, 1, 0)),
        (109300, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 0)),
        (109400, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 0)),
        (109500, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 0)),
        (109600, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 0, 1, 1, 0, 1, 0)),
        (109700, new ModeCGillhamBits(1, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 0)),
        (109800, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 0, 0, 1, 0, 1, 0)),
        (109900, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 0, 1, 1, 0, 1, 0)),
        (110000, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 0, 1, 0, 0, 1, 0)),
        (110100, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 1, 0)),
        (110200, new ModeCGillhamBits(1, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 0)),
        (110300, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0)),
        (110400, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1, 0)),
        (110500, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0)),
        (110600, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 0, 1, 1, 0, 1, 0)),
        (110700, new ModeCGillhamBits(1, 1, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0)),
        (110800, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0)),
        (110900, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 0, 1, 1, 0, 1, 0)),
        (111000, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0)),
        (111100, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1, 0)),
        (111200, new ModeCGillhamBits(0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0)),
        (111300, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 0)),
        (111400, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 1, 1, 0, 0, 1, 0)),
        (111500, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 1, 0)),
        (111600, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 0, 1, 1, 0, 1, 0)),
        (111700, new ModeCGillhamBits(0, 1, 0, 0, 0, 1, 0, 0, 1, 0, 1, 0)),
        (111800, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 0)),
        (111900, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 0, 1, 1, 0, 1, 0)),
        (112000, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 0)),
        (112100, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 0)),
        (112200, new ModeCGillhamBits(0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 0)),
        (112300, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 1, 0, 0, 0, 1, 0)),
        (112400, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 0)),
        (112500, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0)),
        (112600, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 0, 1, 1, 0, 1, 0)),
        (112700, new ModeCGillhamBits(0, 1, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0)),
        (112800, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 0, 0, 1, 0, 1, 0)),
        (112900, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0)),
        (113000, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0)),
        (113100, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1, 0)),
        (113200, new ModeCGillhamBits(0, 1, 0, 1, 1, 0, 1, 0, 0, 0, 1, 0)),
        (113300, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 1, 0, 0, 0, 1, 0)),
        (113400, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 1, 1, 0, 0, 1, 0)),
        (113500, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 0, 1, 0, 0, 1, 0)),
        (113600, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 0, 1, 1, 0, 1, 0)),
        (113700, new ModeCGillhamBits(0, 1, 0, 1, 1, 1, 0, 0, 1, 0, 1, 0)),
        (113800, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 0, 0, 1, 0, 1, 0)),
        (113900, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 0, 1, 1, 0, 1, 0)),
        (114000, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 0, 1, 0, 0, 1, 0)),
        (114100, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 1, 1, 0, 0, 1, 0)),
        (114200, new ModeCGillhamBits(0, 1, 0, 1, 0, 1, 1, 0, 0, 0, 1, 0)),
        (114300, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 1, 0, 0, 0, 1, 0)),
        (114400, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0)),
        (114500, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 0, 1, 0, 0, 1, 0)),
        (114600, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 0, 1, 1, 0, 1, 0)),
        (114700, new ModeCGillhamBits(0, 1, 0, 1, 0, 0, 0, 0, 1, 0, 1, 0)),
        (114800, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 0, 0, 1, 0, 1, 0)),
        (114900, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 0, 1, 1, 0, 1, 0)),
        (115000, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 0, 1, 0, 0, 1, 0)),
        (115100, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 0)),
        (115200, new ModeCGillhamBits(0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 1, 0)),
        (115300, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 1, 0, 0, 0, 1, 0)),
        (115400, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 1, 1, 0, 0, 1, 0)),
        (115500, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 0, 1, 0, 0, 1, 0)),
        (115600, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 0, 1, 1, 0, 1, 0)),
        (115700, new ModeCGillhamBits(0, 1, 1, 1, 0, 1, 0, 0, 1, 0, 1, 0)),
        (115800, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 0, 0, 1, 0, 1, 0)),
        (115900, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 0)),
        (116000, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 0, 1, 0, 0, 1, 0)),
        (116100, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 0)),
        (116200, new ModeCGillhamBits(0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 0)),
        (116300, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 1, 0, 0, 0, 1, 0)),
        (116400, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 1, 1, 0, 0, 1, 0)),
        (116500, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 0, 1, 0, 0, 1, 0)),
        (116600, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 1, 0)),
        (116700, new ModeCGillhamBits(0, 1, 1, 1, 1, 0, 0, 0, 1, 0, 1, 0)),
        (116800, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 0, 0, 1, 0, 1, 0)),
        (116900, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 1, 0)),
        (117000, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 0, 1, 0, 0, 1, 0)),
        (117100, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 1, 0)),
        (117200, new ModeCGillhamBits(0, 1, 1, 0, 1, 0, 1, 0, 0, 0, 1, 0)),
        (117300, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 1, 0, 0, 0, 1, 0)),
        (117400, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 1, 1, 0, 0, 1, 0)),
        (117500, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0)),
        (117600, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 0)),
        (117700, new ModeCGillhamBits(0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 1, 0)),
        (117800, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 1, 0)),
        (117900, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0)),
        (118000, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 0, 1, 0, 0, 1, 0)),
        (118100, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0)),
        (118200, new ModeCGillhamBits(0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1, 0)),
        (118300, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0)),
        (118400, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 0)),
        (118500, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 1, 0)),
        (118600, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 1, 0)),
        (118700, new ModeCGillhamBits(0, 1, 1, 0, 0, 0, 0, 0, 1, 0, 1, 0)),
        (118800, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 1, 0)),
        (118900, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 1, 0)),
        (119000, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 1, 0)),
        (119100, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 1, 0)),
        (119200, new ModeCGillhamBits(0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0)),
        (119300, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 1, 0, 0, 0, 1, 0)),
        (119400, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0)),
        (119500, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 0, 1, 0, 0, 1, 0)),
        (119600, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0)),
        (119700, new ModeCGillhamBits(0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 1, 0)),
        (119800, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 0, 0, 1, 0, 1, 0)),
        (119900, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 0, 1, 1, 0, 1, 0)),
        (120000, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0)),
        (120100, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 1, 1, 0, 0, 1, 0)),
        (120200, new ModeCGillhamBits(0, 0, 1, 0, 1, 1, 1, 0, 0, 0, 1, 0)),
        (120300, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 1, 0)),
        (120400, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 1, 1, 0, 0, 1, 0)),
        (120500, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 0, 1, 0, 0, 1, 0)),
        (120600, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 0, 1, 1, 0, 1, 0)),
        (120700, new ModeCGillhamBits(0, 0, 1, 0, 1, 0, 0, 0, 1, 0, 1, 0)),
        (120800, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 0, 0, 1, 0, 1, 0)),
        (120900, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 0, 1, 1, 0, 1, 0)),
        (121000, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 1, 0)),
        (121100, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 1, 1, 0, 0, 1, 0)),
        (121200, new ModeCGillhamBits(0, 0, 1, 1, 1, 0, 1, 0, 0, 0, 1, 0)),
        (121300, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 1, 0)),
        (121400, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 1, 0)),
        (121500, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 0, 1, 0, 0, 1, 0)),
        (121600, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 0, 1, 1, 0, 1, 0)),
        (121700, new ModeCGillhamBits(0, 0, 1, 1, 1, 1, 0, 0, 1, 0, 1, 0)),
        (121800, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 0)),
        (121900, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 0)),
        (122000, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 0, 1, 0, 0, 1, 0)),
        (122100, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 1, 1, 0, 0, 1, 0)),
        (122200, new ModeCGillhamBits(0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 1, 0)),
        (122300, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 1, 0)),
        (122400, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 0)),
        (122500, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 1, 0)),
        (122600, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 0, 1, 1, 0, 1, 0)),
        (122700, new ModeCGillhamBits(0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 1, 0)),
        (122800, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 1, 0)),
        (122900, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 1, 0)),
        (123000, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 0)),
        (123100, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0)),
        (123200, new ModeCGillhamBits(0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1, 0)),
        (123300, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 1, 0, 0, 0, 1, 0)),
        (123400, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 1, 1, 0, 0, 1, 0)),
        (123500, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 0, 1, 0, 0, 1, 0)),
        (123600, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 0, 1, 1, 0, 1, 0)),
        (123700, new ModeCGillhamBits(0, 0, 0, 1, 0, 1, 0, 0, 1, 0, 1, 0)),
        (123800, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 0, 0, 1, 0, 1, 0)),
        (123900, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 0, 1, 1, 0, 1, 0)),
        (124000, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 0, 1, 0, 0, 1, 0)),
        (124100, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 1, 0)),
        (124200, new ModeCGillhamBits(0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 0)),
        (124300, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 1, 0, 0, 0, 1, 0)),
        (124400, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 1, 0)),
        (124500, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0)),
        (124600, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0)),
        (124700, new ModeCGillhamBits(0, 0, 0, 1, 1, 0, 0, 0, 1, 0, 1, 0)),
        (124800, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0)),
        (124900, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 0)),
        (125000, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0)),
        (125100, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 1, 1, 0, 0, 1, 0)),
        (125200, new ModeCGillhamBits(0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1, 0)),
        (125300, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 1, 0)),
        (125400, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 0)),
        (125500, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 0, 1, 0, 0, 1, 0)),
        (125600, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 0, 1, 1, 0, 1, 0)),
        (125700, new ModeCGillhamBits(0, 0, 0, 0, 1, 1, 0, 0, 1, 0, 1, 0)),
        (125800, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 1, 0)),
        (125900, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 0, 1, 1, 0, 1, 0)),
        (126000, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 1, 0)),
        (126100, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 1, 0)),
        (126200, new ModeCGillhamBits(0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 1, 0)),
        (126300, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0)),
        (126400, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 1, 0)),
        (126500, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0)),
        (126600, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 1, 0)),
        (126700, new ModeCGillhamBits(0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0))
    ];


}