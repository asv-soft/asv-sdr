using System;
using Asv.IO;

namespace Asv.Sdr;

public abstract class AdsbAirbornePosition : AdsbExtendedSquitterBase
{
    public uint NCprLat { get; set; }
    public uint NCprLon { get; set; }

    public double Latitude { get; set; } = double.NaN;

    public double Longitude { get; set; } = double.NaN;

    public double Altitude { get; set; } = double.NaN;

    public CprFormatEnum CprFormat { get; set; }

    public SurveillanceStatusEnum SurveillanceStatus { get; set; }
    public bool IsSingleAntenna { get; set; } = false;
    private uint Time { get; set; }

    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        base.InternalDeserialize(ref buffer);
        var bitIndex = 0;
        SetAirbornePositionType(SpanBitHelper.GetBitU(buffer, ref bitIndex, 5));
        SurveillanceStatus = (SurveillanceStatusEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        IsSingleAntenna = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 1;
        var alt = SpanBitHelper.GetBitU(buffer, ref bitIndex, 12);
        Altitude = GetAltitude(alt);
        Time = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1);
        CprFormat = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 0 ? CprFormatEnum.Even : CprFormatEnum.Odd;
        NCprLat = SpanBitHelper.GetBitU(buffer, ref bitIndex, 17);
        NCprLon = SpanBitHelper.GetBitU(buffer, ref bitIndex, 17);
        buffer = buffer[(bitIndex / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var bitIndex = 0;
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 5, GetAirbornePositionType());
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, (uint)SurveillanceStatus);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, IsSingleAntenna ? 1 : 0);
        var nAlt = SetAltitude(Altitude);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 12, nAlt);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, Time);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, (uint)CprFormat);
        if (!double.IsNaN(Latitude) && !double.IsNaN(Longitude))
        {
            var pos = TransponderHelper.UnambiguousPositionEncoding(Latitude, Longitude, CprFormat);
            NCprLat = pos.nCprLat;
            NCprLon = pos.nCprLon;
        }
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 17, NCprLat);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 17, NCprLon);
        buffer = buffer[(bitIndex / 8)..];
    }

    public void CalculatePosition(AdsbAirbornePosition prevPosition)
    {
        if (CprFormat == prevPosition.CprFormat) return;

        if (CprFormat == CprFormatEnum.Even)
        {
            var pos = TransponderHelper.GloballyUnambiguousPositionDecoding(NCprLat, NCprLon, prevPosition.NCprLat,
                prevPosition.NCprLon, DateTime.Now, DateTime.Now.AddSeconds(-1));
            Latitude = pos.Lat;
            Longitude = pos.Lon;
        }
        else
        {
            var pos = TransponderHelper.GloballyUnambiguousPositionDecoding(prevPosition.NCprLat, prevPosition.NCprLon,
                NCprLat, NCprLon, DateTime.Now.AddSeconds(-1), DateTime.Now);
            Latitude = pos.Lat;
            Longitude = pos.Lon;
        }
    }

    #region Common

    protected abstract double GetAltitude(uint nAlt);
    protected abstract uint SetAltitude(double alt);
    
    protected abstract void SetAirbornePositionType(uint nMsgType);
    protected abstract uint GetAirbornePositionType();
    

    #endregion
    
}

public class AdsbAirbornePositionWithBaroAlt : AdsbAirbornePosition
{
    public override AdsbMessageTypeEnum MessageType => AdsbMessageTypeEnum.AirborneBarometricPosition;
    
    /// <summary>
    /// When Type Code is from 9 to 18, the encoded altitude represents the barometric altitude of the aircraft.
    /// </summary>
    public AirbornePositionBaroAltTypeCode AirbornePositionType { get; set; } = AirbornePositionBaroAltTypeCode.BasicPositionHighUpdateRate;

    protected override void SetAirbornePositionType(uint nMsgType)
    {
        AirbornePositionType = (AirbornePositionBaroAltTypeCode)nMsgType;
    }

    protected override uint GetAirbornePositionType()
    {
        return (uint)AirbornePositionType;
    }

