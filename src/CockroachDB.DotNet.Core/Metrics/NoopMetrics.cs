using CockroachDB.DotNet.Abstractions.Metrics;

namespace CockroachDB.DotNet.Core.Metrics;

public sealed class NoopMetrics : ICockroachDBMetrics
{
    public void RecordTransaction(long startTimestamp, int retries, bool success) { }

    public void RecordFollowerRead(long startTimestamp, bool success) { }

    public long TransactionCount => 0;

    public long RetryCount => 0;

    public long FailureCount => 0;

    public long FollowerReadCount => 0;

    public double AverageDurationMs => 0;
}
