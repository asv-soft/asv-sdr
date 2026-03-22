using System;
using Asv.IO;

namespace Asv.Sdr;

public abstract class AdsBase : ISizedSpanSerializable
{
    public abstract byte Ads1 { get; }
    
    public abstract byte Ads2 { get; }

    public byte DataSelector => (byte)((Ads1 << 0x4) | (Ads2 & 0xF));
    public virtual void Deserialize(ref ReadOnlySpan<byte> buffer)
    {
        var pos = 0;
        var bds1 = ModeSHelper.GetBitU(buffer, ref pos, 4);
        var bds2 = ModeSHelper.GetBitU(buffer, ref pos, 4);
        if (bds1 != Ads1) throw new FormatException("Invalid BDS 1.");
        if (bds2 != Ads2) throw new FormatException("Invalid BDS 2.");
        buffer = buffer[(pos / 8)..];
        InternalDeserialize(ref buffer);
    }

    public virtual void Serialize(ref Span<byte> buffer)
    {
        var pos = 0;
        ModeSHelper.SetBitU(buffer, ref pos, 4, Ads1);
        ModeSHelper.SetBitU(buffer, ref pos, 4, Ads2);
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