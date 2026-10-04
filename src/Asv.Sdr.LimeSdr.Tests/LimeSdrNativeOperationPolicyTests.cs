using Asv.Sdr.LimeSdr;
using Xunit;

namespace Asv.Sdr.LimeSdr.Tests;

public sealed class LimeSdrNativeOperationPolicyTests
{
    [Fact]
    public void GetRetryDelay_SuccessAfterTransientPllDiagnostic_DoesNotRetry()
    {
        const string message = "SetPllFrequency: timeout, busy bit is still 1";

        Assert.True(LimeSdrNativeOperationPolicy.IsPllPhaseSearchTimeout(message));
        Assert.Null(LimeSdrNativeOperationPolicy.GetRetryDelay(0, 1));
    }

    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 50)]
    public void GetRetryDelay_NonzeroResultWithinBudget_ReturnsExpectedDelay(
        int completedAttempt,
        int expectedDelayMilliseconds
    )
    {
        var actual = LimeSdrNativeOperationPolicy.GetRetryDelay(-1, completedAttempt);

        Assert.Equal(TimeSpan.FromMilliseconds(expectedDelayMilliseconds), actual);
    }

    [Fact]
    public void GetRetryDelay_NonzeroResultAfterRetryBudget_ReturnsNull()
    {
        Assert.Null(LimeSdrNativeOperationPolicy.GetRetryDelay(-1, 3));
    }

    [Theory]
    [InlineData("SetPllFrequency: timeout, busy bit is still 1")]
    [InlineData("SetPllFrequency: PHCFG_DONE did not assert before timeout")]
    public void IsPllPhaseSearchTimeout_KnownNativeMessages_ReturnsTrue(string message)
    {
        Assert.True(LimeSdrNativeOperationPolicy.IsPllPhaseSearchTimeout(message));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("LML RX phase search FAIL")]
    [InlineData("Call LMS_SetSampleRate error: generic failure")]
    public void IsPllPhaseSearchTimeout_UnrelatedMessage_ReturnsFalse(string? message)
    {
        Assert.False(LimeSdrNativeOperationPolicy.IsPllPhaseSearchTimeout(message));
    }
}
