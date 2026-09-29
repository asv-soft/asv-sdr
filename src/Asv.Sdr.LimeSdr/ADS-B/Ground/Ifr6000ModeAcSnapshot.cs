using System;

namespace Asv.Sdr.LimeSdr;

/// <summary>Optional saturating receive-path diagnostics captured with an A/C generation.</summary>
public sealed class Ifr6000ModeAcDiagnostics
{
    internal const int RegisterCount = 7;
    internal const ushort ExpectedSignature = 0xAC01;

    private Ifr6000ModeAcDiagnostics(ReadOnlySpan<ushort> registers)
    {
        SchemaSignature = registers[0];
        RejectedModeACount = registers[1];
        RejectedModeCCount = registers[2];
        AbortedModeACount = registers[3];
        AbortedModeCCount = registers[4];
        OverrunCount = registers[5];
        ConservativeIntervalAssignmentCount = registers[6];
    }

    public ushort SchemaSignature { get; }
    public ushort RejectedModeACount { get; }
    public ushort RejectedModeCCount { get; }
    public ushort AbortedModeACount { get; }
    public ushort AbortedModeCCount { get; }
    public ushort OverrunCount { get; }

    /// <summary>
    /// Gets the number of confirmed preambles initially assigned the conservative 120 us
    /// interval. This is not a count of final uncertain classifications.
    /// </summary>
    public ushort ConservativeIntervalAssignmentCount { get; }

    internal static Ifr6000ModeAcDiagnostics? Decode(ReadOnlySpan<ushort> registers)
    {
        return registers.Length == RegisterCount && registers[0] == ExpectedSignature
            ? new Ifr6000ModeAcDiagnostics(registers)
            : null;
    }
}

/// <summary>A/C register measurements. Legacy snapshots do not guarantee a shared acquisition generation.</summary>
public sealed class Ifr6000ModeAcSnapshot
{
    internal const int RegisterCount = 10;
    private const ushort SignatureMask = 0x7F00;
    private const ushort Signature = 0x2A00;
    private const ushort ReadyMask = 0x8000;

    private Ifr6000ModeAcSnapshot(ReadOnlySpan<ushort> registers, byte? generation,
        double delayOffsetUs, Ifr6000ModeAcDiagnostics? diagnostics, Ifr6000MeasurementProfile profile)
    {
        IsCoherent = generation.HasValue;
        Generation = generation;
        Diagnostics = diagnostics;
        RawModeA = registers[0];
        RawModeC = registers[1];
        var modeAReplyCount = registers[2] >> 8;
        var modeCReplyCount = registers[2] & 0xFF;
        ReplyRatio = (profile.ToReplyRatioPercent(modeAReplyCount), profile.ToReplyRatioPercent(modeCReplyCount));
        ModeA = ModeAcReplyDecoder.DecodeSquawk(RawModeA);
        ModeCAltitude = ModeAcReplyDecoder.DecodeAltitude(RawModeC);
        ModeAPulseWidth = DecodeWidths(registers[3]);
        ModeCPulseWidth = DecodeWidths(registers[4]);
        PulseSpacing = ((sbyte)(registers[5] >> 8) * 0.025f, (sbyte)registers[5] * 0.025f);
        ModeAReplyDelay = (/* 120 */ + (short)registers[6]) * 0.025f + (float)delayOffsetUs;
        ModeAReplyJitter = registers[7] * 0.025f;
        ModeCReplyDelay = (/* 120 */ + (short)registers[8]) * 0.025f + (float)delayOffsetUs;
        ModeCReplyJitter = registers[9] * 0.025f;
        // A zero reply count means there is no timing measurement, not a zero-jitter PASS.
        if (modeAReplyCount == 0)
        {
            ModeAReplyDelay = float.NaN;
            ModeAPulseWidth = (float.NaN, float.NaN);
            PulseSpacing = (float.NaN, PulseSpacing.ModeC);
        }
        if (modeCReplyCount == 0)
        {
            ModeCReplyDelay = float.NaN;
            ModeCPulseWidth = (float.NaN, float.NaN);
            PulseSpacing = (PulseSpacing.ModeA, float.NaN);
        }
        // Jitter is a spread between samples and requires at least two accepted replies.
        if (modeAReplyCount < 2)
            ModeAReplyJitter = float.NaN;
        if (modeCReplyCount < 2)
            ModeCReplyJitter = float.NaN;
    }

    public bool IsCoherent { get; }
    public byte? Generation { get; }
    public Ifr6000ModeAcDiagnostics? Diagnostics { get; }
    public ushort RawModeA { get; }
    public ushort RawModeC { get; }
    public (float ModeA, float ModeC) ReplyRatio { get; }
    public (string Squawk, bool Spi) ModeA { get; }
    public int? ModeCAltitude { get; }
    public (float F1, float F2) ModeAPulseWidth { get; }
    public (float F1, float F2) ModeCPulseWidth { get; }
    public (float ModeA, float ModeC) PulseSpacing { get; }
    public float ModeAReplyDelay { get; }
    public float ModeCReplyDelay { get; }
    public float ModeAReplyJitter { get; }
    public float ModeCReplyJitter { get; }

    internal static bool IsSupported(ushort status) => (status & SignatureMask) == Signature;

    internal static bool TryDecode(ushort before, ReadOnlySpan<ushort> registers, ushort after,
        double delayOffsetUs, out Ifr6000ModeAcSnapshot? snapshot, out string reason,
        Ifr6000ModeAcDiagnostics? diagnostics = null, Ifr6000MeasurementProfile? profile = null)
    {
        snapshot = null;
        if (registers.Length != RegisterCount) { reason = "register-count"; return false; }
        if (!IsSupported(before) || !IsSupported(after)) { reason = "unsupported"; return false; }
        if ((before & ReadyMask) == 0 || (after & ReadyMask) == 0) { reason = "not-ready"; return false; }
        if (before != after) { reason = "generation-changed"; return false; }
        snapshot = new Ifr6000ModeAcSnapshot(registers, (byte)before, delayOffsetUs, diagnostics,
            profile ?? Ifr6000MeasurementProfile.Default);
        // Preserve impossible raw counts as evidence; never clamp to a plausible percentage.
        reason = snapshot.ReplyRatio.ModeA > 100 || snapshot.ReplyRatio.ModeC > 100
            ? "count-out-of-range" : "accepted";
        return true;
    }

    internal static Ifr6000ModeAcSnapshot DecodeLegacy(ReadOnlySpan<ushort> registers, double delayOffsetUs,
        Ifr6000MeasurementProfile? profile = null)
    {
        if (registers.Length != RegisterCount) throw new ArgumentException("Expected ten A/C registers.", nameof(registers));
        return new Ifr6000ModeAcSnapshot(registers, null, delayOffsetUs, null,
            profile ?? Ifr6000MeasurementProfile.Default);
    }

    private static (float F1, float F2) DecodeWidths(ushort word) => ((word >> 8) * 0.025f, (word & 0xFF) * 0.025f);
}
