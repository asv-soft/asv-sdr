using System;

namespace Asv.Sdr;

public enum ThreatTypeIndicatorEnum : byte
{
    NoIdentityData = 0,
    ModeSTransponderAddress = 1,
    AltitudeRangeBearingData = 2,
    NotAssigned = 3
}

/// <summary>
/// DAPs registers
/// Selected vertical intention
/// </summary>
public class Bds30 : BdsBase
{
    public override byte Bds1 => 3;
    public override byte Bds2 => 0;
    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        var pos = 0;
        Ara = (ushort)ModeSHelper.GetBitU(buffer, ref pos, 14);
        Rac = (byte)ModeSHelper.GetBitU(buffer, ref pos, 4);
        Rat = (byte)ModeSHelper.GetBitU(buffer, ref pos, 1);
        Mte = (byte)ModeSHelper.GetBitU(buffer, ref pos, 1);
        ThreatType = (ThreatTypeIndicatorEnum)ModeSHelper.GetBitU(buffer, ref pos, 2);
        switch (ThreatType)
        {
            case ThreatTypeIndicatorEnum.NoIdentityData or ThreatTypeIndicatorEnum.NotAssigned:
                pos += 26;
                break;
            case ThreatTypeIndicatorEnum.ModeSTransponderAddress:
                TidAddress = ModeSHelper.GetBitU(buffer, ref pos, 24);
                break;
            case ThreatTypeIndicatorEnum.AltitudeRangeBearingData:
            {
                var code = (ushort)ModeSHelper.GetBitU(buffer, ref pos, 13);
                TidAltitude = ModeSHelper.GetAltitudeFromModeCAltitudeCode(code);
                var range = ModeSHelper.GetBitU(buffer, ref pos, 7);
                TidRange = range == 0 ? null : (range - 1) / 10.0;
                var bearing = (ushort)ModeSHelper.GetBitU(buffer, ref pos, 6);
                TidBearing = bearing is 0 or > 60 ? null : (ushort)(bearing * 6 - 3);
                break;
            }
        }

        buffer = buffer[(pos/8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var pos = 0;
        ModeSHelper.SetBitU(buffer, ref pos, 14, Ara);
        ModeSHelper.SetBitU(buffer, ref pos, 4, Rac);
        ModeSHelper.SetBitU(buffer, ref pos, 1, Rat);
        ModeSHelper.SetBitU(buffer, ref pos, 1, Mte);
        ModeSHelper.SetBitU(buffer, ref pos, 2, (byte)ThreatType);
        switch (ThreatType)
        {
            case ThreatTypeIndicatorEnum.NoIdentityData or ThreatTypeIndicatorEnum.NotAssigned:
                ModeSHelper.SetBitU(buffer, ref pos, 26, 0);
                break;
            case ThreatTypeIndicatorEnum.ModeSTransponderAddress:
                ModeSHelper.SetBitU(buffer, ref pos, 24, TidAddress ?? 0);
                ModeSHelper.SetBitU(buffer, ref pos, 2, 0);
                break;
            case ThreatTypeIndicatorEnum.AltitudeRangeBearingData:
                var code = TidAltitude != null ? ModeSHelper.GetModeCAltitudeCodeFromAltitude(TidAltitude.Value) : (ushort)0;
                ModeSHelper.SetBitU(buffer, ref pos, 13, code);
                switch (TidRange)
                {
                    case null or < 0:
                        ModeSHelper.SetBitU(buffer, ref pos, 7, 0);
                        break;
                    case >= 12.55:
                        ModeSHelper.SetBitU(buffer, ref pos, 7, 127);
                        break;
                    default:
                    {
                        var range = (byte)(Math.Round(TidRange.Value * 10.0) + 1);
                        ModeSHelper.SetBitU(buffer, ref pos, 7, range);
                        break;
                    }
                }

                switch (TidBearing)
                {
                    case null:
                        ModeSHelper.SetBitU(buffer, ref pos, 6, 0);
                        break;
                    default:
                    {
                        TidBearing = (ushort?)(TidBearing.Value % 360);
                        var bearing = (ushort)(TidBearing.Value / 6 + 1);
                        ModeSHelper.SetBitU(buffer, ref pos, 6, bearing);
                        break;
                    }
                }
                break;
        }
        buffer = buffer[(pos/8)..];
    }

    /// <summary>
    /// ARA
    /// (14 bits)
    /// </summary>
    public ushort Ara { get; set; }
    
    /// <summary>
    /// RAC
    /// (4 bits)
    /// </summary>
    public byte Rac { get; set; }

    /// <summary>
    /// RAT
    /// (1 bit)
    /// </summary>
    public byte Rat { get; set; }
    
    /// <summary>
    /// Mte
    /// (1 bit)
    /// </summary>
    public byte Mte { get; set; }

    /// <summary>
    /// TTI
    /// Threat type indicator
    /// 2-bit
    /// </summary>
    public ThreatTypeIndicatorEnum ThreatType { get; set; }
    
    /// <summary>
    /// TID
    /// Threat identity data, 24 bits - aircraft address of the threat, 2 bits = zero
    /// (26 bits) 
    /// </summary>
    public uint? TidAddress { get; set; }
    
    /// <summary>
    /// TIDA
    /// Threat identity data altitude. Most recently reported Mode C altitude code of the threat
    /// C1 A1 C2 A2 C4 A4 0 B1 D1 B2 D2 B4 D4
    /// (13 bits)
    /// </summary>
    public int? TidAltitude { get; set; }

    /// <summary>
    /// TIDR
    /// Threat identity data range. Most recent threat range estimated by ACAS
    /// (7 bits)
    /// </summary>
    public double? TidRange { get; set; }
    
    /// <summary>
    /// TIDB
    /// Threat identity data bearing.
    /// Most recent estimated bearing of the threat aircraft, relative to the ACAS aircraft heading
    /// (6 bits)
    /// </summary>
    public ushort? TidBearing { get; set; }

}