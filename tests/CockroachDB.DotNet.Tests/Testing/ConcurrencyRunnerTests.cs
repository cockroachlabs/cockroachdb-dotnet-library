using CockroachDB.DotNet.Testing;

namespace CockroachDB.DotNet.Tests.Testing;

public sealed class ConcurrencyRunnerTests
{
    [Fact]
    public async Task AllSucceed_CountsCorrectly()
    {
        var result = await CockroachDBConcurrencyRunner.RunAsync(
            threads: 4, iterationsPerThread: 10,
            action: () => Task.CompletedTask);

        Assert.Equal(40, result.TotalOperations);
        Assert.Equal(40, result.Successes);
        Assert.Equal(0, result.Failures);
        Assert.Empty(result.Errors);
        Assert.True(result.OpsPerSecond > 0);
    }

    [Fact]
    public async Task AllFail_CountsCorrectly()
    {
        var result = await CockroachDBConcurrencyRunner.RunAsync(
            threads: 2, iterationsPerThread: 5,
            action: () => throw new InvalidOperationException("boom"));

        Assert.Equal(10, result.TotalOperations);
        Assert.Equal(0, result.Successes);
        Assert.Equal(10, result.Failures);
        Assert.Equal(10, result.Errors.Count);
    }

    [Fact]
    public async Task MixedResults_TracksSuccessesAndFailures()
    {
        var counter = 0;
        var result = await CockroachDBConcurrencyRunner.RunAsync(
            threads: 1, iterationsPerThread: 10,
            action: () =>
            {
                if (Interlocked.Increment(ref counter) % 2 == 0)
                    throw new InvalidOperationException("even");
                return Task.CompletedTask;
            });

        Assert.Equal(10, result.TotalOperations);
        Assert.Equal(5, result.Successes);
        Assert.Equal(5, result.Failures);
    }

    [Fact]
    public async Task ConcurrentExecution_AllThreadsRun()
    {
        var threadIds = new System.Collections.Concurrent.ConcurrentBag<int>();

        var result = await CockroachDBConcurrencyRunner.RunAsync(
            threads: 4, iterationsPerThread: 1,
            action: () =>
            {
                threadIds.Add(Environment.CurrentManagedThreadId);
                return Task.CompletedTask;
            });

        Assert.Equal(4, result.TotalOperations);
        Assert.Equal(4, result.Successes);
    }
}
