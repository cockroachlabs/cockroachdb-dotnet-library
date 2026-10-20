using System.Collections.Concurrent;
using System.Diagnostics;

namespace CockroachDB.DotNet.Testing;

public static class CockroachDBConcurrencyRunner
{
    public static async Task<ConcurrencyResult> RunAsync(
        int threads,
        int iterationsPerThread,
        Func<Task> action)
    {
        var barrier = new SemaphoreSlim(0, threads);
        var successes = 0;
        var failures = 0;
        var errors = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, threads).Select(async _ =>
        {
            await barrier.WaitAsync();

            for (var i = 0; i < iterationsPerThread; i++)
            {
                try
                {
                    await action();
                    Interlocked.Increment(ref successes);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failures);
                    errors.Add(ex);
                }
            }
        }).ToArray();

        var stopwatch = Stopwatch.StartNew();
        barrier.Release(threads);
        await Task.WhenAll(tasks);
        stopwatch.Stop();

        return new ConcurrencyResult(
            Threads: threads,
            IterationsPerThread: iterationsPerThread,
            TotalOperations: threads * iterationsPerThread,
            Successes: successes,
            Failures: failures,
            DurationMs: stopwatch.ElapsedMilliseconds,
            Errors: [.. errors]);
    }
}

public sealed record ConcurrencyResult(
    int Threads,
    int IterationsPerThread,
    int TotalOperations,
    int Successes,
    int Failures,
    long DurationMs,
    IReadOnlyList<Exception> Errors)
{
    public double OpsPerSecond => TotalOperations / (DurationMs / 1000.0);
}
