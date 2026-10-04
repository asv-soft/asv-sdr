using System.Reflection;
using Asv.Sdr.LimeSdr;
using Xunit;

namespace Asv.Sdr.LimeSdr.Tests;

public sealed class Ifr6000DfReadBudgetTests
{
    [Fact]
    public void RequestDfMessage_TypedDefaults_UseTenAttemptsInsteadOfTimeout()
    {
        var methods = typeof(LimeSdrIfr6000Device)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(x => x.Name == "RequestDfMessage").ToArray();

        Assert.Equal(2, methods.Length);
        Assert.All(methods, method =>
        {
            Assert.DoesNotContain(method.GetParameters(), x => x.Name == "timeoutMs");
            Assert.Equal(10, method.GetParameters().Single(x => x.Name == "attempts").DefaultValue);
        });
    }

    [Fact]
    public void TryBeginRead_ThreeSlowAttempts_IgnoresElapsedTimeout()
    {
        var budget = new Ifr6000DfReadBudget(1_000, 30, attempts: 3);

        Assert.True(budget.TryBeginRead(1_000));
        Assert.True(budget.TryBeginRead(1_080));
        Assert.True(budget.TryBeginRead(1_160));
        Assert.False(budget.TryBeginRead(1_240));
        Assert.Equal(3, budget.ReadsStarted);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    public void TryBeginRead_ExplicitAttemptLimit_StopsAtRequestedCount(int attempts)
    {
        var budget = new Ifr6000DfReadBudget(0, 30, attempts);

        for (var read = 0; read < attempts; read++)
        {
            Assert.True(budget.TryBeginRead(read * 80L));
        }

        Assert.False(budget.TryBeginRead(attempts * 80L));
        Assert.False(budget.TryBeginRead(attempts * 80L));
        Assert.Equal(attempts, budget.ReadsStarted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void TryBeginRead_NonpositiveAttempts_AllowsExactlyOneRead(int attempts)
    {
        var budget = new Ifr6000DfReadBudget(0, 30, attempts);

        Assert.True(budget.TryBeginRead(80));
        Assert.False(budget.TryBeginRead(160));
        Assert.Equal(1, budget.ReadsStarted);
    }

    [Fact]
    public void TryBeginRead_DeadlineBudget_StopsAtDeadlineWithoutCountingRejection()
    {
        var budget = new Ifr6000DfReadBudget(1_000, 100);

        Assert.True(budget.TryBeginRead(1_000));
        Assert.True(budget.TryBeginRead(1_080));
        Assert.True(budget.TryBeginRead(1_099));
        Assert.False(budget.TryBeginRead(1_100));
        Assert.False(budget.TryBeginRead(1_160));
        Assert.Equal(3, budget.ReadsStarted);
    }

    [Fact]
    public void TryBeginRead_FirstReadAfterDeadline_PreservesLegacyDoWhileBehavior()
    {
        var budget = new Ifr6000DfReadBudget(1_000, 100);

        Assert.Equal(0, budget.ReadsStarted);
        Assert.True(budget.TryBeginRead(1_200));
        Assert.False(budget.TryBeginRead(1_200));
        Assert.Equal(1, budget.ReadsStarted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void TryBeginRead_NonpositiveTimeout_NormalizesToOneMillisecond(int timeoutMs)
    {
        var budget = new Ifr6000DfReadBudget(1_000, timeoutMs);

        Assert.True(budget.TryBeginRead(1_000));
        Assert.True(budget.TryBeginRead(1_000));
        Assert.False(budget.TryBeginRead(1_001));
        Assert.Equal(2, budget.ReadsStarted);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task BudgetLoop_SuccessfulRead_ExitsBeforeRemainingAttempts(int successOnRead)
    {
        var budget = new Ifr6000DfReadBudget(0, 30, attempts: 3);
        var reads = 0;

        var result = await RunBudgetLoopAsync(
            budget,
            () => reads * 80L,
            _ => Task.FromResult(++reads == successOnRead ? "reply" : null),
            TestContext.Current.CancellationToken);

        Assert.Equal("reply", result);
        Assert.Equal(successOnRead, reads);
        Assert.Equal(successOnRead, budget.ReadsStarted);
    }

    [Fact]
    public async Task BudgetLoop_NineSlowReadsRejected_AcceptsTenthReply()
    {
        var budget = new Ifr6000DfReadBudget(0, 30, attempts: 10);
        var reads = 0;
        var nowMs = 0L;

        var result = await RunBudgetLoopAsync(
            budget,
            () => nowMs,
            _ =>
            {
                nowMs += 80;
                return Task.FromResult(++reads == 10 ? "reply" : null);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("reply", result);
        Assert.Equal(10, reads);
        Assert.Equal(10, budget.ReadsStarted);
        Assert.Equal(800L, nowMs);
        Assert.False(budget.TryBeginRead(nowMs));
    }

    [Fact]
    public async Task BudgetLoop_AllReadsRejected_ConsumesExactlyThreeAttempts()
    {
        var budget = new Ifr6000DfReadBudget(0, 30, attempts: 3);
        var reads = 0;

        var result = await RunBudgetLoopAsync(
            budget,
            () => reads * 80L,
            _ =>
            {
                reads++;
                return Task.FromResult<string?>(null);
            },
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(3, reads);
        Assert.Equal(3, budget.ReadsStarted);
    }

    [Fact]
    public async Task BudgetLoop_CancelledBeforeRead_DoesNotBeginAnAttempt()
    {
        var budget = new Ifr6000DfReadBudget(0, 30, attempts: 3);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunBudgetLoopAsync(
            budget,
            () => 0,
            _ => throw new InvalidOperationException("A cancelled read must not start."),
            cancellation.Token));

        Assert.Equal(0, budget.ReadsStarted);
    }

    [Fact]
    public async Task BudgetLoop_CancelledDuringRead_PropagatesWithoutAnotherAttempt()
    {
        var budget = new Ifr6000DfReadBudget(0, 30, attempts: 3);
        using var cancellation = new CancellationTokenSource();
        var reads = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunBudgetLoopAsync(
            budget,
            () => reads * 80L,
            token =>
            {
                reads++;
                cancellation.Cancel();
                return Task.FromCanceled<string?>(token);
            },
            cancellation.Token));

        Assert.Equal(1, reads);
        Assert.Equal(1, budget.ReadsStarted);
    }

    // This harness tests budget-loop composition, not native device I/O or DF decoding.
    private static async Task<string?> RunBudgetLoopAsync(
        Ifr6000DfReadBudget budget,
        Func<long> nowMs,
        Func<CancellationToken, Task<string?>> readAttempt,
        CancellationToken cancel = default)
    {
        while (true)
        {
            cancel.ThrowIfCancellationRequested();
            if (!budget.TryBeginRead(nowMs()))
            {
                return null;
            }

            var result = await readAttempt(cancel);
            if (result is not null)
            {
                return result;
            }
        }
    }
}
