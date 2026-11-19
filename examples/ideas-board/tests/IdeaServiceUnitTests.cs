using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace IdeasBoard.Tests;

/// <summary>
/// Fast, in-process unit tests for IdeaService's data logic. These run against
/// EF Core's InMemory provider — no Docker, no CockroachDB cluster — so they
/// exercise the service/entity read-modify-write logic in milliseconds.
///
/// NOTE: EF InMemory does NOT model SERIALIZABLE isolation or SQLSTATE 40001
/// (serialization failure). The contention + retry behavior (UpvoteAsync and
/// the retry middleware) is covered by the Testcontainers integration suite
/// (IdeaServiceConcurrencyTests, "Category=Integration"). These unit tests
/// cover only the plain service/entity logic.
/// </summary>
[Trait("Category", "Unit")]
public class IdeaServiceUnitTests
{
    // tag::unit-test[]
    // IdeaService depends on IDbContextFactory<AppDbContext>. This test-only shim
    // hands out contexts backed by a shared InMemory database — seed in one
    // context, read back in another, exactly like the production factory.
    // TransactionIgnoredWarning is suppressed because UpvoteWithoutRetryAsync opens
    // a transaction, which the InMemory provider treats as a no-op.
    private sealed class InMemoryDbContextFactory(string dbName) : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

        public AppDbContext CreateDbContext() => new(_options);
    }

    // crdb (ICockroachDBTemplate) is only used by the retry path (UpvoteAsync),
    // which these unit tests do not call, so null! is a safe test seam here.
    private static IdeaService NewService() =>
        new(new InMemoryDbContextFactory(Guid.NewGuid().ToString()), crdb: null!);

    [Fact]
    public async Task UpvoteWithoutRetryAsync_IncrementsVotesByExactlyOne()
    {
        var service = NewService();
        var id = await service.SeedAsync("Add dark mode");

        await service.UpvoteWithoutRetryAsync(id);

        Assert.Equal(1, await service.CurrentVotesAsync(id));
    }

    [Fact]
    public async Task ResetVotesAsync_SetsVotesToZero()
    {
        var service = NewService();
        var id = await service.SeedAsync("Ship it Friday");
        await service.UpvoteWithoutRetryAsync(id);
        await service.UpvoteWithoutRetryAsync(id);

        await service.ResetVotesAsync(id);

        Assert.Equal(0, await service.CurrentVotesAsync(id));
    }

    [Fact]
    public async Task CurrentVotesAsync_ReturnsStoredValue()
    {
        var service = NewService();
        var id = await service.SeedAsync("Dark theme by default");

        // A freshly seeded idea starts at zero; each upvote adds exactly one.
        Assert.Equal(0, await service.CurrentVotesAsync(id));
        await service.UpvoteWithoutRetryAsync(id);
        await service.UpvoteWithoutRetryAsync(id);
        await service.UpvoteWithoutRetryAsync(id);
        Assert.Equal(3, await service.CurrentVotesAsync(id));
    }
    // end::unit-test[]

    [Fact]
    public void Idea_NewInstance_DefaultsToZeroVotesAndEmptyText()
    {
        var idea = new Idea();

        Assert.Equal(0, idea.Votes);
        Assert.Equal("", idea.Text);
    }
}
