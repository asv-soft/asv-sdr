using System;

namespace Asv.Sdr;

public class ModeSUF11 : ModeSUFormatBase
{
    protected override int FormatLength => 7;
    public override byte FormatId => 11;

    public byte PR { get; set; } = 0;

    public byte IC { get; set; } = 1;

    public byte CL { get; set; } = 0;

    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        PR = (byte)ModeSHelper.GetBitU(buffer, ref pos, 4);
        IC = (byte)ModeSHelper.GetBitU(buffer, ref pos, 4);
        CL = (byte)ModeSHelper.GetBitU(buffer, ref pos, 3);
        pos += 16;
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        ModeSHelper.SetBitU(buffer, ref pos, 4, PR);
        ModeSHelper.SetBitU(buffer, ref pos, 4, IC);
        ModeSHelper.SetBitU(buffer, ref pos, 3, CL);
        pos += 16;
    }

    protected override uint GetModifiedCrc(uint calcCrc)
    {
        var ap = GetAddressParity(0xFFFFFF);
        return ap ^ calcCrc;
    }
}

public class ModeSDF11 : ModeSDFormatBase
{
    /// <summary>
    /// Raw Capability
    /// Default Level 2+ transponder, with ability to set CA to 7, airborne
    /// </summary>
    private int _rawCa = 5;
    protected override int FormatLength => 7;
    public override byte FormatId => 11;

    public CapabilityEnum Capability
    {
        get => TransponderHelper.GetCapability(_rawCa);
        set => _rawCa = TransponderHelper.SetCapability(value);
    }
    
    public byte IC { get; set; }
    
    public byte CL { get; set; }

    public ModeSDF11(byte ic, byte cl)
    {
        IC = ic;
        CL = cl;
    }
    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        _rawCa = (int)ModeSHelper.GetBitU(buffer, ref pos, 3);
        IcaoAddress = ModeSHelper.GetBitU(buffer, ref pos, 24);
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        ModeSHelper.SetBitU(buffer, ref pos, 3, (uint)_rawCa);
        ModeSHelper.SetBitU(buffer, ref pos, 24, IcaoAddress);
    }

    protected override uint GetModifiedCrc(uint calcCrc)
    {
        return (uint)(calcCrc ^ (((CL & 0x7) << 4) | (IC & 0xF)));
    }
}