using System;

namespace Asv.Sdr;

/// <summary>
/// DAPs registers
/// Track and turn report
/// </summary>
public class Bds50 : BdsBase
{
    public override byte Bds1 => 5;
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
        HasRollAngle = sb1 == 1;
        var rollAngle = ModeSHelper.GetBitS(buffer, ref pos, 10);
        switch (sb1)
        {
            case 0 when rollAngle != 0:
                throw new Exception("Failed to deserialize BDS 5,0 data");
            case 1:
            {
                RollAngle = rollAngle * (45.0 / 256.0);
                break;
            }
        }

        var sb2 = ModeSHelper.GetBitU(buffer, ref pos, 1);
        HasTrueTrackAngle = sb2 == 1;
        var trueTrackAngle = ModeSHelper.GetBitS(buffer, ref pos, 11);
        if (sb2 == 0 && trueTrackAngle != 0) throw new Exception("Failed to deserialize BDS 5,0 data");
        TrueTrackAngle = trueTrackAngle * (90.0 / 512.0);
        
        
        var sb3 = ModeSHelper.GetBitU(buffer, ref pos, 1);
        HasGroundSpeed = sb3 == 1;
        var groundSpeed = ModeSHelper.GetBitU(buffer, ref pos, 10);
        switch (sb3)
        {
            case 0 when groundSpeed != 0:
                throw new Exception("Failed to deserialize BDS 5,0 data");
            case 1:
            {
                GroundSpeed = groundSpeed * 2.0;
                break;
            }
        }


        var sb4 = ModeSHelper.GetBitU(buffer, ref pos, 1);
        HasTrackAngleRate = sb4 == 1;
        var trackAngleRate = ModeSHelper.GetBitS(buffer, ref pos, 10);
        if (sb4 == 0 && trackAngleRate != 0) throw new Exception("Failed to deserialize BDS 5,0 data");
        TrackAngleRate = trackAngleRate * (8.0 / 256.0);
        
        var sb5 = ModeSHelper.GetBitU(buffer, ref pos, 1);
        HasTrueAirspeed = sb5 == 1;
        var trueAirspeed = ModeSHelper.GetBitU(buffer, ref pos, 10);
        switch (sb5)
        {
            case 0 when trueAirspeed != 0:
                throw new Exception("Failed to deserialize BDS 5,0 data");
            case 1:
            {
                TrueAirspeed = trueAirspeed * 2.0;
                break;
            }
        }

        buffer = buffer[(pos/8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var pos = 0;
        if (RollAngle < -90.0) RollAngle = -90.0;
        if (RollAngle > 89.82421875) RollAngle = 89.82421875;
        ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        ModeSHelper.SetBitS(buffer, ref pos, 10, (int)Math.Round(RollAngle * (256.0 / 45.0)));

        TrueTrackAngle %= 180.0;
        if (TrueTrackAngle < -180.0) TrueTrackAngle = -180.0;
        if (TrueTrackAngle > 179.82421875) TrueTrackAngle = 179.82421875;
        ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        ModeSHelper.SetBitS(buffer, ref pos, 11, (int)Math.Round(TrueTrackAngle * (512.0 / 90.0)));
        
        if (GroundSpeed < 0.0) GroundSpeed = 0.0;
        if (GroundSpeed > 2046.0) GroundSpeed = 2046.0;
        ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        ModeSHelper.SetBitU(buffer, ref pos, 10, (uint)Math.Round(GroundSpeed / 2.0));
        
        if (TrackAngleRate < -16.0) TrackAngleRate = -16.0;
        if (TrackAngleRate > 15.96875) TrackAngleRate = 15.96875;
        ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        ModeSHelper.SetBitS(buffer, ref pos, 10, (int)Math.Round(TrackAngleRate * (256.0 / 8.0)));
        
        if (TrueAirspeed < 0.0) TrueAirspeed = 0.0;
        if (TrueAirspeed > 2046.0) TrueAirspeed = 2046.0;
        ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        ModeSHelper.SetBitU(buffer, ref pos, 10, (uint)Math.Round(TrueAirspeed / 2.0));
        
        buffer = buffer[(pos/8)..];
    }
    
    /// <summary>
    /// Roll angle
    ///
    /// (11 bits)
    /// </summary>
    public double RollAngle { get; set; }

    public bool HasRollAngle { get; private set; }
    
    /// <summary>
    /// True track angle
    ///
    /// (12 bits)
    /// </summary>
    public double TrueTrackAngle { get; set; }

    public bool HasTrueTrackAngle { get; private set; }
    
    /// <summary>
    /// Ground speed
    ///
    /// (11 bits)
    /// </summary>
    public double GroundSpeed { get; set; }

    public bool HasGroundSpeed { get; private set; }
    
    /// <summary>
    /// Track angle rate
    ///
    /// (11 bits)
    /// </summary>
    public double TrackAngleRate { get; set; }

    public bool HasTrackAngleRate { get; private set; }
    
    /// <summary>
    /// True airspeed
    ///
    /// (11 bits)
    /// </summary>
    public double TrueAirspeed { get; set; }

    public bool HasTrueAirspeed { get; private set; }
}
