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

    private SurveillanceStatusEnum SurveillanceStatus { get; set; }
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
            NCprLat = pos.Lat;
            NCprLon = pos.Lon;
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
        var q = (nAlt & 0x10) != 0 ? 1 : 0;
        nAlt = ((nAlt & 0xFE0) >> 1) | (nAlt & 0xF);
        if (q == 1)
        {
            return (nAlt * 25.0 - 1000.0) / 3.28084;
        }
        return GrayToBinary((int)nAlt) * 100.0 / 3.28084;
    }

    protected override uint SetAltitude(double alt)
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

    #region Gray code

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


