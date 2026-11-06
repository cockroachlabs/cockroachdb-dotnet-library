using CockroachDB.DotNet.Abstractions.Template;
using Microsoft.EntityFrameworkCore;

namespace IdeasBoard;

/// <summary>
/// Read-side of the Ideas Board. The leaderboard is a pure read, so it runs as a
/// follower read: CockroachDB serves it from the nearest replica at
/// <c>follower_read_timestamp()</c> (~4.2s stale) for lower latency and zero
/// contention with the write path. The EF query inside the lambda picks up
/// <c>SET TRANSACTION AS OF SYSTEM TIME</c> from the activated follower-read scope.
/// </summary>
// tag::follower-read[]
public sealed class IdeaReadService(
    IDbContextFactory<AppDbContext> dbFactory,
    ICockroachDBTemplate crdb)
{
    // Top ideas by votes, read from a follower replica. Reads are slightly stale
    // (follower_read_timestamp()), which is exactly what a leaderboard can tolerate.
    public Task<List<Idea>> LeaderboardAsync(int top = 10) =>
        crdb.ExecuteFollowerReadAsync(async () =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            return await db.Ideas
                .AsNoTracking()
                .OrderByDescending(i => i.Votes)
                .ThenBy(i => i.Id)
                .Take(top)
                .ToListAsync();
        });
}
// end::follower-read[]
