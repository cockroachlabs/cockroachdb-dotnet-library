using System.Diagnostics;
using CockroachDB.DotNet.Core.Metrics;

namespace CockroachDB.DotNet.Tests.Metrics;

public sealed class InMemoryMetricsTests
{
    [Fact]
    public void RecordTransaction_IncrementsCount()
    {
        var metrics = new InMemoryMetrics();
        metrics.RecordTransaction(Stopwatch.GetTimestamp(), 0, true);
        metrics.RecordTransaction(Stopwatch.GetTimestamp(), 0, true);

        Assert.Equal(2, metrics.TransactionCount);
    }

    [Fact]
    public void RecordTransaction_AccumulatesRetries()
    {
        var metrics = new InMemoryMetrics();
        metrics.RecordTransaction(Stopwatch.GetTimestamp(), 2, true);
        metrics.RecordTransaction(Stopwatch.GetTimestamp(), 3, true);

        Assert.Equal(5, metrics.RetryCount);
    }

    [Fact]
    public void Failure_IncrementsFailureCount()
    {
        var metrics = new InMemoryMetrics();
        metrics.RecordTransaction(Stopwatch.GetTimestamp(), 0, true);
        metrics.RecordTransaction(Stopwatch.GetTimestamp(), 0, false);
        metrics.RecordTransaction(Stopwatch.GetTimestamp(), 0, false);

        Assert.Equal(3, metrics.TransactionCount);
        Assert.Equal(2, metrics.FailureCount);
    }

    [Fact]
    public void AverageDurationMs_ZeroWhenEmpty()
    {
        var metrics = new InMemoryMetrics();
        Assert.Equal(0, metrics.AverageDurationMs);
    }

    [Fact]
    public void AverageDurationMs_PositiveAfterRecording()
    {
        var metrics = new InMemoryMetrics();
        var start = Stopwatch.GetTimestamp();
        Thread.Sleep(10);
        metrics.RecordTransaction(start, 0, true);

        Assert.True(metrics.AverageDurationMs > 0);
    }

    [Fact]
    public void RecordFollowerRead_IncrementsCount()
    {
        var metrics = new InMemoryMetrics();
        metrics.RecordFollowerRead(Stopwatch.GetTimestamp(), true);
        metrics.RecordFollowerRead(Stopwatch.GetTimestamp(), true);

        Assert.Equal(2, metrics.FollowerReadCount);
    }

    [Fact]
    public void RecordFollowerRead_Failure_IncrementsFailureCount()
    {
        var metrics = new InMemoryMetrics();
        metrics.RecordFollowerRead(Stopwatch.GetTimestamp(), false);

        Assert.Equal(1, metrics.FollowerReadCount);
        Assert.Equal(1, metrics.FailureCount);
    }
}
