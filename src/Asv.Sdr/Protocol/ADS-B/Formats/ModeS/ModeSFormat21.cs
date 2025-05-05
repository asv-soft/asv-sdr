using System;

namespace Asv.Sdr;

public class ModeSUF21 : ModeSUF5
{
    protected override int FormatLength => 14;
    public override byte FormatId => 21;
    
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

public class ModeSDF21 : ModeSDF5
{
    protected override int FormatLength => 14;
    public override byte FormatId => 21;

    public BdsBase BDS { get; set; }
    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        base.InternalDeserialize(buffer, ref pos);
        BDS = DeserializeCommB(buffer, ref pos);
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        base.InternalSerialize(buffer, ref pos);
        var buff = buffer[(pos / 8)..];
        BDS.Serialize(ref buff);
        pos = (buffer.Length - buff.Length) * 8;
    }

    private void FeelCommB()
    {
        // ToDo заполнить CommB
    }
    
    private BdsBase DeserializeCommB(ReadOnlySpan<byte> buffer, ref int pos)
    {
        var bds1 = (byte)(buffer[pos / 8] >> 4);
        var bds2 = (byte)(buffer[pos / 8] & 0xF);
        var buff = buffer[(pos / 8)..];
        BdsBase result;
        switch (bds1)
        {
            case 1 when bds2 == 0:
            {
                var bds10 = new Bds10();
                bds10.Deserialize(ref buff);
                result = bds10;
                break;
            }
            case 2 when bds2 == 0:
            {
                var bds20 = new Bds20();
                bds20.Deserialize(ref buff);
                result = bds20;
                break;
            }
            default:
            {
                var bdsAny = new BdsAny(bds1, bds2);
                bdsAny.Deserialize(ref buff);
                result = bdsAny;
                break;
            }
        }
        pos = (buffer.Length - buff.Length) * 8;
        
        return result;
    }
}