    protected override double GetAltitude(uint nAlt)
    {
        if (nAlt == 0) return double.NaN;
        var q = (nAlt & QBitMask) != 0;
        if (q)
        {
            // ALT bits: [N10..N4] [Q] [N3..N0]
            // Remove Q bit and reconstruct 11-bit N.
            var n = ((nAlt >> 1) & 0x07F0) |
                    (nAlt & 0x000F);
            return (n * 25.0 - 1000.0) / 3.28084;
        }
        // Q = 0 is not plain binary * 100 ft.
        // It is Gillham / Mode C modified Gray code.
        return DecodeGillhamAltitude12(nAlt);
    }

    protected override uint SetAltitude(double alt)
    {
        if (double.IsNaN(alt)) return 0;

        alt *= 3.28084;
        var altNorm = alt switch
        {
            > 126700.0 => 126700,
            > 50187.5 => (int)Math.Round(alt / 100.0, 0) * 100,
            < -1200.0 => -1200,
            < -1000.0 => (int)Math.Round(alt / 100.0, 0) * 100,
            _ => (int)Math.Round(alt / 25.0) * 25
        };

        return altNorm is <= 50175 and >= -1000
            ? EncodeBarometricAltitudeQ1(altNorm)
            : EncodeBarometricAltitudeGillhamQ0(altNorm);
    }

    #region Gray code

    // Функция для преобразования двоичного числа в код Грея
    private static int BinaryToGray(int binary)
    {
        return binary ^ (binary >> 1);
    }

    // Функция для преобразования числа в коде Грея обратно в двоичный код
    private static int GrayToBinary(uint gray)
    {
        var binary = 0;

        for (; gray != 0; gray >>= 1)
            binary = (int)(binary ^ gray);

        return binary;
    }
    
    #endregion

    #region Encode/Decode Altitude

    private const int AltitudeFieldMask = 0x0FFF; // 12 bits
    private const int QBitMask = 0x0010;          // Q = 8-й бит от MSB, т.е. bit 4 от LSB

    /// <summary>
    /// Encode altitude using Q=1 format: 25 ft resolution.
    /// Valid range: -1000 .. 50175 ft.
    /// </summary>
    private static uint EncodeBarometricAltitudeQ1(int altitudeFt)
    {
        if (altitudeFt is < -1000 or > 50175)
            throw new ArgumentOutOfRangeException(
                nameof(altitudeFt),
                "Q=1 ADS-B altitude range is -1000..50175 ft.");

        var shifted = altitudeFt + 1000;

        if (shifted % 25 != 0)
            throw new ArgumentException(
                "For Q=1 encoding altitude must be a multiple of 25 ft after adding 1000 ft.",
                nameof(altitudeFt));

        var n = shifted / 25; // 11-bit value

        // Insert Q bit after the upper 7 bits of N:
        // N: [N10..N4] [N3..N0]
        // ALT: [N10..N4] [Q] [N3..N0]
        return (uint)(((n & 0x07F0) << 1) | QBitMask | (n & 0x000F));
    }

    /// <summary>
    /// Encode altitude using Q=0 Gillham / Mode C format: 100 ft resolution.
    /// Normally use Q=1 unless you explicitly need legacy Gillham encoding.
    /// Valid practical Gillham range here: -1200 .. 126700 ft.
    /// </summary>
    private static uint EncodeBarometricAltitudeGillhamQ0(int altitudeFt)
    {
        if (altitudeFt is < -1200 or > 126700)
            throw new ArgumentOutOfRangeException(
                nameof(altitudeFt),
                "Gillham altitude range is -1200..126700 ft.");

        if ((altitudeFt + 1300) % 100 != 0)
            throw new ArgumentException(
                "For Q=0 Gillham encoding altitude must be representable in 100 ft increments.",
                nameof(altitudeFt));

        // From decoding formula:
        // altitude = 500 * N500 + 100 * N100 - 1300
        // N100 is 1..5, not 0..4.
        var x = (altitudeFt + 1300) / 100;

        var n500 = (x - 1) / 5;
        var n100 = x - n500 * 5; // 1..5

        if (n500 < 0 || n500 > 255 || n100 < 1 || n100 > 5)
            throw new ArgumentOutOfRangeException(nameof(altitudeFt));

        // Gillham 100-ft digit is mirrored on odd 500-ft intervals.
        var n100ForCode = ((n500 & 1) != 0) ? 6 - n100 : n100;

        // In Gillham decoding, value 7 is remapped to 5.
        // Values 5 and 6 are invalid before remap, so encode logical 5 as 7.
        if (n100ForCode == 5)
            n100ForCode = 7;

        var n500Gray = BinaryToGray(n500) & 0xFF;
        var n100Gray = BinaryToGray(n100ForCode) & 0x07;

        // Gillham order:
        // D2 D4 A1 A2 A4 B1 B2 B4 C1 C2 C4
        var d2 = (n500Gray >> 7) & 1;
        var d4 = (n500Gray >> 6) & 1;
        var a1 = (n500Gray >> 5) & 1;
        var a2 = (n500Gray >> 4) & 1;
        var a4 = (n500Gray >> 3) & 1;
        var b1 = (n500Gray >> 2) & 1;
        var b2 = (n500Gray >> 1) & 1;
        var b4 = n500Gray & 1;

        var c1 = (n100Gray >> 2) & 1;
        var c2 = (n100Gray >> 1) & 1;
        var c4 = n100Gray & 1;

        // ADS-B ALT12 order for Q=0:
        // C1 A1 C2 A2 C4 A4 B1 Q B2 D2 B4 D4
        // Q = 0
        return (uint)((c1 << 11) |
                      (a1 << 10) |
                      (c2 << 9)  |
                      (a2 << 8)  |
                      (c4 << 7)  |
                      (a4 << 6)  |
                      (b1 << 5)  |
                      0 |
                      (b2 << 3)  |
                      (d2 << 2)  |
                      (b4 << 1)  |
                      d4);
    }

