using System;
using Asv.IO;

namespace Asv.Sdr;


public abstract class AdsbAirborneVelocityBase : AdsbExtendedSquitterBase
{
    public VelocitySubTypeEnum SubType { get; set; }
    public bool IntentChangeFlag { get; set; }
    public bool IFRCapabilityFlag { get; set; }
    public NavigationUncertaintyCategoryEnum NavigationUncertaintyCategory { get; set; }
    public VerticalRateSourceEnum VrSrc { get; set; }
    public double VerticalRate { get; set; }
    public double GnssBaroAltDiff { get; set; }
    
    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        base.InternalDeserialize(ref buffer);
        var bitIndex = 5;
        SubType = (VelocitySubTypeEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);
        IntentChangeFlag = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 1;
        IFRCapabilityFlag = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 1;
        NavigationUncertaintyCategory =
            (NavigationUncertaintyCategoryEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);
        ReadVelocityData(buffer, ref bitIndex, SubType);
        VrSrc = (VerticalRateSourceEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 1);
        var svt = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 0 ? 1 : -1;
        VerticalRate = svt * GetVerticalRate(SpanBitHelper.GetBitU(buffer, ref bitIndex, 9)); 
        bitIndex += 2;
        var sDiff = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 0 ? 1 : -1;
        GnssBaroAltDiff = sDiff * GetGnssBaroAltDiff(SpanBitHelper.GetBitU(buffer, ref bitIndex, 7));
        buffer = buffer[(bitIndex / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var bitIndex = 0;
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 5, (uint)MessageType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, (uint)SubType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, IntentChangeFlag ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, IFRCapabilityFlag ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, (uint)NavigationUncertaintyCategory);
        WriteVelocityData(buffer, ref bitIndex, SubType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, VrSrc == VerticalRateSourceEnum.Gnss ? 0 : 1);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, VerticalRate < 0 ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 9, SetVerticalRate(VerticalRate));
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, GnssBaroAltDiff < 0 ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 7, SetGnssBaroAltDiff(GnssBaroAltDiff));
        buffer = buffer[(bitIndex / 8)..];
    }

    protected abstract void ReadVelocityData(ReadOnlySpan<byte> buffer, ref int pos, VelocitySubTypeEnum subType);
    protected abstract void WriteVelocityData(Span<byte> buffer, ref int pos, VelocitySubTypeEnum subType);

    public override AdsbMessageTypeEnum MessageType => AdsbMessageTypeEnum.AirborneVelocities;

    #region Common

    private static double GetVerticalRate(uint rateBits)
    {
        if (rateBits == 0) return double.NaN;
        return (rateBits - 1) * 64 * 0.00508; // ft/min => m/s
    }
    
    private static uint SetVerticalRate(double rate)
    {
        if (double.IsNaN(rate)) return 0;
        rate = Math.Abs(rate);
        var rateBits = (uint)Math.Round(rate / (64 * 0.00508) + 1, 0); // m/s => ft/min
        return rateBits > 511 ? 511 : rateBits;
    }

    private static double GetGnssBaroAltDiff(uint diffBits)
    {
        if (diffBits == 0) return double.NaN;
        return (diffBits - 1) * 25.0 * 0.3048; // ft => m
    }
    
    private static uint SetGnssBaroAltDiff(double diff)
    {
        if (double.IsNaN(diff)) return 0;
        diff = Math.Abs(diff);
        var diffBits = (uint)Math.Round(diff / (25.0 * 0.3048) + 1, 0);
        return diffBits > 127 ? 127 : diffBits;
    }

    #endregion
    
    
}

public class AdsbGroundSpeed : AdsbAirborneVelocityBase
{
    public double GroundSpeed { get; set; }
    
    public double GroundTrackAngle { get; set; }
    protected override void ReadVelocityData(ReadOnlySpan<byte> buffer, ref int pos, VelocitySubTypeEnum subType)
    {
        var ewVelocityCoef = SpanBitHelper.GetBitU(buffer, ref pos, 1) == 0 ? 1.0 : -1.0;
        var ewVelocityBits = SpanBitHelper.GetBitU(buffer, ref pos, 10);
        ewVelocityCoef *= (subType == VelocitySubTypeEnum.SubType1 ? 1.0 : 4.0);
        var vx = ewVelocityBits == 0 ? double.NaN : ewVelocityCoef * (ewVelocityBits - 1) * 0.51444; // knots => m/s
        
        var nsVelocityCoef = SpanBitHelper.GetBitU(buffer, ref pos, 1) == 0 ? 1.0 : -1.0;
        var nsVelocityBits = SpanBitHelper.GetBitU(buffer, ref pos, 10);
        nsVelocityCoef *= (subType == VelocitySubTypeEnum.SubType1 ? 1.0 : 4.0);
        var vy = nsVelocityBits == 0 ? double.NaN : nsVelocityCoef * (nsVelocityBits - 1) * 0.51444; // knots => m/s

        GroundSpeed = Math.Sqrt(vx * vx + vy * vy);
        GroundTrackAngle = (Math.Atan2(vx, vy) * 180.0 / Math.PI) % 360.0;
    }

