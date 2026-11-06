using CockroachDB.DotNet.Abstractions.Template;
using CockroachDB.DotNet.Core.Region;
using Microsoft.EntityFrameworkCore;

namespace IdeasBoard;

/// <summary>
/// Region-scoped writes for REGIONAL BY ROW ideas. Each write is routed through the
/// template's <c>InRegion(region)</c> execution (which flows the region through the
/// pipeline's RegionMiddleware), and <see cref="RegionContext"/> is scoped so the EF
/// command interceptor prepends <c>SET crdb_region</c>. The row itself is pinned by
/// its <c>crdb_region</c> column (the <c>[CrdbRegion]</c> property). This only does
/// anything meaningful on a multi-region cluster.
/// </summary>
// tag::inregion-usage[]
public sealed class RegionalIdeaService(
    IDbContextFactory<AppDbContext> dbFactory,
    ICockroachDBTemplate crdb)
{
    // Write an idea pinned to a region. InRegion(...) scopes the unit of work to the
    // region; RegionContext.Scoped makes the EF interceptor emit SET crdb_region; and
    // the entity's Region column records where the row lives.
    public Task<Guid> AddIdeaInRegionAsync(string region, string text) =>
        crdb.InRegion(region).ExecuteAsync(async () =>
        {
            using var _ = RegionContext.Scoped(region);
            await using var db = await dbFactory.CreateDbContextAsync();
            var idea = new RegionalIdea
            {
                Id = Guid.NewGuid(),
                Text = text,
                Votes = 0,
                Region = region,
            };
            db.RegionalIdeas.Add(idea);
            await db.SaveChangesAsync();
            return idea.Id;
        });

    // Read back the region a row was pinned to.
    public async Task<string> RegionOfAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var idea = await db.RegionalIdeas.SingleAsync(i => i.Id == id);
        return idea.Region;
    }
}
// end::inregion-usage[]
