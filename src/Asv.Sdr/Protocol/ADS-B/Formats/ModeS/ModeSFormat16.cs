using System;

namespace Asv.Sdr;

public class ModeSUF16 : ModeSUF0
{
    public override byte FormatId => 16;
    protected override int FormatLength => 14;

    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        base.InternalDeserialize(buffer, ref pos);
        DeserializeAds(buffer, ref pos);
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        base.InternalSerialize(buffer, ref pos);
        SerializeAds(buffer, ref pos);
    }
}

public class ModeSDF16 : ModeSDF0
{
    public override byte FormatId => 16;
    protected override int FormatLength => 14;

    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        base.InternalDeserialize(buffer, ref pos);
        DeserializeBds(buffer, ref pos);
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        base.InternalSerialize(buffer, ref pos);
        SerializeBds(buffer, ref pos);
    }
}