using System;
using System.Collections.Generic;

namespace Asv.Sdr;


public class ModeSUF20 : ModeSUF4
{
    protected override int FormatLength => 14;
    public override byte FormatId => 20;
    
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

public class ModeSDF20 : ModeSDF4
{
    protected override int FormatLength => 14;
    public override byte FormatId => 20;

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

    protected override BdsBase SelectCandidate(List<BdsBase> candidates)
    {
        return BdsCandidateSelector.SelectByAltitude(candidates, Altitude);
    }
}
