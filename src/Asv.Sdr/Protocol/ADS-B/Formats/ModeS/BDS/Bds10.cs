using System;

namespace Asv.Sdr;

public class Bds10 : BdsBase
{
    public override byte Bds1 => 1;
    public override byte Bds2 => 0;

    /// <summary>
    /// Bit 9
    /// Ставится в true, если BDS 1,0 продолжается в BDS 1,1
    /// </summary>
    public bool ContinuationFlag { get; set; } = false;
    
    /// <summary>
    /// Bit 10
    /// Только если Overlay Command/Data Parity реально реализован. Для современного Level 2 он обязателен.
    /// </summary>
    public bool OverlayCommandCapability { get; set; }
    
    /// <summary>
    /// Bits 11-15
    /// 0 - если нет ACAS/CAS.
    /// </summary>
    public byte Acas { get; set; }

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
    /// Bit 24
    /// false означает Level 2, 3, 4
    /// true означает Level 5
    /// </summary>
    public bool TransponderEnhancedProtocolIndicator { get; set; }
    
    /// <summary>
    /// Bit 25
    /// Если есть поддержка Enhanced Surveillance (BDS 4,0/5,0/6,0)
    /// и/или Extended Squitter регистров (BDS 0,5/0,6/0,8/0,9)
    /// а так же ACAS, AICB и Comm-A
    /// </summary>
    public bool ModeSSpecificServicesCapability { get; set; } = false;
    
    /// <summary>
    /// Bits 26-28
    /// 0 - без uplink ELM
    /// </summary>
    public uint UplinkElmAverageThroughputCapacity { get; set; }

    /// <summary>
    /// Bits 29-31
    /// 0 - без downlink ELM
    /// </summary>
    public uint DownlinkElmThroughput { get; set; }
    
    /// <summary>
    /// Bit 33
    /// true - если реализован BDS 2,0
    /// </summary>
    public bool AircraftIdCapability { get; set; } = true;
    
    /// <summary>
    /// Bit 34
    /// true - пока ES-регистры действительно обновляются и валидны
    /// </summary>
    public bool SquitterCapability { get; set; } = true;
    
    /// <summary>
    /// Bit 35
    /// true - Если полностью реализован SI lockout. Для современного Level 2 это требуется
    /// </summary>
    public bool SurveillanceIdentifierCode { get; set; }

    /// <summary>
    /// Bit 36
    /// toggle-бит - Это не постоянный признак true. Он должен менять состояние при изменении BDS 1,7
    /// </summary>
    public bool CommonUsageGicbCapabilityReport { get; set; }

    /// <summary>
    /// Bits 49-50
    /// true - если есть подключенные DTE/ADS/Comm-A приложения
    /// </summary>
    public ushort DataTerminalEquipmentStatus { get; set; } = 0;

    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        var pos = 0;
        ContinuationFlag = ModeSHelper.GetBitU(buffer, ref pos, 1) != 0;
        
        var reserved = ModeSHelper.GetBitU(buffer, ref pos, 5);
        if (reserved != 0) throw new Exception("Failed to deserialize BDS 1,0 data");
        
        OverlayCommandCapability = ModeSHelper.GetBitU(buffer, ref pos, 1) != 0;
        Acas = (byte)(ModeSHelper.GetBitU(buffer, ref pos, 1) << 4);
        
        ModeSSubnetworkVersion = (byte)ModeSHelper.GetBitU(buffer, ref pos, 7);
        
        TransponderEnhancedProtocolIndicator = ModeSHelper.GetBitU(buffer, ref pos, 1) != 0;
        ModeSSpecificServicesCapability = ModeSHelper.GetBitU(buffer, ref pos, 1) != 0;
        
        UplinkElmAverageThroughputCapacity = ModeSHelper.GetBitU(buffer, ref pos, 3);
        DownlinkElmThroughput = ModeSHelper.GetBitU(buffer, ref pos, 4);
        
        AircraftIdCapability = ModeSHelper.GetBitU(buffer, ref pos, 1) != 0;
        SquitterCapability = ModeSHelper.GetBitU(buffer, ref pos, 1) != 0;
        SurveillanceIdentifierCode = ModeSHelper.GetBitU(buffer, ref pos, 1) != 0;
        CommonUsageGicbCapabilityReport = ModeSHelper.GetBitU(buffer, ref pos, 1) != 0;
        
        Acas = (byte)(Acas | ModeSHelper.GetBitU(buffer, ref pos, 4));
        
        DataTerminalEquipmentStatus = (ushort)ModeSHelper.GetBitU(buffer, ref pos, 16);
        buffer = buffer[(pos/8)..];
    }

    


    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var pos = 0;
        ModeSHelper.SetBitU(buffer, ref pos, 1, ContinuationFlag ? 1U : 0U);
        ModeSHelper.SetBitU(buffer, ref pos, 5, 0U);
        ModeSHelper.SetBitU(buffer, ref pos, 1, OverlayCommandCapability ? 1U : 0U);
        
        ModeSHelper.SetBitU(buffer, ref pos, 1, (Acas & 0x10) != 0 ? 1U : 0U);
        ModeSHelper.SetBitU(buffer, ref pos, 7, ModeSSubnetworkVersion);
        
        ModeSHelper.SetBitU(buffer, ref pos, 1, TransponderEnhancedProtocolIndicator ? 1U : 0U);
        ModeSHelper.SetBitU(buffer, ref pos, 1, ModeSSpecificServicesCapability ? 1U : 0U);
        
        ModeSHelper.SetBitU(buffer, ref pos, 3, UplinkElmAverageThroughputCapacity);
        ModeSHelper.SetBitU(buffer, ref pos, 4, DownlinkElmThroughput);
        
        ModeSHelper.SetBitU(buffer, ref pos, 1, AircraftIdCapability ? 1U : 0U);
        ModeSHelper.SetBitU(buffer, ref pos, 1, SquitterCapability ? 1U : 0U);
        ModeSHelper.SetBitU(buffer, ref pos, 1, SurveillanceIdentifierCode ? 1U : 0U);
        ModeSHelper.SetBitU(buffer, ref pos, 1, CommonUsageGicbCapabilityReport ? 1U : 0U);
        
        ModeSHelper.SetBitU(buffer, ref pos, 4, (byte)(Acas & 0xF));
        ModeSHelper.SetBitU(buffer, ref pos, 16, DataTerminalEquipmentStatus);
        
        buffer = buffer[(pos/8)..];
    }
}