using System;

namespace Asv.Sdr;


public class ModeSUF0 : ModeSUFormatBase
{
    protected override int FormatLength => 7;
    public override byte FormatId => 0;
    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        pos += 3; // spare
        ReplyLength = (byte)ModeSHelper.GetBitU(buffer, ref pos, 1);
        pos += 4; // spare
        Acquisition = (byte)ModeSHelper.GetBitU(buffer, ref pos, 1);
        DataSelector = (byte)ModeSHelper.GetBitU(buffer, ref pos, 8);
        pos += 10; // spare
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        pos += 3; // spare
        ModeSHelper.SetBitU(buffer, ref pos, 1, ReplyLength);
        pos += 4; // spare
        ModeSHelper.SetBitU(buffer, ref pos, 1, Acquisition);
        ModeSHelper.SetBitU(buffer, ref pos, 8, DataSelector);
        pos += 10; // spare
    }

    /// <summary>
    /// RL (reply length):
    /// 0 signifies a reply with DF0,
    /// 1 signifies a reply with DF16.
    /// (1-bit)
    /// </summary>
    public byte ReplyLength { get; set; }
    
    /// <summary>
    /// AQ (acquisition):
    /// 1-bit код, управляющий содержимым поля RI (reply information).
    /// </summary>
    public byte Acquisition { get; set; }
    
    /// <summary>
    /// BDS1 - most significant 4 bits
    /// </summary>
    public byte Bds1 { get; set; }
    
    /// <summary>
    /// BDS2 - least significant 4 bits
    /// </summary>
    public byte Bds2 { get; set; }
    
    /// <summary>
    /// DS (data selector). This 8-bit (15-22) uplink field shall contain the BDS code of the GICB register whose contents shall be returned to the corresponding reply with DF = 16
    /// </summary>
    public byte DataSelector {
        get => (byte)((Bds1 << 0x4) | (Bds2 & 0xF));
        set
        {
            Bds1 = (byte)(value >> 0x4);
            Bds2 = (byte)(value & 0xF);
        } 
    }
}

