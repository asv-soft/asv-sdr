using System;
using Asv.IO;

namespace Asv.Sdr;

public abstract class AdsbAircraftStatus : AdsbExtendedSquitterBase
{
    public override ushort Id => (ushort)(base.Id | (ushort)AircraftStatusSubType);
    public override AdsbMessageTypeEnum MessageType => AdsbMessageTypeEnum.AircraftStatus;

    public abstract AircraftStatusSubTypeEnum AircraftStatusSubType { get; }
    public ushort ReservedBitsHigh16 { get; set; }
    public uint ReservedBitsLow32 { get; set; }

    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        base.InternalDeserialize(ref buffer);
        var bitIndex = 5;
        var subType = (AircraftStatusSubTypeEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);
        if (subType != AircraftStatusSubType)
        {
            throw new Exception($"Deserialization ADS-B aircraft status failed: want subtype '{AircraftStatusSubType:G}'. Read = '{subType:G}'");
        }

        ReadStatusData(buffer, ref bitIndex);
        buffer = buffer[(bitIndex / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var bitIndex = 0;
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 5, (uint)MessageType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, (uint)AircraftStatusSubType);
        WriteStatusData(buffer, ref bitIndex);
        buffer = buffer[(bitIndex / 8)..];
    }

    protected virtual void ReadStatusData(ReadOnlySpan<byte> buffer, ref int bitIndex)
    {
        ReservedBitsHigh16 = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 16);
        ReservedBitsLow32 = SpanBitHelper.GetBitU(buffer, ref bitIndex, 32);
    }

    protected virtual void WriteStatusData(Span<byte> buffer, ref int bitIndex)
    {
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 16, ReservedBitsHigh16);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 32, ReservedBitsLow32);
    }
}

public class AdsbAircraftStatusNoInformation : AdsbAircraftStatus
{
    public override AircraftStatusSubTypeEnum AircraftStatusSubType => AircraftStatusSubTypeEnum.NoInformation;
}

public class AdsbAircraftEmergencyStatus : AdsbAircraftStatus
{
    public override AircraftStatusSubTypeEnum AircraftStatusSubType => AircraftStatusSubTypeEnum.EmergencyPriorityStatus;

    public byte EmergencyState { get; set; }
    public string EmergencyStateText { get; set; } = string.Empty;
    public ushort ModeAIdentityRaw { get; set; }
    public bool ModeAIdentityXBit { get; set; }
    public string Squawk { get; set; } = string.Empty;

    protected override void ReadStatusData(ReadOnlySpan<byte> buffer, ref int bitIndex)
    {
        EmergencyState = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);
        ModeAIdentityRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 13);
        ReservedBitsLow32 = SpanBitHelper.GetBitU(buffer, ref bitIndex, 32);

        EmergencyStateText = TransponderHelper.DecodeEmergencyState(EmergencyState);
        ModeAIdentityXBit = TransponderHelper.GetModeAIdentityXBit(ModeAIdentityRaw);
        Squawk = TransponderHelper.DecodeModeAIdentityToSquawk(ModeAIdentityRaw);
    }

    protected override void WriteStatusData(Span<byte> buffer, ref int bitIndex)
    {
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, EmergencyState);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 13, ModeAIdentityRaw);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 32, ReservedBitsLow32);
    }
}

public class AdsbAircraftAcasRaBroadcast : AdsbAircraftStatus
{
    public override AircraftStatusSubTypeEnum AircraftStatusSubType => AircraftStatusSubTypeEnum.AcasRaBroadcast;

    public byte[] RawAcasRa { get; } = new byte[6];
    public TransponderHelper.AcasRaInfo AcasRa { get; set; } = new();

    protected override void ReadStatusData(ReadOnlySpan<byte> buffer, ref int bitIndex)
    {
        for (var i = 0; i < RawAcasRa.Length; i++)
        {
            RawAcasRa[i] = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 8);
        }

        var bds30Payload = new byte[7];
        bds30Payload[0] = 0x30;
        RawAcasRa.CopyTo(bds30Payload, 1);
        AcasRa = TransponderHelper.DecodeAcasRaBds30(bds30Payload);
    }

    protected override void WriteStatusData(Span<byte> buffer, ref int bitIndex)
    {
        for (var i = 0; i < RawAcasRa.Length; i++)
        {
            SpanBitHelper.SetBitU(buffer, ref bitIndex, 8, RawAcasRa[i]);
        }
    }
}
