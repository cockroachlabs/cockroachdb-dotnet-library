using CockroachDB.DotNet.Abstractions.Template;
using CockroachDB.DotNet.Core.Region;
using Microsoft.EntityFrameworkCore;

namespace IdeasBoard;

/// <summary>
/// Region-scoped writes for REGIONAL BY ROW ideas. Each write is routed through the
/// template's <c>InRegion(region)</c> execution (which flows the region through the
/// pipeline's RegionMiddleware), and <see cref="RegionContext"/> is scoped so the EF
/// <c>CrdbRegionSaveChangesInterceptor</c> stamps the row's <c>crdb_region</c> column
/// (the <c>[CrdbRegion]</c> property) on insert — that column is what pins the row to
/// a region. CockroachDB has no <c>crdb_region</c> session variable, so homing is done
/// by the column, never with <c>SET</c>. This only does anything meaningful on a
/// multi-region cluster.
/// </summary>
// tag::inregion-usage[]
public sealed class RegionalIdeaService(
    IDbContextFactory<AppDbContext> dbFactory,
    ICockroachDBTemplate crdb)
{
    // Write an idea pinned to a region. InRegion(...) scopes the unit of work to the
    // region; RegionContext.Scoped makes the EF save-changes interceptor stamp the
    // crdb_region column — we do NOT set Region by hand, the library homes the row.
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
