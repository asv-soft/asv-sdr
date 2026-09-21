using System;

namespace Asv.Sdr.LimeSdr;

/// <summary>
/// Describes the running IFR6000 FPGA's measurement batches. These settings do not
/// reconfigure the FPGA and must match its batch size and interrogation period.
/// </summary>
public sealed record Ifr6000MeasurementProfile
{
    public static Ifr6000MeasurementProfile Default { get; } = new();

    /// <summary>Number of interrogations per A, C or S batch; raw counters are eight-bit.</summary>
    public int BatchSize { get; init; } = 200;

    /// <summary>Period between interrogations of the same type, not one A/C/S slot.</summary>
    public double InterrogationCycleMs { get; init; } = 6;

    public int CleanBatchesAfterChange { get; init; } = 2;
    public double MeasurementGuardMs { get; init; } = 100;

    /// <summary>
    /// Allows an in-progress mixed batch to finish, followed by complete clean batches.
    /// Reading after this delay returns the latest batch, not an average of those batches.
    /// </summary>
    public TimeSpan SettleDelay
    {
        get
        {
            Validate();
            return TimeSpan.FromMilliseconds(GetSettleMilliseconds());
        }
    }

    public void Validate()
    {
        if (BatchSize is < 1 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(BatchSize), "Batch size must be between 1 and 255.");
        if (!double.IsFinite(InterrogationCycleMs) || InterrogationCycleMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(InterrogationCycleMs), "The interrogation period must be finite and positive.");
        if (CleanBatchesAfterChange < 1)
            throw new ArgumentOutOfRangeException(nameof(CleanBatchesAfterChange), "At least one clean batch is required.");
        if (!double.IsFinite(MeasurementGuardMs) || MeasurementGuardMs < 0)
            throw new ArgumentOutOfRangeException(nameof(MeasurementGuardMs), "The guard time must be finite and nonnegative.");
        // Task.Delay(TimeSpan, ...) supports at most uint.MaxValue - 1 milliseconds.
        var milliseconds = GetSettleMilliseconds();
        if (!double.IsFinite(milliseconds) || milliseconds > uint.MaxValue - 1d || milliseconds < 1)
            throw new ArgumentOutOfRangeException(nameof(InterrogationCycleMs), "The computed settling time is outside the supported timer range.");
    }

    public float ToReplyRatioPercent(int receivedCount)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(receivedCount);
        // Preserve counts above BatchSize as evidence; do not clamp impossible ratios.
        return 100.0f * receivedCount / BatchSize;
    }

    private double GetSettleMilliseconds() =>
        (1.0 + CleanBatchesAfterChange) * BatchSize * InterrogationCycleMs + MeasurementGuardMs;
}
