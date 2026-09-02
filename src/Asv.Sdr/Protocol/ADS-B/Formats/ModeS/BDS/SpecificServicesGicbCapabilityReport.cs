using System;
using System.Collections.Generic;
using System.Linq;

namespace Asv.Sdr;

public abstract class SpecificServicesGicbCapabilityReport : BdsBase
{
    protected abstract byte FirstDataSelector { get; }

    protected abstract int CapabilityCount { get; }

    public List<Gicb> Gicbs { get; } = [];

    public uint ReservedBits { get; set; }

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
        Gicbs.Clear();
        ReservedBits = 0;
        var position = 0;
        for (var i = 0; i < 56; i++)
        {
            var isSupported = ModeSHelper.GetBitU(buffer, ref position, 1) != 0;
            if (i < CapabilityCount)
            {
                if (isSupported)
                {
                    var dataSelector = (byte)(FirstDataSelector + i);
                    Gicbs.Add(
                        new Gicb((byte)(dataSelector >> 4), (byte)(dataSelector & 0x0F))
                    );
                }
            }
            else
            {
                ReservedBits = (ReservedBits << 1) | (isSupported ? 1U : 0U);
            }
        }

        buffer = buffer[(position / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var position = 0;
        for (var i = 0; i < 56; i++)
        {
            uint bit;
            if (i < CapabilityCount)
            {
                var dataSelector = (byte)(FirstDataSelector + i);
                bit = Gicbs.Any(x => x.DataSelector == dataSelector) ? 1U : 0U;
            }
            else
            {
                bit = (ReservedBits >> (55 - i)) & 1U;
            }

            ModeSHelper.SetBitU(buffer, ref position, 1, bit);
        }

        buffer = buffer[(position / 8)..];
    }
}

public sealed class Bds18 : SpecificServicesGicbCapabilityReport
{
    public override byte Bds1 => 1;
    public override byte Bds2 => 8;
    protected override byte FirstDataSelector => 0x01;
    protected override int CapabilityCount => 56;
}

public sealed class Bds19 : SpecificServicesGicbCapabilityReport
{
    public override byte Bds1 => 1;
    public override byte Bds2 => 9;
    protected override byte FirstDataSelector => 0x39;
    protected override int CapabilityCount => 56;
}

public sealed class Bds1A : SpecificServicesGicbCapabilityReport
{
    public override byte Bds1 => 1;
    public override byte Bds2 => 0x0A;
    protected override byte FirstDataSelector => 0x71;
    protected override int CapabilityCount => 56;
}

public sealed class Bds1B : SpecificServicesGicbCapabilityReport
{
    public override byte Bds1 => 1;
    public override byte Bds2 => 0x0B;
    protected override byte FirstDataSelector => 0xA9;
    protected override int CapabilityCount => 56;
}

public sealed class Bds1C : SpecificServicesGicbCapabilityReport
{
    public override byte Bds1 => 1;
    public override byte Bds2 => 0x0C;
    protected override byte FirstDataSelector => 0xE1;
    protected override int CapabilityCount => 31;
}
