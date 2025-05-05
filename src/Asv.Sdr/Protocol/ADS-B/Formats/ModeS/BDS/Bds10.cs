using System;

namespace Asv.Sdr;

public class Bds10 : BdsBase
{
    public override byte Bds1 => 1;
    public override byte Bds2 => 0;

    /// <summary>
    /// Bit 9
    /// </summary>
    public bool ContinuationFlag { get; set; } = false;

    /// <summary>
    /// Bits 17-23
    /// 0 - Mode S subnetwork not available;
    /// 1 - ICAO Doc 9688 (1996);
    /// 2 - ICAO Doc 9688 (1998);
    /// 3 - ICAO Annex 10, Vol III, Amendment;
    /// 4 - ICAO Doc 9871, Edition 1 + DO-181D + ED-73C;
    /// 5 - ICAO Doc 9871, Edition 2 + DO-181E + ED-73E;
    /// 6 - ICAO Doc 9871, Edition 3 + DO-181F + ED-73F;
    /// 7 - 127 Reserved;
    /// </summary>
    public byte ModeSSubnetworkVersion { get; set; } = 0;

    /// <summary>
    /// Bit 25
    /// </summary>
    public bool ModeSSpecificServicesCapability { get; set; } = false;

    /// <summary>
    /// Bit 33
    /// </summary>
    public bool AircraftIdCapability { get; set; } = true;
    
    /// <summary>
    /// Bit 34
    /// </summary>
    public bool SquitterCapability { get; set; } = true;

    /// <summary>
    /// Bits 49-50
    /// </summary>
    public byte ActiveTransponderSideIndicator { get; set; } = 0;

    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        var pos = 0;
        ContinuationFlag = ModeSHelper.GetBitU(buffer, ref pos, 1) != 0; pos += 7;
        ModeSSubnetworkVersion = (byte)ModeSHelper.GetBitU(buffer, ref pos, 7); pos += 1;
        ModeSSpecificServicesCapability = ModeSHelper.GetBitU(buffer, ref pos, 1) != 0; pos += 7;
        AircraftIdCapability = ModeSHelper.GetBitU(buffer, ref pos, 1) != 0;
        SquitterCapability = ModeSHelper.GetBitU(buffer, ref pos, 1) != 0; pos += 14;
        ActiveTransponderSideIndicator = (byte)ModeSHelper.GetBitU(buffer, ref pos, 2); pos += 6;
        buffer = buffer[(pos/8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var pos = 0;
        ModeSHelper.SetBitU(buffer, ref pos, 1, ContinuationFlag ? 1U : 0U);
        ModeSHelper.SetBitU(buffer, ref pos, 7, 0U);
        ModeSHelper.SetBitU(buffer, ref pos, 7, ModeSSubnetworkVersion);
        ModeSHelper.SetBitU(buffer, ref pos, 1, 0U);
        ModeSHelper.SetBitU(buffer, ref pos, 1, ModeSSpecificServicesCapability ? 1U : 0U);
        ModeSHelper.SetBitU(buffer, ref pos, 7, 0U);
        ModeSHelper.SetBitU(buffer, ref pos, 1, AircraftIdCapability ? 1U : 0U);
        ModeSHelper.SetBitU(buffer, ref pos, 1, SquitterCapability ? 1U : 0U);
        ModeSHelper.SetBitU(buffer, ref pos, 14, 0U);
        ModeSHelper.SetBitU(buffer, ref pos, 2, ActiveTransponderSideIndicator);
        ModeSHelper.SetBitU(buffer, ref pos, 6, 0U);
        buffer = buffer[(pos/8)..];
    }
}