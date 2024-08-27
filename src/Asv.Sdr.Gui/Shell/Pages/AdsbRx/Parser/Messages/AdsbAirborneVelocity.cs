using System;
using Asv.IO;

namespace Asv.Sdr.Gui;


public abstract class AdsbAirborneVelocity : AdsbExtendedSquitterBase
{
    public VelocitySubTypeEnum SubType { get; set; }
    public bool IntentChangeFlag { get; set; }
    public bool IFRCapabilityFlag { get; set; }
    public NavigationUncertaintyCategoryEnum NavigationUncertaintyCategory { get; set; }
    // private int SubTypeSpecificFields { get; set; }
    public AltitudeTypeEnum VrSrc { get; set; }
    public VerticalDirectionEnum VerticalDirection { get; set; }
    public double VerticalRate { get; set; }
    public GnssBaroAltDiffEnum GnssBaroAltDiffSign { get; set; }
    public double GnssBaroAltDiff { get; set; }
    
    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        var bitIndex = 0;
        var typeCode = SpanBitHelper.GetBitU(buffer, ref bitIndex, 5);
        SubType = (VelocitySubTypeEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);
        IntentChangeFlag = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 1;
        IFRCapabilityFlag = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 1;
        NavigationUncertaintyCategory =
            (NavigationUncertaintyCategoryEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);
        var speed = SpanBitHelper.GetBitU(buffer, ref bitIndex, 22);
        SpeedDecode(SubType, speed);
        VrSrc = (AltitudeTypeEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 1);
        VerticalDirection = (VerticalDirectionEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 1);
        VerticalRate = GetVerticalRate(SpanBitHelper.GetBitU(buffer, ref bitIndex, 9));
        bitIndex += 2;
        GnssBaroAltDiffSign = (GnssBaroAltDiffEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 1);
        GnssBaroAltDiff = GetGnssBaroAltDiff(SpanBitHelper.GetBitU(buffer, ref bitIndex, 7));
        buffer = buffer[(bitIndex / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var bitIndex = 0;
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 5, 19);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, (uint)SubType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, IntentChangeFlag ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, IFRCapabilityFlag ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, (uint)NavigationUncertaintyCategory);
        var speed = SpeedEncode();
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 22, speed);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, VrSrc == AltitudeTypeEnum.Gnss ? 0 : 1);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, (uint)VerticalDirection);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 9, SetVerticalRate(VerticalRate));
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, (uint)GnssBaroAltDiffSign);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 7, SetGnssBaroAltDiff(GnssBaroAltDiff));
        buffer = buffer[(bitIndex / 8)..];
    }

    protected abstract void SpeedDecode(VelocitySubTypeEnum subType, uint speed);
    protected abstract uint SpeedEncode();

    public override TypeCodeEnum TypeCode => TypeCodeEnum.AirborneVelocities;

    #region Common

    private static (double ewVelocity, double nsVelocity) GroundSpeedDecoding(VelocitySubTypeEnum subtype,
        EastWestVelocityDirectionEnum ewDirection, uint ewSpeedBits, NorthSouthVelocityDirectionEnum nsDirection,
        int nsSpeedBits)
    {
        double ewVelocity = ewSpeedBits == 0 ? 0 : (ewSpeedBits - 1) * (subtype == VelocitySubTypeEnum.SubType1 ? 1 : 4);
        double nsVelocity = nsSpeedBits == 0 ? 0 : (nsSpeedBits - 1) * (subtype == VelocitySubTypeEnum.SubType1 ? 1 : 4);

        if (ewDirection == EastWestVelocityDirectionEnum.FromEastToWest)
            ewVelocity = -ewVelocity;

        if (nsDirection == NorthSouthVelocityDirectionEnum.FromNorthToSouth)
            nsVelocity = -nsVelocity;

        return (ewVelocity, nsVelocity);
    }

    public static (EastWestVelocityDirectionEnum ewDirection, uint ewSpeedBits, NorthSouthVelocityDirectionEnum nsDirection,
        uint nsSpeedBits) GroundSpeedEncoding(VelocitySubTypeEnum subtype, double ewVelocity, double nsVelocity)
    {
        var ewDirection = ewVelocity < 0
            ? EastWestVelocityDirectionEnum.FromEastToWest
            : EastWestVelocityDirectionEnum.FromWestToEast;
        var nsDirectionBit = nsVelocity < 0
            ? NorthSouthVelocityDirectionEnum.FromNorthToSouth
            : NorthSouthVelocityDirectionEnum.FromSouthToNorth;

        var ewSpeedBits = (uint)(Math.Abs(ewVelocity) + 1);
        var nsSpeedBits = (uint)(Math.Abs(nsVelocity) + 1);

        if (subtype == VelocitySubTypeEnum.SubType2)
        {
            ewSpeedBits = (uint)((Math.Abs(ewVelocity) + 1) / 4.0);
            nsSpeedBits = (uint)((Math.Abs(nsVelocity) + 1) / 4.0);
        }

        // Ограничение на 10 бит
        ewSpeedBits = Math.Min(ewSpeedBits, 1023);
        nsSpeedBits = Math.Min(nsSpeedBits, 1023);

        return (ewDirection, ewSpeedBits, nsDirectionBit, nsSpeedBits);
    }
    
    private static (MagneticHeadingStatusEnum headingAvailable, double magneticHeading, AirspeedTypeEnum AirspeedType, double airspeed) AirspeedDecoding(VelocitySubTypeEnum subtype, MagneticHeadingStatusEnum status, uint headingBits, AirspeedTypeEnum speedType, int speedBits)
    {
        var magneticHeading = status == MagneticHeadingStatusEnum.Available ? headingBits * (360.0 / 1024.0) : 0;

        double airspeed = 0;
        if (speedBits != 0)
        {
            airspeed = subtype == VelocitySubTypeEnum.SubType3 ? speedBits - 1 : 4.0 * (speedBits - 1);
        }

        return (status, magneticHeading, speedType, airspeed);
    }

    public static (MagneticHeadingStatusEnum status, uint headingBits, AirspeedTypeEnum speedType, uint speedBits)
        AirspeedEncoding(VelocitySubTypeEnum subtype, MagneticHeadingStatusEnum headingStatus, double magneticHeading,
            AirspeedTypeEnum airspeedType, double airspeed)
    {

        var headingBits = headingStatus == MagneticHeadingStatusEnum.Available
            ? (uint)Math.Round(magneticHeading / 360.0 * 1024.0, 0) & 1023
            : 0; // Маскируем, чтобы убедиться, что биты не превышают 10 бит

        uint speedBits = 0;

        if (airspeed > 0)
        {
            if (subtype == VelocitySubTypeEnum.SubType3)
            {
                speedBits = (uint)airspeed;
            }
            else if (subtype == VelocitySubTypeEnum.SubType4)
            {
                speedBits = (uint)Math.Round(airspeed / 4.0, 0);
            }
        }

        // Убедимся, что скорость не превышает максимально допустимое 10-битное значение
        speedBits = Math.Min(speedBits, 1023);

        return (headingStatus, headingBits, airspeedType, speedBits);
    }

    private static double GetVerticalRate(uint rateBits)
    {
        return rateBits;
    }
    
    private static uint SetVerticalRate(double rate)
    {
        return (uint)Math.Round(rate, 0);
    }

    private static double GetGnssBaroAltDiff(uint diffBits)
    {
        return diffBits;
    }
    
    private static uint SetGnssBaroAltDiff(double diff)
    {
        return (uint)Math.Round(diff, 0);
    }

    #endregion
    
    
}