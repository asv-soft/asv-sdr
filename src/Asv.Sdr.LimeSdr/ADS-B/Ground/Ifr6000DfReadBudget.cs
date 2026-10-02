using System;

namespace Asv.Sdr.LimeSdr;

internal sealed class Ifr6000DfReadBudget(long startedAtMs, int timeoutMs, int? attempts = null)
{
    private readonly long _deadlineMs = startedAtMs + Math.Max(1, timeoutMs);
    private readonly int? _attempts = attempts.HasValue ? Math.Max(1, attempts.Value) : null;

    public int ReadsStarted { get; private set; }

    public bool TryBeginRead(long nowMs)
    {
        // Count-based callers must get complete reads, regardless of USB latency.
        // Timed callers retain the original do/while behavior: at least one read.
        if (_attempts.HasValue
                ? ReadsStarted >= _attempts.Value
                : ReadsStarted > 0 && nowMs >= _deadlineMs)
        {
            return false;
        }

        ReadsStarted++;
        return true;
    }
}
