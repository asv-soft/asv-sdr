using System;

namespace Asv.Sdr;

public class ModeSUF4 : ModeSUFormatBase
{
    protected override int FormatLength => 7;
    public override byte FormatId => 4;

    public byte PC { get; set; }
    public byte RR { get; set; }
    public byte DI { get; set; }
    public byte SD { get; set; }

    
    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        PC = (byte)ModeSHelper.GetBitU(buffer, ref pos, 3);
        RR = (byte)ModeSHelper.GetBitU(buffer, ref pos, 5);
        DI = (byte)ModeSHelper.GetBitU(buffer, ref pos, 3);
        SD = (byte)ModeSHelper.GetBitU(buffer, ref pos, 16);
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        ModeSHelper.SetBitU(buffer, ref pos, 3, PC);
        ModeSHelper.SetBitU(buffer, ref pos, 5, RR);
        ModeSHelper.SetBitU(buffer, ref pos, 3, DI);
        ModeSHelper.SetBitU(buffer, ref pos, 16, SD);
    }
}

public class ModeSDF4 : ModeSDFormatBase
{
    protected override int FormatLength => 7;
    public override byte FormatId => 4;

    public byte FS { get; set; } = 0x1;
    public byte DR { get; set; } = 0x0;
    public byte UM { get; set; } = 0x0;
    private ushort AC { get; set; } = (ushort)SetAltitude(0.0);

    public double Altitude
    {
        get => GetAltitude(AC);
        set => AC = (ushort)SetAltitude(value);
    }


    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        throw new NotImplementedException();
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        ModeSHelper.SetBitU(buffer, ref pos, 3, FS);
        ModeSHelper.SetBitU(buffer, ref pos, 5, DR);
        ModeSHelper.SetBitU(buffer, ref pos, 6, UM);
        ModeSHelper.SetBitU(buffer, ref pos, 13, AC);
    }
    
    private static double GetAltitude(uint nAlt)
    {
        if (nAlt == 0) return double.NaN;
        
        var m = (nAlt & 0x40) != 0 ? 1 : 0;
        
        if (m == 1) // meter
        {
            return GetAltitudeInMeters(((nAlt & 0x1F80) >> 1) | (nAlt & 0x3F));
        }
        
        var q = (nAlt & 0x10) != 0 ? 1 : 0;
        nAlt = ((nAlt & 0x1F80) >> 2) | ((nAlt & 0x20) >> 1) | (nAlt & 0xF);
        if (q == 1)
        {
            return (nAlt * 25.0 - 1000.0) / 3.28084;
        }
        return GetAltitudeFromModeC((int)nAlt) / 3.28084;
    }

    private static double GetAltitudeFromModeC(int nAlt)
    {
        return 0;
    }
    
    private static int SetAltitudeToModeC(double altitude)
    {
        return 0;
    }

    /// <summary>
    /// Сам придумал этот метод, в Icao пока кодирование в метрах не описано
    /// </summary>
    /// <param name="nAlt"></param>
    /// <returns></returns>
    private static double GetAltitudeInMeters(uint nAlt)
    {
        return nAlt * 4.0 - 304.0;
    }

    private static uint SetAltitude(double alt)
    {
        if (double.IsNaN(alt)) return 0;
    
        alt *= 3.28084;
        var altNorm = alt switch
        {
            > 50187.5 => (int)Math.Round(alt / 100.0, 0) * 100,
            < -1000.0 => -1000,
            _ => (int)Math.Round(alt / 25.0) * 25
        };
    
        if (altNorm > 50175)
        {
            var nAlt = SetAltitudeToModeC(altNorm);
            return (uint)(((nAlt & 0x7E0) << 2) | ((nAlt & 0x10) << 1) | (nAlt & 0xF));
        }
    
        altNorm = (altNorm + 1000) / 25;
        return (uint)(((altNorm & 0x7E0) << 2) | ((altNorm & 0x10) << 1) | 0x10 | (altNorm & 0xF));
    }
}