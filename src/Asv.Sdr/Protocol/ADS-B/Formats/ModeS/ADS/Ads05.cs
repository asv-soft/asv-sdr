using System;

namespace Asv.Sdr;

public class Ads05 : AdsBase
{
    public override byte Ads1 => 0;
    public override byte Ads2 => 5;
    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        buffer = buffer[6..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        buffer = buffer[6..];
    }
}