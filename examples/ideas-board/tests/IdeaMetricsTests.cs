using CockroachDB.DotNet.Abstractions.Metrics;
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
/// Proves UseMetrics() records transactions against a real cluster. The library's
/// metrics are not OpenTelemetry at this version: UseMetrics() registers an
/// ICockroachDBMetrics singleton (InMemoryMetrics) and MetricsMiddleware calls
/// RecordTransaction exactly once per pipeline operation. The test resolves that
/// instrument and asserts the transaction count rises as upvotes run through
/// crdb.ExecuteAsync.
/// </summary>
[Trait("Category", "Integration")]
public class IdeaMetricsTests : IAsyncLifetime
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
    public async Task UpvoteAsync_RecordsTransactionMetrics()
    {
        var service = _sp.GetRequiredService<IdeaService>();
        var metrics = _sp.GetRequiredService<ICockroachDBMetrics>();
        var id = await service.SeedAsync("instrument me");

        // tag::metrics-test[]
        var before = metrics.TransactionCount;

        const int upvotes = 5;
        for (var i = 0; i < upvotes; i++)
            await service.UpvoteAsync(id);

        var recorded = metrics.TransactionCount - before;
        Assert.True(
            recorded >= upvotes,
            $"expected >= {upvotes} transactions recorded, got {recorded}");
        Assert.True(metrics.TransactionCount > 0);
        // end::metrics-test[]
    }
}