    private static double DecodeGillhamAltitude12(uint altitudeField12)
    {
        // ADS-B ALT12 order:
        // C1 A1 C2 A2 C4 A4 B1 Q B2 D2 B4 D4
        var c1 = Bit(altitudeField12, 11);
        var a1 = Bit(altitudeField12, 10);
        var c2 = Bit(altitudeField12, 9);
        var a2 = Bit(altitudeField12, 8);
        var c4 = Bit(altitudeField12, 7);
        var a4 = Bit(altitudeField12, 6);
        var b1 = Bit(altitudeField12, 5);
        var b2 = Bit(altitudeField12, 3);
        var d2 = Bit(altitudeField12, 2);
        var b4 = Bit(altitudeField12, 1);
        var d4 = Bit(altitudeField12, 0);

        // Rearranged Gillham order:
        // D2 D4 A1 A2 A4 B1 B2 B4 C1 C2 C4
        var n500Gray =
            (d2 << 7) |
            (d4 << 6) |
            (a1 << 5) |
            (a2 << 4) |
            (a4 << 3) |
            (b1 << 2) |
            (b2 << 1) |
            b4;

        var n100Gray =
            (c1 << 2) |
            (c2 << 1) |
            c4;

        var n500 = GrayToBinary(n500Gray);
        var n100 = GrayToBinary(n100Gray);

        // Invalid Gillham 100-ft digit values.
        if (n100 == 0 || n100 == 5 || n100 == 6)
            return double.NaN;

        if (n100 == 7)
            n100 = 5;

        if ((n500 & 1) != 0)
            n100 = 6 - n100;

        return 500 * n500 + 100 * n100 - 1300;
    }
    
    private static uint Bit(uint value, int bitIndex)
    {
        return (value >> bitIndex) & 1;
    }
    
    #endregion
    
}

public class AdsbAirbornePositionWithGnssAlt : AdsbAirbornePosition
{
    public override AdsbMessageTypeEnum MessageType => AdsbMessageTypeEnum.AirborneGnssPosition;
    
    /// <summary>
    /// When the Type Code is from 20 to 22, the encoded altitude contains the GNSS altitude of the aircraft.
    /// </summary>
    public AirbornePositionGnssAltTypeCode AirbornePositionType { get; set; } = AirbornePositionGnssAltTypeCode.BasicGnssPosition;
    
    protected override void SetAirbornePositionType(uint nMsgType)
    {
        AirbornePositionType = (AirbornePositionGnssAltTypeCode)nMsgType;
    }

    protected override uint GetAirbornePositionType()
    {
        return (uint)AirbornePositionType;
    }

    protected override double GetAltitude(uint nAlt)
    {
        return nAlt * 6.25 - 1000.0;
    }

    protected override uint SetAltitude(double alt)
    {
        if (alt > 24593.75) alt = 24593.75;
        if (alt < -1000.0) alt = -1000.0;

        return (uint)Math.Round((alt + 1000.0) / 6.25, 0);
    }
}


