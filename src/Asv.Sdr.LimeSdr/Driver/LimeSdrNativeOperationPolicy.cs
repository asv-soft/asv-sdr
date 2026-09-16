using System;

namespace Asv.Sdr.LimeSdr;

internal static class LimeSdrNativeOperationPolicy
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(20),
        TimeSpan.FromMilliseconds(50),
    ];

    internal static bool IsPllPhaseSearchTimeout(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("SetPllFrequency", StringComparison.OrdinalIgnoreCase)
            && (
                message.Contains("timeout, busy bit is still 1", StringComparison.OrdinalIgnoreCase)
                || message.Contains("PHCFG_DONE", StringComparison.OrdinalIgnoreCase)
            );
    }

    internal static TimeSpan? GetRetryDelay(int resultCode, int completedAttempt)
    {
        if (completedAttempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(completedAttempt));
        }

        if (resultCode == 0 || completedAttempt > RetryDelays.Length)
        {
            return null;
        }

        return RetryDelays[completedAttempt - 1];
    }
}