    protected override void WriteVelocityData(Span<byte> buffer, ref int pos, VelocitySubTypeEnum subType)
    {
        if (double.IsNaN(GroundSpeed) || double.IsNaN(GroundTrackAngle))
        {
            SpanBitHelper.SetBitU(buffer, ref pos, 22, 0);
            return;
        }

        var angle = (GroundTrackAngle % 360.0) * Math.PI / 180.0;
        var vx = GroundSpeed * Math.Sin(angle) / 0.51444;
        var vy = GroundSpeed * Math.Cos(angle) / 0.51444;
        
        SpanBitHelper.SetBitU(buffer, ref pos, 1, vx < 0.0 ? 1 : 0);
        vx = Math.Abs(vx);
        if (subType == VelocitySubTypeEnum.SubType2) vx /= 4.0;
        var ewVelocityBits = (uint)Math.Round(vx, 0) + 1;
        ewVelocityBits = ewVelocityBits > 1023 ? 1023 : ewVelocityBits;
        SpanBitHelper.SetBitU(buffer, ref pos, 10, ewVelocityBits);
        
        SpanBitHelper.SetBitU(buffer, ref pos, 1, vy < 0.0 ? 1 : 0);
        vy = Math.Abs(vy);
        if (subType == VelocitySubTypeEnum.SubType2) vy /= 4.0;
        var nsVelocityBits = (uint)Math.Round(vy, 0) + 1;
        nsVelocityBits = nsVelocityBits > 1023 ? 1023 : nsVelocityBits;
        SpanBitHelper.SetBitU(buffer, ref pos, 10, nsVelocityBits);
    }

    public override ushort Id => (ushort)(base.Id | (ushort)VelocitySubTypeEnum.SubType1);
}

public class AdsbAirspeed : AdsbAirborneVelocityBase
{
    public double MagneticHeading { get; set; }
    public AirspeedTypeEnum AirspeedType { get; set; }
    public double Airspeed { get; set; }
    
    
    protected override void ReadVelocityData(ReadOnlySpan<byte> buffer, ref int pos, VelocitySubTypeEnum subType)
    {
        if (SpanBitHelper.GetBitU(buffer, ref pos, 1) == 0)
        {
            MagneticHeading = double.NaN;
            pos += 10;
        }
        else
        {
            MagneticHeading = (SpanBitHelper.GetBitU(buffer, ref pos, 10) * 360.0 / 1024.0) % 360.0;
        }

        AirspeedType = SpanBitHelper.GetBitU(buffer, ref pos, 1) == 0 ? AirspeedTypeEnum.IAS : AirspeedTypeEnum.TAS;
        var coef = subType == VelocitySubTypeEnum.SubType3 ? 1.0 : 4.0;

        Airspeed = coef * (SpanBitHelper.GetBitU(buffer, ref pos, 1) - 1) * 0.51444; // knots => m/s

    }

    protected override void WriteVelocityData(Span<byte> buffer, ref int pos, VelocitySubTypeEnum subType)
    {
        if (double.IsNaN(MagneticHeading))
        {
            SpanBitHelper.SetBitU(buffer, ref pos, 11, 0);
        }
        else
        {
            SpanBitHelper.SetBitU(buffer, ref pos, 1, 1);
            var mhBits = (uint)Math.Abs(Math.Round((MagneticHeading % 360.0) * 1024.0 / 360.0, 0));
            mhBits = mhBits > 1023 ? 1023 : mhBits;
            SpanBitHelper.SetBitU(buffer, ref pos, 10, mhBits);
        }
        
        SpanBitHelper.SetBitU(buffer, ref pos, 1, AirspeedType == AirspeedTypeEnum.IAS ? 0 : 1);
        var coef = subType == VelocitySubTypeEnum.SubType3 ? 1.0 : 4.0;

        var asBits = Math.Round(Airspeed / (0.51444 * coef) + 1, 0);
        asBits = asBits > 1023 ? 1023 : asBits;
        SpanBitHelper.SetBitU(buffer, ref pos, 10, asBits);
    }

    public override ushort Id => (ushort)(base.Id | (ushort)VelocitySubTypeEnum.SubType3);
}