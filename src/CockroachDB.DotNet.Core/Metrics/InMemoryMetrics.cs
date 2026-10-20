using System.Diagnostics;
using CockroachDB.DotNet.Abstractions.Metrics;

namespace CockroachDB.DotNet.Core.Metrics;

public sealed class InMemoryMetrics : ICockroachDBMetrics
{
    private long _transactionCount;
    private long _retryCount;
    private long _failureCount;
    private long _followerReadCount;
    private long _totalDurationTicks;

    public void RecordTransaction(long startTimestamp, int retries, bool success)
    {
        var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
        Interlocked.Increment(ref _transactionCount);
        Interlocked.Add(ref _retryCount, retries);
        Interlocked.Add(ref _totalDurationTicks, elapsed);

        if (!success)
            Interlocked.Increment(ref _failureCount);
    }

    public void RecordFollowerRead(long startTimestamp, bool success)
    {
        Interlocked.Increment(ref _followerReadCount);
        if (!success)
            Interlocked.Increment(ref _failureCount);
    }

    public long TransactionCount => Interlocked.Read(ref _transactionCount);

    public long RetryCount => Interlocked.Read(ref _retryCount);

    public long FailureCount => Interlocked.Read(ref _failureCount);

    public long FollowerReadCount => Interlocked.Read(ref _followerReadCount);

    public double AverageDurationMs
    {
        get
        {
            var count = TransactionCount;
            if (count == 0) return 0;

            var totalTicks = Interlocked.Read(ref _totalDurationTicks);
            var totalMs = (double)totalTicks / Stopwatch.Frequency * 1000;
            return totalMs / count;
        }
    }
}
