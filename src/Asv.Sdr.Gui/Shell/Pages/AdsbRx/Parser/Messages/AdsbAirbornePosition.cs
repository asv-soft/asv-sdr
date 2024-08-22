using System;
using Asv.IO;

namespace Asv.Sdr.Gui;

public class AdsbAirbornePosition : AdsbExtendedSquitterBase
{

    public uint NCprLat { get; set; }
    public uint NCprLon { get; set; }

    public AltitudeTypeEnum AltitudeType => (int)AirbornePositionType is >= 9 and <= 18 ? AltitudeTypeEnum.Barometric : AltitudeTypeEnum.Gnss;

    public double Latitude { get; set; } = double.NaN;

    public double Longitude { get; set; } = double.NaN;

    public double Altitude { get; set; } = double.NaN;

    public CprFormatEnum CprFormat { get; set; }

    /// <summary>
    /// When Type Code is from 9 to 18, the encoded altitude represents the barometric altitude of the aircraft.
    /// When the Type Code is from 20 to 22, the encoded altitude contains the GNSS altitude of the aircraft.
    /// </summary>
    public AirbornePositionTypeCode AirbornePositionType { get; set; } = AirbornePositionTypeCode.BasicGnssPosition;

    private SurveillanceStatusEnum SurveillanceStatus { get; set; }
    public bool IsSingleAntenna { get; set; } = false;
    private uint Time { get; set; }

    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        var bitIndex = 0;
        AirbornePositionType = (AirbornePositionTypeCode)SpanBitHelper.GetBitU(buffer, ref bitIndex, 5);
        SurveillanceStatus = (SurveillanceStatusEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        IsSingleAntenna = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 1;
        var alt = SpanBitHelper.GetBitU(buffer, ref bitIndex, 12);
        Altitude = AltitudeType == AltitudeTypeEnum.Barometric ? GetBaroAltitude(alt) : GetGnssAltitude(alt);
        Time = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1);
        CprFormat = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 0 ? CprFormatEnum.Even : CprFormatEnum.Odd;
        NCprLat = SpanBitHelper.GetBitU(buffer, ref bitIndex, 17);
        NCprLon = SpanBitHelper.GetBitU(buffer, ref bitIndex, 17);
        buffer = buffer[(bitIndex / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var bitIndex = 0;
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 5, (uint)AirbornePositionType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, (uint)SurveillanceStatus);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, IsSingleAntenna ? 1 : 0);
        var nAlt = AltitudeType == AltitudeTypeEnum.Barometric ? SetBaroAltitude(Altitude) : SetGnssAltitude(Altitude);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 12, nAlt);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, Time);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, (uint)CprFormat);
        if (!double.IsNaN(Latitude) && !double.IsNaN(Longitude))
        {
            var pos = AdsbHelper.UnambiguousPositionEncoding(Latitude, Longitude, CprFormat);
            NCprLat = pos.Lat;
            NCprLon = pos.Lon;
        }
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 17, NCprLat);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 17, NCprLon);
        buffer = buffer[(bitIndex / 8)..];
    }

    public override TypeCodeEnum TypeCode => (int)AirbornePositionType switch
    {
        >= 9 and <= 18 => TypeCodeEnum.AirborneBarometricPosition,
        >= 20 and <= 22 => TypeCodeEnum.AirborneGnssPosition,
        _ => TypeCodeEnum.Reserved
    };

    public void CalculatePosition(AdsbAirbornePosition prevPosition)
    {
        if (CprFormat == prevPosition.CprFormat) return;

        if (CprFormat == CprFormatEnum.Even)
        {
            var pos = AdsbHelper.GloballyUnambiguousPositionDecoding(NCprLat, NCprLon, prevPosition.NCprLat,
                prevPosition.NCprLon, DateTime.Now, DateTime.Now.AddSeconds(-1));
            Latitude = pos.Lat;
            Longitude = pos.Lon;
        }
        else
        {
            var pos = AdsbHelper.GloballyUnambiguousPositionDecoding(prevPosition.NCprLat, prevPosition.NCprLon,
                NCprLat, NCprLon, DateTime.Now.AddSeconds(-1), DateTime.Now);
            Latitude = pos.Lat;
            Longitude = pos.Lon;
        }
    }

    #region Common

    // Функция для преобразования двоичного числа в код Грея
    private static int BinaryToGray(int binary)
    {
        return binary ^ (binary >> 1);
    }

    // Функция для преобразования числа в коде Грея обратно в двоичный код
    private static int GrayToBinary(int gray)
    {
        var binary = gray;
        var shift = 1;
        while ((gray >> shift) > 0)
        {
            binary ^= gray >> shift;
            shift++;
        }
        return binary;
    }
    private static double GetBaroAltitude(uint nAlt)
    {
        if (nAlt == 0) return double.NaN;
        var q = (nAlt & 0x10) != 0 ? 1 : 0;
        nAlt = ((nAlt & 0xFE0) >> 1) | (nAlt & 0xF);
        if (q == 1)
        {
            return (nAlt * 25.0 - 1000.0) / 3.28084;
        }
        return GrayToBinary((int)nAlt) * 100.0 / 3.28084;
    }

    private static uint SetBaroAltitude(double alt)
    {
        if (double.IsNaN(alt)) return 0;

        alt *= 3.28084;
        var altNorm = alt switch
        {
            > 50187.5 => (int)Math.Round(alt / 100.0, 0) * 100,
            < -1000.0 => -1000,
            _ => (int)Math.Round(alt / 25.0) * 25
        };

        if (altNorm > 50175)
        {
            altNorm /= 100;
            var nAlt = BinaryToGray(altNorm);
            return (uint)(((nAlt & 0x7F0) << 1) | (nAlt & 0xF));
        }

        altNorm = (altNorm + 1000) / 25;
        return (uint)(((altNorm & 0x7F0) << 1) | 0x10 | (altNorm & 0xF));
    }
    
    private static double GetGnssAltitude(uint nAlt)
    {
        return nAlt * 6.25 - 1000.0;
    }
    
    private static uint SetGnssAltitude(double alt)
    {
        if (alt > 24593.75) alt = 24593.75;
        if (alt < -1000.0) alt = -1000.0;

        return (uint)Math.Round((alt + 1000.0) / 6.25, 0);
    }

    #endregion
    
}
