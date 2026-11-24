using CockroachDB.DotNet.Core.Extensions;
using CockroachDB.DotNet.EntityFramework.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace IdeasBoard.Tests;

/// <summary>
/// Proves region-scoped (REGIONAL BY ROW) writes. This behaviour only exists on a
/// multi-region cluster, so on a single node the test SKIPS cleanly. Point it at a
/// real multi-region cluster with CRDB_MULTIREGION=true and CRDB_URL=&lt;conn&gt; to run.
/// </summary>
[Trait("Category", "Integration")]
public class RegionalIdeaTests
{
    [SkippableFact]
    public async Task AddIdeaInRegion_PinsRowToRegion()
    {
        // tag::region-test[]
        // Multi-region behaviour can't be exercised on a single node, so skip unless
        // explicitly pointed at a real multi-region cluster. Skip.IfNot reports this
        // as skipped (not passed, not failed) and lets the test run when enabled.
        Skip.IfNot(Environment.GetEnvironmentVariable("CRDB_MULTIREGION") == "true",
            "Set CRDB_MULTIREGION=true and CRDB_URL=<multi-region cluster> to run this test.");

        var connectionString = Environment.GetEnvironmentVariable("CRDB_URL")
            ?? throw new InvalidOperationException("CRDB_URL is required when CRDB_MULTIREGION=true.");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CockroachDB:Region:Validate"] = "true",
                ["CockroachDB:Region:Allowed:0"] = "us-east1",
                ["CockroachDB:Region:Allowed:1"] = "us-west1",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddCockroachDB(config, pipeline =>
        {
            pipeline.UseMetrics();
            pipeline.UseCircuitBreaker();
            pipeline.UseRetry();
            pipeline.UseRegion();
            pipeline.UseFollowerRead();
        });
        services.AddDbContextFactory<AppDbContext>(o => o.UseCockroachDB(connectionString));
        services.AddSingleton<RegionalIdeaService>();
        await using var sp = services.BuildServiceProvider();

        await using (var db = await sp
            .GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync())
        {
            await db.Database.ExecuteSqlRawAsync(RegionalIdea.CreateTableSql);
        }

        var svc = sp.GetRequiredService<RegionalIdeaService>();
        var id = await svc.AddIdeaInRegionAsync("us-east1", "regional idea");

        // The row was pinned to the region it was written in.
        Assert.Equal("us-east1", await svc.RegionOfAsync(id));
        // end::region-test[]
    }

    [SkippableFact]
    public async Task MatchingSchema_BootsClean()
    {
        // tag::mr-check-report[]
        // Same multi-region gate as above: only meaningful against a real cluster.
        Skip.IfNot(Environment.GetEnvironmentVariable("CRDB_MULTIREGION") == "true",
            "Set CRDB_MULTIREGION=true and CRDB_URL=<multi-region cluster> to run this test.");

        var connectionString = Environment.GetEnvironmentVariable("CRDB_URL")
            ?? throw new InvalidOperationException("CRDB_URL is required when CRDB_MULTIREGION=true.");

        // Register the context with AddCockroachDBDbContextFactory<T>(): this turns the
        // startup multi-region check ON BY DEFAULT (mode from CockroachDB:MultiRegion:Mode).
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CockroachDB:MultiRegion:Mode"] = "warn",
        });
        builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        builder.Services.AddCockroachDB(builder.Configuration, pipeline => pipeline.UseRetry());
        builder.Services.AddCockroachDBDbContextFactory<AppDbContext>(o => o.UseCockroachDB(connectionString));

        using var host = builder.Build();

        await using (var db = await host.Services
            .GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync())
        {
            await db.Database.ExecuteSqlRawAsync(AppDbContext.CreateTableSql);
            await db.Database.ExecuteSqlRawAsync(RegionalIdea.CreateTableSql);
        }

        // StartAsync runs the hosted multi-region check. With a schema that matches the
        // [RegionalByRow]/[CrdbRegion] annotations, the host boots without throwing.
        await host.StartAsync();
        await host.StopAsync();
        // end::mr-check-report[]
    }
}
