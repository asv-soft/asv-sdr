using System;
using System.Collections.Generic;
using Asv.IO;

namespace Asv.Sdr;

public abstract class BdsBase : ISizedSpanSerializable
{
    public abstract byte Bds1 { get; }
    
    public abstract byte Bds2 { get; }


    public byte DataSelector => (byte)((Bds1 << 0x4) | (Bds2 & 0xF));
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

    public byte[] Data { get; } = new byte[7];
    public override void Deserialize(ref ReadOnlySpan<byte> buffer)
    {
        InternalDeserialize(ref buffer);
    }

    public override void Serialize(ref Span<byte> buffer)
    {
        InternalSerialize(ref buffer);
    }

    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        var len = Math.Min(buffer.Length, 7);
        for (var i = 0; i < len; i++)
        {
            Data[i] = buffer[i];
        }
        buffer = buffer[len..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var len = Math.Min(buffer.Length, 7);
        for (var i = 0; i < len; i++)
        {
            Data[i] = buffer[i];
            BinSerialize.WriteByte(ref buffer, Data[i]);
        }
        buffer = buffer[len..];
    }
}