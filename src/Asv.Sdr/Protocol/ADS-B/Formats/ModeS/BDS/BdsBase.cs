using System;
using Asv.IO;

namespace Asv.Sdr;


public abstract class BdsBase : ISizedSpanSerializable
{
    public abstract byte Bds1 { get; }
    
    public abstract byte Bds2 { get; }

    public virtual void Deserialize(ref ReadOnlySpan<byte> buffer)
    {
        var pos = 0;
        var bds1 = ModeSHelper.GetBitU(buffer, ref pos, 4);
        var bds2 = ModeSHelper.GetBitU(buffer, ref pos, 4);
        if (bds1 != Bds1) throw new FormatException("Invalid BDS 1.");
        if (bds2 != Bds2) throw new FormatException("Invalid BDS 2.");
        buffer = buffer[(pos / 8)..];
        InternalDeserialize(ref buffer);
    }

    public virtual void Serialize(ref Span<byte> buffer)
    {
        var pos = 0;
        ModeSHelper.SetBitU(buffer, ref pos, 4, Bds1);
        ModeSHelper.SetBitU(buffer, ref pos, 4, Bds2);
        buffer = buffer[(pos / 8)..];
        InternalSerialize(ref buffer);
    }
    
    protected abstract void InternalDeserialize(ref ReadOnlySpan<byte> buffer);
    protected abstract void InternalSerialize(ref Span<byte> buffer);

    public int GetByteSize()
    {
        return 7;
    }
}

public class BdsAny(byte bds1, byte bds2) : BdsBase
{
    public override byte Bds1 { get; } = bds1;
    public override byte Bds2 { get; } = bds2;

    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        buffer = buffer[6..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        for (var i = 0; i < 6; i++)
        {
            BinSerialize.WriteByte(ref buffer, 0);
        }
    }
}