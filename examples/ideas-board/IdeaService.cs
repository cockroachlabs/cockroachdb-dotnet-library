using System.Data;
using CockroachDB.DotNet.Abstractions.Template;
using Microsoft.EntityFrameworkCore;

namespace IdeasBoard;

/// <summary>
/// Two upvote paths over the same read-modify-write unit of work. Each upvote
/// opens a serializable transaction, reads the row, increments Votes, and
/// commits — so concurrent callers genuinely contend and CockroachDB raises
/// SQLSTATE 40001 (serialization failure) on the losers.
/// </summary>
public class IdeaService(
    IDbContextFactory<AppDbContext> dbFactory,
    ICockroachDBTemplate crdb)
{
    public async Task<Guid> SeedAsync(string text)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var idea = new Idea { Id = Guid.NewGuid(), Text = text, Votes = 0 };
        db.Ideas.Add(idea);
        await db.SaveChangesAsync();
        return idea.Id;
    }

    // tag::plain[]
    // No retry: a 40001 on commit propagates straight to the caller, so under
    // contention upvotes are lost or the call throws.
    public async Task UpvoteWithoutRetryAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        // SERIALIZABLE so the conflict surfaces as 40001 (Npgsql would otherwise
        // request READ COMMITTED, which CockroachDB honors and silently loses the update).
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var idea = await db.Ideas.SingleAsync(i => i.Id == id);
        idea.Votes++;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
    // end::plain[]

    // tag::retry[]
    // Same unit of work wrapped in ExecuteAsync. The retry middleware absorbs
    // 40001 and re-runs the whole lambda (fresh context + fresh transaction +
    // fresh read) until it commits, so no upvote is lost.
    public Task UpvoteAsync(Guid id) =>
        crdb.ExecuteAsync(async () =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var idea = await db.Ideas.SingleAsync(i => i.Id == id);
            idea.Votes++;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        });
    // end::retry[]

    public async Task<int> CurrentVotesAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var idea = await db.Ideas.SingleAsync(i => i.Id == id);
        return idea.Votes;
    }

    public async Task ResetVotesAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var idea = await db.Ideas.SingleAsync(i => i.Id == id);
        idea.Votes = 0;
        await db.SaveChangesAsync();
    }
}
