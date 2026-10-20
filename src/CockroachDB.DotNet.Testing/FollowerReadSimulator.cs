using CockroachDB.DotNet.Core.FollowerRead;

namespace CockroachDB.DotNet.Testing;

public static class FollowerReadSimulator
{
    public static FollowerReadScope ActivateFollowerRead()
    {
        return FollowerReadContext.Scoped();
    }

    public static async Task<FollowerReadResult> MeasureStalenessAsync(
        Func<Task> writeOperation,
        Func<Task<bool>> followerReadCheck,
        int maxWaitMs = 5000,
        int pollIntervalMs = 100)
    {
        await writeOperation();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var visible = false;

        while (stopwatch.ElapsedMilliseconds < maxWaitMs)
        {
            using (FollowerReadContext.Scoped())
            {
                visible = await followerReadCheck();
            }

            if (visible) break;
            await Task.Delay(pollIntervalMs);
        }

        stopwatch.Stop();

        return new FollowerReadResult(
            WriteToVisibleMs: visible ? stopwatch.ElapsedMilliseconds : -1,
            WasVisible: visible,
            MaxWaitMs: maxWaitMs);
    }
}

public sealed record FollowerReadResult(
    long WriteToVisibleMs,
    bool WasVisible,
    int MaxWaitMs);
