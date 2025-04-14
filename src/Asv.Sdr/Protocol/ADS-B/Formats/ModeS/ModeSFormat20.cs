using System;

namespace Asv.Sdr;


public class ModeSUF20 : ModeSUF4
{
    protected override int FormatLength => 14;
    public override byte FormatId => 20;
    private byte[] CommA => new byte[7];
    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        base.InternalDeserialize(buffer, ref pos);
        for (var i = 0; i < 7; i++)
        {
            CommA[i] = (byte)ModeSHelper.GetBitU(buffer, ref pos, 8);
        }
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        base.InternalSerialize(buffer, ref pos);
        FeelCommA();
        for (var i = 0; i < 7; i++)
        {
            ModeSHelper.SetBitU(buffer, ref pos, 8, CommA[i]);
        }
    }

    private void FeelCommA()
    {
        // ToDo заполнить CommA
    }
}

public class ModeSDF20 : ModeSDF4
{
    protected override int FormatLength => 14;
    public override byte FormatId => 20;

    private byte[] CommB => new byte[7];
    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        base.InternalDeserialize(buffer, ref pos);
        for (var i = 0; i < 7; i++)
        {
            CommB[i] = (byte)ModeSHelper.GetBitU(buffer, ref pos, 8);
        }
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        base.InternalSerialize(buffer, ref pos);
        FeelCommB();
        for (var i = 0; i < 7; i++)
        {
            ModeSHelper.SetBitU(buffer, ref pos, 8, CommB[i]);
        }
    }

    private void FeelCommB()
    {
        // ToDo заполнить CommB
    }
}