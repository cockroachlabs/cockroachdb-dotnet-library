using CockroachDB.DotNet.Core.Extensions;
using CockroachDB.DotNet.EntityFramework.Extensions;
using CockroachDB.DotNet.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace IdeasBoard.Tests;

/// <summary>
/// Proves the hero feature against a real CockroachDB (Testcontainers):
/// ExecuteAsync + the retry middleware turns a contended read-modify-write
/// into a lossless one, while the plain EF path loses votes or fails.
/// </summary>
[Trait("Category", "Integration")]
public class IdeaServiceConcurrencyTests : IAsyncLifetime
{
    private readonly CockroachDBFixture _fixture = new();
    private ServiceProvider _sp = null!;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CockroachDB:Retry:MaxRetries"] = "50",
                ["CockroachDB:Retry:DelayMs"] = "10",
                ["CockroachDB:Retry:Multiplier"] = "1.5",
                ["CockroachDB:Retry:MaxDelayMs"] = "500",
                ["CockroachDB:Retry:JitterMs"] = "10",
            })
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
        services.AddSingleton<IdeaService>();
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
    public async Task UpvoteAsync_UnderContention_CountsEveryVote()
    {
        var service = _sp.GetRequiredService<IdeaService>();
        var id = await service.SeedAsync("Add dark mode");

        // tag::concurrency-test[]
        var result = await CockroachDBConcurrencyRunner.RunAsync(
            threads: 3,
            iterationsPerThread: 20,
            () => service.UpvoteAsync(id));

        Assert.Equal(0, result.Failures);
        Assert.Equal(60, result.Successes);
        Assert.Equal(60, await service.CurrentVotesAsync(id));
        // end::concurrency-test[]
    }

    [Fact]
    public async Task UpvoteWithoutRetry_UnderContention_LosesVotesOrFails()
    {
        var service = _sp.GetRequiredService<IdeaService>();
        var id = await service.SeedAsync("Ship it Friday");

        var result = await CockroachDBConcurrencyRunner.RunAsync(
            threads: 3,
            iterationsPerThread: 20,
            () => service.UpvoteWithoutRetryAsync(id));

        var finalVotes = await service.CurrentVotesAsync(id);

        // No retry => at least one 40001 failure, or lost updates (votes < 60).
        Assert.True(
            result.Failures > 0 || finalVotes < 60,
            $"expected contention damage but got failures={result.Failures}, votes={finalVotes}");
    }
}
