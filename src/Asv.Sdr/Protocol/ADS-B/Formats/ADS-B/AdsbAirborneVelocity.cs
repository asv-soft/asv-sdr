using System;
using Asv.IO;

namespace Asv.Sdr;


public abstract class AdsbAirborneVelocityBase : AdsbExtendedSquitterBase
{
    private byte _navigationAccuracyCategoryVelocityCode;

    public VelocitySubTypeEnum SubType { get; set; }
    public bool IntentChangeFlag { get; set; }
    public bool IFRCapabilityFlag { get; set; }
    public byte NavigationAccuracyCategoryVelocityCode
    {
        get => _navigationAccuracyCategoryVelocityCode;
        set => _navigationAccuracyCategoryVelocityCode = value <= 7
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "NACv must fit in three bits.");
    }

    public NavigationUncertaintyCategoryEnum NavigationUncertaintyCategory
    {
        get => (NavigationUncertaintyCategoryEnum)_navigationAccuracyCategoryVelocityCode;
        set => _navigationAccuracyCategoryVelocityCode = (byte)value;
    }

    public VerticalRateSourceEnum VrSrc { get; set; }
    public double VerticalRate { get; set; }
    public double GnssBaroAltDiff { get; set; }
    public ushort VerticalRateRaw { get; set; }
    public byte ReservedBits { get; set; }
    public byte GnssBaroAltitudeDifferenceRaw { get; set; }
    
    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        base.InternalDeserialize(ref buffer);
        var bitIndex = 5;
        SubType = (VelocitySubTypeEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);
        IntentChangeFlag = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 1;
        IFRCapabilityFlag = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 1;
        NavigationAccuracyCategoryVelocityCode =
            (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);
        ReadVelocityData(buffer, ref bitIndex, SubType);
        VrSrc = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 0
            ? VerticalRateSourceEnum.Gnss
            : VerticalRateSourceEnum.Barometric;
        var svt = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 0 ? 1 : -1;
        VerticalRateRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 9);
        VerticalRate = svt * GetVerticalRate(VerticalRateRaw);
        ReservedBits = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        var sDiff = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 0 ? 1 : -1;
        GnssBaroAltitudeDifferenceRaw = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 7);
        GnssBaroAltDiff = sDiff * GetGnssBaroAltDiff(GnssBaroAltitudeDifferenceRaw);
        buffer = buffer[(bitIndex / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var bitIndex = 0;
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 5, (uint)MessageType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, (uint)SubType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, IntentChangeFlag ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, IFRCapabilityFlag ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, NavigationAccuracyCategoryVelocityCode);
        WriteVelocityData(buffer, ref bitIndex, SubType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, VrSrc == VerticalRateSourceEnum.Gnss ? 0 : 1);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, VerticalRate < 0 ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 9, SetVerticalRate(VerticalRate));
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, ReservedBits);
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
    public bool EastWestVelocityIsWest { get; set; }
    public ushort EastWestVelocityRaw { get; set; }
    public bool NorthSouthVelocityIsSouth { get; set; }
    public ushort NorthSouthVelocityRaw { get; set; }

    protected override void ReadVelocityData(ReadOnlySpan<byte> buffer, ref int pos, VelocitySubTypeEnum subType)
    {
        EastWestVelocityIsWest = SpanBitHelper.GetBitU(buffer, ref pos, 1) != 0;
        var ewVelocityCoef = EastWestVelocityIsWest ? -1.0 : 1.0;
        EastWestVelocityRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref pos, 10);
        ewVelocityCoef *= (subType == VelocitySubTypeEnum.SubType1 ? 1.0 : 4.0);
        var vx = EastWestVelocityRaw == 0
            ? double.NaN
            : ewVelocityCoef * (EastWestVelocityRaw - 1) * 0.51444; // knots => m/s

        NorthSouthVelocityIsSouth = SpanBitHelper.GetBitU(buffer, ref pos, 1) != 0;
        var nsVelocityCoef = NorthSouthVelocityIsSouth ? -1.0 : 1.0;
        NorthSouthVelocityRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref pos, 10);
        nsVelocityCoef *= (subType == VelocitySubTypeEnum.SubType1 ? 1.0 : 4.0);
        var vy = NorthSouthVelocityRaw == 0
            ? double.NaN
            : nsVelocityCoef * (NorthSouthVelocityRaw - 1) * 0.51444; // knots => m/s

        GroundSpeed = Math.Sqrt(vx * vx + vy * vy);
        GroundTrackAngle = Math.Atan2(vx, vy) * 180.0 / Math.PI;
        if (GroundTrackAngle < 0)
        {
            GroundTrackAngle += 360.0;
        }
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
    public bool MagneticHeadingAvailable { get; set; }
    public ushort MagneticHeadingRaw { get; set; }
    public AirspeedTypeEnum AirspeedType { get; set; }
    public double Airspeed { get; set; }
    public ushort AirspeedRaw { get; set; }
    
    
    protected override void ReadVelocityData(ReadOnlySpan<byte> buffer, ref int pos, VelocitySubTypeEnum subType)
    {
        MagneticHeadingAvailable = SpanBitHelper.GetBitU(buffer, ref pos, 1) != 0;
        if (!MagneticHeadingAvailable)
        {
            MagneticHeading = double.NaN;
            MagneticHeadingRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref pos, 10);
        }
        else
        {
            MagneticHeadingRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref pos, 10);
            MagneticHeading = (MagneticHeadingRaw * 360.0 / 1024.0) % 360.0;
        }

        AirspeedType = SpanBitHelper.GetBitU(buffer, ref pos, 1) == 0 ? AirspeedTypeEnum.IAS : AirspeedTypeEnum.TAS;
        var coef = subType == VelocitySubTypeEnum.SubType3 ? 1.0 : 4.0;

        AirspeedRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref pos, 10);
        Airspeed = AirspeedRaw == 0
            ? double.NaN
            : coef * (AirspeedRaw - 1) * 0.51444; // knots => m/s

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
        var asBits = double.IsNaN(Airspeed)
            ? 0U
            : (uint)Math.Clamp(Math.Round(Airspeed / (0.51444 * coef) + 1, 0), 1, 1023);
        SpanBitHelper.SetBitU(buffer, ref pos, 10, asBits);
    }

    public override ushort Id => (ushort)(base.Id | (ushort)VelocitySubTypeEnum.SubType3);
}
