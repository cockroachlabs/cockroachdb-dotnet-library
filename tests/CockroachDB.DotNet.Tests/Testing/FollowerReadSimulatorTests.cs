using CockroachDB.DotNet.Core.FollowerRead;
using CockroachDB.DotNet.Testing;

namespace CockroachDB.DotNet.Tests.Testing;

public sealed class FollowerReadSimulatorTests
{
    [Fact]
    public void ActivateFollowerRead_SetsContext()
    {
        Assert.False(FollowerReadContext.Current);

        using (FollowerReadSimulator.ActivateFollowerRead())
        {
            Assert.True(FollowerReadContext.Current);
        }

        Assert.False(FollowerReadContext.Current);
    }

    [Fact]
    public async Task MeasureStaleness_WhenImmediatelyVisible_ReturnsLowLatency()
    {
        var written = false;

        var result = await FollowerReadSimulator.MeasureStalenessAsync(
            writeOperation: () => { written = true; return Task.CompletedTask; },
            followerReadCheck: () => Task.FromResult(written),
            maxWaitMs: 1000,
            pollIntervalMs: 10);

        Assert.True(result.WasVisible);
        Assert.True(result.WriteToVisibleMs >= 0);
        Assert.True(result.WriteToVisibleMs < 1000);
    }

    [Fact]
    public async Task MeasureStaleness_WhenNeverVisible_ReportsTimeout()
    {
        var result = await FollowerReadSimulator.MeasureStalenessAsync(
            writeOperation: () => Task.CompletedTask,
            followerReadCheck: () => Task.FromResult(false),
            maxWaitMs: 200,
            pollIntervalMs: 50);

        Assert.False(result.WasVisible);
        Assert.Equal(-1, result.WriteToVisibleMs);
        Assert.Equal(200, result.MaxWaitMs);
    }

    [Fact]
    public async Task MeasureStaleness_SetsFollowerReadContext_DuringCheck()
    {
        var wasFollowerReadActive = false;

        var result = await FollowerReadSimulator.MeasureStalenessAsync(
            writeOperation: () => Task.CompletedTask,
            followerReadCheck: () =>
            {
                wasFollowerReadActive = FollowerReadContext.Current;
                return Task.FromResult(true);
            },
            maxWaitMs: 1000,
            pollIntervalMs: 10);

        Assert.True(wasFollowerReadActive);
        Assert.True(result.WasVisible);
    }
}
