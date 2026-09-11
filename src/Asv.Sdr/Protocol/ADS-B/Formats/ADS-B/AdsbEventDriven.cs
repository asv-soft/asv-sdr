using System;
using Asv.IO;

namespace Asv.Sdr;

/// <summary>
/// BDS 0,A extended squitter event-driven register (TC 23).
/// </summary>
public class AdsbEventDriven : AdsbExtendedSquitterBase
{
    public override AdsbMessageTypeEnum MessageType => AdsbMessageTypeEnum.EventDriven;

    public byte SubType { get; set; }
    public ushort ModeAIdentityRaw { get; set; }
    public bool ModeAIdentityXBit { get; private set; }
    public string Squawk { get; private set; } = string.Empty;
    public byte ReservedBits21To23 { get; set; }
    public uint ReservedBits24To55 { get; set; }

    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        base.InternalDeserialize(ref buffer);
        var bitIndex = 5;
        SubType = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);
        ModeAIdentityRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 13);
        ReservedBits21To23 = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);
        ReservedBits24To55 = SpanBitHelper.GetBitU(buffer, ref bitIndex, 32);

        ModeAIdentityXBit = TransponderHelper.GetModeAIdentityXBit(ModeAIdentityRaw);
        Squawk = TransponderHelper.DecodeModeAIdentityToSquawk(ModeAIdentityRaw);
        buffer = buffer[(bitIndex / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var bitIndex = 0;
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 5, (uint)MessageType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, SubType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 13, ModeAIdentityRaw);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, ReservedBits21To23);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 32, ReservedBits24To55);
        buffer = buffer[(bitIndex / 8)..];
    }
}