public class ModeSDF0 : ModeSDFormatBase
{
    private ushort _ac;
    private double _altitude;
    protected override int FormatLength => 7;
    public override byte FormatId => 0;
    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        VerticalStatus = (byte)ModeSHelper.GetBitU(buffer, ref pos, 1);
        CrossLinkCapability = (byte)ModeSHelper.GetBitU(buffer, ref pos, 1);
        pos += 1; // spare
        SensitivityLevel = (byte)ModeSHelper.GetBitU(buffer, ref pos, 3);
        pos += 2; // spare
        ReplyInformation = (byte)ModeSHelper.GetBitU(buffer, ref pos, 4);
        pos += 2; // spare
        AltitudeCode = (byte)ModeSHelper.GetBitU(buffer, ref pos, 13);
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        ModeSHelper.SetBitU(buffer, ref pos, 1, VerticalStatus);
        ModeSHelper.SetBitU(buffer, ref pos, 1, CrossLinkCapability);
        pos += 1; // spare
        ModeSHelper.SetBitU(buffer, ref pos, 3, SensitivityLevel);
        pos += 2; // spare
        ModeSHelper.SetBitU(buffer, ref pos, 4, ReplyInformation);
        pos += 2; // spare
        ModeSHelper.SetBitU(buffer, ref pos, 13, AltitudeCode);
    }

    /// <summary>
    /// VS (vertical status):
    /// 0 - aircraft is airborne;
    /// 1 - aircraft is on the ground.
    /// (1 - bit)
    /// </summary>
    public byte VerticalStatus { get; set; }
    
    /// <summary>
    /// CC (cross-link capability):
    /// 0 - transponder cannot support the cross-link capability;
    /// 1 - transponder supports the cross-link capability.
    /// (1-bit) 
    /// </summary>
    public byte CrossLinkCapability { get; set; }
    
    /// <summary>
    /// SL (sensitivity level, ACAS):
    /// 0 - ACAS inoperative;
    /// 1 - ACAS is operating at sensitivity level 1;
    /// 2 - ACAS is operating at sensitivity level 2;
    /// 3 - ACAS is operating at sensitivity level 3;
    /// 4 - ACAS is operating at sensitivity level 4;
    /// 5 - ACAS is operating at sensitivity level 5;
    /// 6 - ACAS is operating at sensitivity level 6;
    /// 7 - ACAS is operating at sensitivity level 7.
    /// (3-bits)
    /// </summary>
    public byte SensitivityLevel { get; set; }
    
    /// <summary>
    /// RI (reply information):
    /// 0 - reply to an air-air interrogation UF = 0 with AQ = 0, no operating ACAS;
    /// 1..7 - reserved for ACAS;
    /// 8..15 - reply to an air-air interrogation UF = 0 with AQ = 1 and that the maximum airspeed (
    /// 8 - no maximum airspeed data available;
    /// 9 - maximum airspeed is .LE. 140 km/h (75 kt);
    /// 10 - maximum airspeed is .GT. 140 and .LE. 280 km/h (75 and 150 kt);
    /// 11 - maximum airspeed is .GT. 280 and .LE. 560 km/h (150 and 300 kt);
    /// 12 - maximum airspeed is .GT. 560 and .LE. 1 110 km/h (300 and 600 kt);
    /// 13 - maximum airspeed is .GT. 1 110 and .LE. 2 220 km/h (600 and 1 200 kt);
    /// 14 - maximum airspeed is more than 2 220 km/h (1 200 kt);
    /// 15 - not assigned).
    /// (4-bits)
    /// </summary>
    public byte ReplyInformation { get; set; }

    /// <summary>
    /// AC (altitude code):
    /// ModeC/S altitude code
    /// (13-bits)
    /// </summary>
    public ushort AltitudeCode
    {
        get => _ac;
        set
        {
            _ac = value;
            _altitude = GetAltitude(value);
        }
    }

    /// <summary>
    /// Altitude in meters
    /// </summary>
    public double Altitude
    {
        get => _altitude;
        set
        {
            _altitude = value; 
            _ac = (ushort)SetAltitude(value);
        }
    }
    
    
    #region altitude code operations (1.2.6.5.4)

    private static double GetAltitude(ushort nAlt)
    {
        if (nAlt == 0) return double.NaN;
        
        var m = (nAlt & 0x40) != 0 ? 1 : 0;
        
        if (m == 1) // meter
        {
            return GetAltitudeInMeters((ushort)(((nAlt & 0x1F80) >> 1) | (nAlt & 0x3F)));
        }
        
        var q = (nAlt & 0x10) != 0 ? 1 : 0;
        if (q == 1)
        {
            nAlt = (ushort)(((nAlt & 0x1F80) >> 2) | ((nAlt & 0x20) >> 1) | (nAlt & 0xF));
            return (nAlt * 25.0 - 1000.0) / 3.28084;
        }

        var modeCAl = ModeSHelper.GetAltitudeFromModeCAltitudeCode(nAlt);
        return  modeCAl != null ? modeCAl.Value / 3.28084 : double.NaN;
    }
    

    /// <summary>
    /// Сам придумал этот метод, в Icao пока кодирование в метрах не описано
    /// </summary>
    /// <param name="nAlt"></param>
    /// <returns></returns>
    private static double GetAltitudeInMeters(ushort nAlt)
    {
        return nAlt * 4.0 - 304.0;
    }

    private static uint SetAltitude(double alt)
    {
        if (double.IsNaN(alt)) return 0;
    
        alt *= 3.28084;
        var altNorm = alt switch
        {
            >= 50187.5 => (int)Math.Round(alt / 100.0, 0) * 100,
            < -1000.0 => -1000,
            >= -1000.0 and < 0 => (int)Math.Round(Math.Abs(alt) / 25.0) * -25,
            _ => (int)Math.Round(alt / 25.0) * 25
        };
    
        if (altNorm >= 50200)
        {
            return ModeSHelper.GetModeCAltitudeCodeFromAltitude(altNorm);
        }
    
        altNorm = (altNorm + 1000) / 25;
        return (uint)(((altNorm & 0x7E0) << 2) | ((altNorm & 0x10) << 1) | 0x10 | (altNorm & 0xF));
    }

    #endregion
    
}