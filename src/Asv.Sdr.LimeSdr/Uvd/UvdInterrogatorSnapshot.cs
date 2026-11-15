using System;

namespace Asv.Sdr.LimeSdr;

/// <summary>One code's registers. Reply count does not establish freshness of its stored information word.</summary>
public sealed class UvdCodeSnapshot
{
    internal UvdCodeSnapshot(UvdRequestCode code, byte replyCount, short delayTicks, UvdReply? storedReply)
    {
        Code = code;
        ReplyCount = replyCount;
        RawReplyDelayTicks = delayTicks;
        StoredReply = storedReply;
    }

    public UvdRequestCode Code { get; }
    public byte ReplyCount { get; }
    public short RawReplyDelayTicks { get; }
    public double ReplyRatioPercent => ReplyCount <= 200 ? ReplyCount * 0.5 : double.NaN;
    /// <summary>Signed raw ticks multiplied by 0.025 us; no nominal delay or calibration is invented.</summary>
    public double ReplyDelayMicroseconds => ReplyCount is > 0 and <= 200
        ? RawReplyDelayTicks * 0.025 : double.NaN;
    /// <summary>Stored information word, without a freshness guarantee; absent by design for ZK4.</summary>
    public UvdReply? StoredReply { get; }
}

/// <summary>All UVD reply and measurement registers read under a single HOLD_FRAME.</summary>
public sealed class UvdInterrogatorSnapshot
{
    public const double ReplyReferenceFrequencyMHz = 740.0;

    internal UvdInterrogatorSnapshot(UvdCodeSnapshot zk1, UvdCodeSnapshot zk2,
        UvdCodeSnapshot zk3, UvdCodeSnapshot zk4, short frequencyOffsetKHz)
    {
        Zk1 = zk1;
        Zk2 = zk2;
        Zk3 = zk3;
        Zk4 = zk4;
        RawFrequencyOffsetKHz = frequencyOffsetKHz;
    }

    public UvdCodeSnapshot Zk1 { get; }
    public UvdCodeSnapshot Zk2 { get; }
    public UvdCodeSnapshot Zk3 { get; }
    public UvdCodeSnapshot Zk4 { get; }
    public short RawFrequencyOffsetKHz { get; }
    /// <summary>MHz derived from the signed offset; unavailable when no code has a valid reply count.</summary>
    public double FrequencyMHz => HasReplies(Zk1) || HasReplies(Zk2) || HasReplies(Zk3) || HasReplies(Zk4)
        ? ReplyReferenceFrequencyMHz + RawFrequencyOffsetKHz / 1000.0 : double.NaN;

    private static bool HasReplies(UvdCodeSnapshot code) => code.ReplyCount is > 0 and <= 200;

    public UvdCodeSnapshot this[UvdRequestCode code] => code switch
    {
        UvdRequestCode.Zk1 => Zk1,
        UvdRequestCode.Zk2 => Zk2,
        UvdRequestCode.Zk3 => Zk3,
        UvdRequestCode.Zk4 => Zk4,
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };
}
