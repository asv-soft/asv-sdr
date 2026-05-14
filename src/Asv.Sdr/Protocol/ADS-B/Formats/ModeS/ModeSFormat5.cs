using System;

namespace Asv.Sdr;

public class ModeSUF5 : ModeSUF4
{
    public override byte FormatId => 5;
}

public class ModeSDF5 : ModeSDFormatBase
{
    protected override int FormatLength => 7;
    public override byte FormatId => 5;

    public byte FS { get; set; } = 0x0;
    public byte DR { get; set; } = 0x0;
    public byte UM { get; set; } = 0x0;
    private ushort ID { get; set; } = ModeSHelper.SetSquawk("0777");

    
    public string Squawk
    {
        get => ModeSHelper.GetSquawk(ID);
        set => ID = ModeSHelper.SetSquawk(value);
    }

    
    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        FS = (byte)ModeSHelper.GetBitU(buffer, ref pos, 3);
        DR = (byte)ModeSHelper.GetBitU(buffer, ref pos, 5);
        UM = (byte)ModeSHelper.GetBitU(buffer, ref pos, 6);
        ID = (ushort)ModeSHelper.GetBitU(buffer, ref pos, 13);
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        ModeSHelper.SetBitU(buffer, ref pos, 3, FS);
        ModeSHelper.SetBitU(buffer, ref pos, 5, DR);
        ModeSHelper.SetBitU(buffer, ref pos, 6, UM);
        ModeSHelper.SetBitU(buffer, ref pos, 13, ID);
    }
    
    
}