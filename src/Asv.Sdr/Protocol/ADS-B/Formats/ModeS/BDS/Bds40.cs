using System;

namespace Asv.Sdr;

/// <summary>
/// DAPs registers
/// Selected vertical intention
/// </summary>
public class Bds40 : BdsBase
{
    public override byte Bds1 => 4;
    public override byte Bds2 => 0;
    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        var pos = 0;
        var sb1 = ModeSHelper.GetBitU(buffer, ref pos, 1);
        var mcpFcuSelectedAltitude = (ushort)ModeSHelper.GetBitU(buffer, ref pos, 12);
        if (sb1 == 0 && mcpFcuSelectedAltitude != 0) throw new Exception("Failed to deserialize BDS 4,0 data");
        McpFcuSelectedAltitude = mcpFcuSelectedAltitude * 16.0;
        
        var sb2 = ModeSHelper.GetBitU(buffer, ref pos, 1);
        var fmsSelectedAltitude = (ushort)ModeSHelper.GetBitU(buffer, ref pos, 12);
        if (sb2 == 0 && fmsSelectedAltitude != 0) throw new Exception("Failed to deserialize BDS 4,0 data");
        FmsSelectedAltitude = fmsSelectedAltitude * 16.0;
        
        var sb3 = ModeSHelper.GetBitU(buffer, ref pos, 1);
        var barometricPressureSetting = (ushort)ModeSHelper.GetBitU(buffer, ref pos, 12);
        if (sb3 == 0 && barometricPressureSetting != 0) throw new Exception("Failed to deserialize BDS 4,0 data");
        BarometricPressureSetting = barometricPressureSetting * 0.1 + 800;
        
        if (ModeSHelper.GetBitU(buffer, ref pos, 8) != 0) throw new Exception("Failed to deserialize BDS 4,0 data");
        
        var sb4 = ModeSHelper.GetBitU(buffer, ref pos, 1);
        var mcpFcuMode = (byte)ModeSHelper.GetBitU(buffer, ref pos, 3);
        if (sb4 == 0 && mcpFcuMode != 0) throw new Exception("Failed to deserialize BDS 4,0 data");
        VnavMode = (mcpFcuMode & 0x4) != 0;
        AltHoldMode = (mcpFcuMode & 0x2) != 0;
        ApproachMode = (mcpFcuMode & 0x1) != 0;
        
        if (ModeSHelper.GetBitU(buffer, ref pos, 2) != 0) throw new Exception("Failed to deserialize BDS 4,0 data");
        
        var sb5 = ModeSHelper.GetBitU(buffer, ref pos, 1);
        var targetAltitudeSource = (byte)ModeSHelper.GetBitU(buffer, ref pos, 2);
        if (sb5 == 0 && targetAltitudeSource != 0) throw new Exception("Failed to deserialize BDS 4,0 data");
        TargetAltitudeSource = (AltitudeSourceEnum)targetAltitudeSource;
        buffer = buffer[(pos/8)..];
    }

    public enum AltitudeSourceEnum
    {
        Unknown,
        Aircraft,
        FcuMcp,
        Fms
    }
    
    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var pos = 0;
        ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        ModeSHelper.SetBitU(buffer, ref pos, 12, (uint)Math.Round(McpFcuSelectedAltitude / 16.0));
        
        ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        ModeSHelper.SetBitU(buffer, ref pos, 12, (uint)Math.Round(FmsSelectedAltitude / 16.0));
        
        ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        if (BarometricPressureSetting < 800) BarometricPressureSetting = 800;
        if (BarometricPressureSetting > 1209) BarometricPressureSetting = 1209;
        ModeSHelper.SetBitU(buffer, ref pos, 12, (uint)Math.Round((BarometricPressureSetting - 800) * 10));
        
        ModeSHelper.SetBitU(buffer, ref pos, 8, 0);
        
        ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        ModeSHelper.SetBitU(buffer, ref pos, 1, VnavMode ? 1u : 0);
        ModeSHelper.SetBitU(buffer, ref pos, 1, AltHoldMode ? 1u : 0);
        ModeSHelper.SetBitU(buffer, ref pos, 1, ApproachMode ? 1u : 0);
        
        ModeSHelper.SetBitU(buffer, ref pos, 2, 0);
        
        ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        ModeSHelper.SetBitU(buffer, ref pos, 2, (uint)TargetAltitudeSource);
        buffer = buffer[(pos/8)..];
    }

    /// <summary>
    /// MCP/FCU selected altitude
    ///
    /// (1 + 12 bits)
    /// </summary>
    public double McpFcuSelectedAltitude { get; set; }
    
    /// <summary>
    /// FMS selected altitude
    ///
    /// (1 + 12 bits)
    /// </summary>
    public double FmsSelectedAltitude { get; set; }

    /// <summary>
    /// Barometric pressure setting minus 800 mb
    ///
    /// (1 + 12 bits)
    /// </summary>
    public double BarometricPressureSetting { get; set; }

    /// <summary>
    /// VNAV mode bits
    ///
    /// (1 bits)
    /// </summary>
    public bool VnavMode { get; set; }
    
    
    /// <summary>
    /// VNAV mode bits
    ///
    /// (1 bits)
    /// </summary>
    public bool AltHoldMode { get; set; }
    
    /// <summary>
    /// VNAV mode bits
    ///
    /// (1 bits)
    /// </summary>
    public bool ApproachMode { get; set; }
    
    /// <summary>
    /// Target altitude source bits
    ///
    /// (1 + 2 bits)
    /// </summary>
    public AltitudeSourceEnum TargetAltitudeSource { get; set; }

}