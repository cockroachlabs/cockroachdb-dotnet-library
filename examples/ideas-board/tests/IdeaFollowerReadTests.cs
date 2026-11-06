using CockroachDB.DotNet.Core.Extensions;
using CockroachDB.DotNet.Core.Middleware;
using CockroachDB.DotNet.EntityFramework.Extensions;
using CockroachDB.DotNet.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Diagnostics;
using Xunit;

namespace IdeasBoard.Tests;

/// <summary>
/// Proves the leaderboard reads through CockroachDB follower reads
/// (AS OF SYSTEM TIME follower_read_timestamp()) against a real cluster. Because a
/// follower read is ~4.2s stale, the test polls until the seed is visible, then
/// asserts the rows come back ordered by votes and non-empty.
/// </summary>
[Trait("Category", "Integration")]
public class IdeaFollowerReadTests : IAsyncLifetime
{
    private readonly CockroachDBFixture _fixture = new();
    private ServiceProvider _sp = null!;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NpgsqlDataSource.Create(_fixture.ConnectionString));
        services.AddCockroachDB(config, pipeline =>
        {
            pipeline.UseMetrics();
            pipeline.UseCircuitBreaker();
            pipeline.UseRetry();
            pipeline.UseRegion();
            pipeline.UseFollowerRead();
        });
        services.AddDbContextFactory<AppDbContext>(
            o => o.UseCockroachDB(_fixture.ConnectionString));
        services.AddSingleton<IdeaReadService>();
        _sp = services.BuildServiceProvider();

        await using var db = await _sp
            .GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync(AppDbContext.CreateTableSql);
    }

    public async Task DisposeAsync()
    {
        await _sp.DisposeAsync();
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task LeaderboardAsync_ReturnsSeededIdeas_OrderedByVotes()
    {
        var factory = _sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var read = _sp.GetRequiredService<IdeaReadService>();

        // Seed three ideas with distinct vote counts in a single write so the rows
        // carry their final votes (no later update to race the follower read).
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Ideas.AddRange(
                new Idea { Id = Guid.NewGuid(), Text = "add search", Votes = 1 },
                new Idea { Id = Guid.NewGuid(), Text = "dark mode", Votes = 5 },
                new Idea { Id = Guid.NewGuid(), Text = "export to CSV", Votes = 9 });
            await db.SaveChangesAsync();
        }

        // tag::follower-read-test[]
        // Follower reads run AS OF SYSTEM TIME follower_read_timestamp() (~4.2s in
        // the past). Right after the cluster starts that timestamp can predate the
        // seed (empty result) or even the database (transient 3D000, which can trip
        // the circuit breaker). Wait out the follower-read window, then poll until
        // the AS-OF window advances past the seed and assert order + non-empty.
        await Task.Delay(TimeSpan.FromSeconds(6));

        var board = new List<Idea>();
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            try
            {
                board = await read.LeaderboardAsync();
                if (board.Count >= 3) break;
            }
            catch (Exception ex) when (ex is PostgresException or CockroachDBCircuitBreakerOpenException)
            {
                // AS-OF window still before the seed/DB, or breaker cooling down.
            }
            await Task.Delay(500);
        }

        Assert.NotEmpty(board);
        Assert.True(board.Count >= 3, $"expected >= 3 ideas visible, got {board.Count}");

        var votes = board.Select(i => i.Votes).ToList();
        Assert.Equal(votes.OrderByDescending(v => v).ToList(), votes);
        Assert.Equal(9, board[0].Votes);
        // end::follower-read-test[]
    }
}
