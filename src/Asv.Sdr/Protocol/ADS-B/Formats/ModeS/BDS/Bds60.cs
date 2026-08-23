using System;

namespace Asv.Sdr;

/// <summary>
/// DAPs registers
/// Heading and speed report
/// </summary>
public class Bds60 : BdsBase
{
    public override byte Bds1 => 6;
    public override byte Bds2 => 0;
    
    public override void Deserialize(ref ReadOnlySpan<byte> buffer)
    {
        InternalDeserialize(ref buffer);
    }

    public override void Serialize(ref Span<byte> buffer)
    {
        InternalSerialize(ref buffer);
    }
    
    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        var pos = 0;
        var sb1 = ModeSHelper.GetBitU(buffer, ref pos, 1);
        var magneticHeading = ModeSHelper.GetBitS(buffer, ref pos, 11);
        if (sb1 == 0 && magneticHeading != 0) throw new Exception("Failed to deserialize BDS 6,0 data");
        MagneticHeading = magneticHeading * (90.0 / 512);
        
        var sb2 = ModeSHelper.GetBitU(buffer, ref pos, 1);
        var indicatedAirspeed = ModeSHelper.GetBitU(buffer, ref pos, 10);
        switch (sb2)
        {
            case 0 when indicatedAirspeed != 0:
                throw new Exception("Failed to deserialize BDS 6,0 data");
            case 1:
            {
                IndicatedAirspeed = indicatedAirspeed;
                if (IndicatedAirspeed > 500.0) throw new Exception("Failed to deserialize BDS 6,0 data");
                break;
            }
        }

        var sb3 = ModeSHelper.GetBitU(buffer, ref pos, 1);
        var mach = ModeSHelper.GetBitU(buffer, ref pos, 10);
        switch (sb3)
        {
            case 0 when mach != 0:
                throw new Exception("Failed to deserialize BDS 6,0 data");
            case 1:
            {
                Mach = mach * 0.004;
                if (Mach > 1.0)  throw new Exception("Failed to deserialize BDS 6,0 data");
                break;
            }
        }


        var sb4 = ModeSHelper.GetBitU(buffer, ref pos, 1);
        var barometricAltitudeRate = ModeSHelper.GetBitS(buffer, ref pos, 10);
        switch (sb4)
        {
            case 0 when barometricAltitudeRate != 0:
                throw new Exception("Failed to deserialize BDS 6,0 data");
            case 1:
            {
                BarometricAltitudeRate = barometricAltitudeRate * 32.0;
                if (BarometricAltitudeRate is < -6000 or > 6000) throw new Exception("Failed to deserialize BDS 6,0 data");
                break;
            }
        }


        var sb5 = ModeSHelper.GetBitU(buffer, ref pos, 1);
        var inertialVerticalVelocity = ModeSHelper.GetBitS(buffer, ref pos, 10);
        switch (sb5)
        {
            case 0 when inertialVerticalVelocity != 0:
                throw new Exception("Failed to deserialize BDS 6,0 data");
            case 1:
            {
                InertialVerticalVelocity = inertialVerticalVelocity * 32.0;
                if (InertialVerticalVelocity is < -6000 or > 6000) throw new Exception("Failed to deserialize BDS 6,0 data");
                break;
            }
        }

        buffer = buffer[(pos/8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var pos = 0;
        ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        MagneticHeading %= 180.0;
        ModeSHelper.SetBitS(buffer, ref pos, 11, (int)Math.Round(MagneticHeading * (512.0 / 90.0)));
        
        ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        if (IndicatedAirspeed < 0) IndicatedAirspeed = 0;
        if (IndicatedAirspeed > 500.0) IndicatedAirspeed = 500.0;
        ModeSHelper.SetBitU(buffer, ref pos, 10, (uint)Math.Round(IndicatedAirspeed));
        
        ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        if (Mach < 0) Mach = 0;
        if (Mach > 1.0) Mach = 1.0;
        ModeSHelper.SetBitU(buffer, ref pos, 10, (uint)Math.Round(Mach * 250));
        
        ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        if (BarometricAltitudeRate < -6000) BarometricAltitudeRate = -6000;
        if (BarometricAltitudeRate > 6000) BarometricAltitudeRate = 6000;
        ModeSHelper.SetBitS(buffer, ref pos, 10, (int)Math.Round(BarometricAltitudeRate / 32.0));
        
        ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        if (InertialVerticalVelocity < -6000) InertialVerticalVelocity = -6000;
        if (InertialVerticalVelocity > 6000) InertialVerticalVelocity = 6000;
        ModeSHelper.SetBitS(buffer, ref pos, 10, (int)Math.Round(InertialVerticalVelocity / 32.0));
        
        buffer = buffer[(pos/8)..];
    }
    
    /// <summary>
    /// Magnetic heading
    ///
    /// (12 bits)
    /// </summary>
    public double MagneticHeading { get; set; }
    
    /// <summary>
    /// Indicated airspeed
    ///
    /// (11 bits)
    /// </summary>
    public double IndicatedAirspeed { get; set; }
    
    /// <summary>
    /// Mach
    ///
    /// (11 bits)
    /// </summary>
    public double Mach { get; set; }
    
    /// <summary>
    /// Barometric altitude rate
    ///
    /// (11 bits)
    /// </summary>
    public double BarometricAltitudeRate { get; set; }
    
    /// <summary>
    /// Inertial vertical velocity
    ///
    /// (11 bits)
    /// </summary>
    public double InertialVerticalVelocity { get; set; }
}