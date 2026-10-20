namespace CockroachDB.DotNet.Abstractions.Metrics;

public interface ICockroachDBMetrics
{
    void RecordTransaction(long startTimestamp, int retries, bool success);

    void RecordFollowerRead(long startTimestamp, bool success);

    long TransactionCount { get; }

    long RetryCount { get; }

    long FailureCount { get; }

    long FollowerReadCount { get; }

    double AverageDurationMs { get; }
}
