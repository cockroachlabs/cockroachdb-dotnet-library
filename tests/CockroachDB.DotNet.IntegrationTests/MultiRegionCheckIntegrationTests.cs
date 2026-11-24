using System;
using System.Linq;
using System.Threading.Tasks;
using CockroachDB.DotNet.Abstractions.Attributes;
using CockroachDB.DotNet.Abstractions.Configuration;
using CockroachDB.DotNet.Core.MultiRegion;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CockroachDB.DotNet.IntegrationTests;

/// <summary>
/// Exercises the multi-region check against a live CockroachDB cluster: the reads (version probe,
/// SHOW statements, information_schema, pg_get_keywords) and the end-to-end Load + Evaluate.
/// Requires a cluster on localhost:26257 (CI starts one; see ci.yml).
/// </summary>
[Trait("Category", "Integration")]
public class MultiRegionCheckIntegrationTests
{
    private const string ConnStr =
        "Host=localhost;Port=26257;Database=defaultdb;Username=root;SSL Mode=Disable";

    [GlobalTable]
    private sealed class GlobalEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class GlobalDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<GlobalEntity> Globals => Set<GlobalEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GlobalEntity>(e =>
            {
                e.ToTable("globals");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.Name).HasColumnName("name");
            });
    }

    [Fact]
    public async Task IsCockroachDB_IsTrue_AgainstRealCluster()
    {
        await using var conn = new NpgsqlConnection(ConnStr);
        await conn.OpenAsync();

        Assert.True(await MultiRegionCheck.IsCockroachDBAsync(conn, default));
    }

    [Fact]
    public async Task LoadAndEvaluate_GlobalEntityOnNonMultiRegionDb_ReportsNotMultiRegion()
    {
        var dbName = $"mr_check_{Guid.NewGuid():N}"[..18];
        await ExecAsync(ConnStr, $"CREATE DATABASE {dbName}");
        var testConnStr = ConnStr.Replace("Database=defaultdb", $"Database={dbName}");
        try
        {
            await ExecAsync(testConnStr,
                "CREATE TABLE globals (id UUID PRIMARY KEY DEFAULT gen_random_uuid(), name TEXT NOT NULL)");

            var options = new DbContextOptionsBuilder<GlobalDbContext>().UseNpgsql(testConnStr).Options;
            await using var ctx = new GlobalDbContext(options);
            var tables = MultiRegionEntities.Read(ctx.Model);

            await using var conn = new NpgsqlConnection(testConnStr);
            await conn.OpenAsync();
            var state = await MultiRegionCheck.LoadAsync(conn, tables, default);
            var report = MultiRegionCheck.Evaluate(state, tables);

            // The read side actually populated state from the live cluster.
            Assert.Equal(dbName, state.Database);
            Assert.NotEmpty(state.Keywords);
            Assert.True(state.Tables.ContainsKey(new TableName(state.CurrentSchema, "globals")));
            Assert.Null(state.PrimaryRegion);

            // A @GlobalTable entity on a non-multi-region database is reported.
            Assert.False(report.IsEmpty);
            Assert.Contains(report.Problems, p => p.Contains("is not multi-region"));
        }
        finally
        {
            await ExecAsync(ConnStr, $"DROP DATABASE {dbName} CASCADE");
        }
    }

    // Full round-trip (mirrors the Spring IT): make the DB multi-region, apply the suggested SQL,
    // re-check and confirm the report clears. Runs only on a cluster that actually has regions
    // (a node started with --locality); a plain single node has none, so this no-ops there.
    [Fact]
    public async Task SuggestedSql_MakesGlobalTableMatch_OnMultiRegionCluster()
    {
        string[] regions;
        await using (var probe = new NpgsqlConnection(ConnStr))
        {
            await probe.OpenAsync();
            var state = await MultiRegionCheck.LoadAsync(probe, [], default);
            regions = state.ClusterRegions.ToArray();
        }

        if (regions.Length == 0)
        {
            return; // Not a multi-region-capable cluster; nothing to exercise.
        }

        var dbName = $"mr_rt_{Guid.NewGuid():N}"[..18];
        await ExecAsync(ConnStr, $"CREATE DATABASE {dbName}");
        var testConnStr = ConnStr.Replace("Database=defaultdb", $"Database={dbName}");
        try
        {
            await ExecAsync(testConnStr, $"ALTER DATABASE {dbName} SET PRIMARY REGION \"{regions[0]}\"");
            await ExecAsync(testConnStr,
                "CREATE TABLE globals (id UUID PRIMARY KEY DEFAULT gen_random_uuid(), name TEXT NOT NULL)");
            await ExecAsync(testConnStr, "ALTER TABLE globals SET LOCALITY GLOBAL");

            var options = new DbContextOptionsBuilder<GlobalDbContext>().UseNpgsql(testConnStr).Options;
            await using var ctx = new GlobalDbContext(options);
            var tables = MultiRegionEntities.Read(ctx.Model);

            await using var conn = new NpgsqlConnection(testConnStr);
            await conn.OpenAsync();
            var state = await MultiRegionCheck.LoadAsync(conn, tables, default);
            var report = MultiRegionCheck.Evaluate(state, tables);

            Assert.True(report.IsEmpty, "report should be clean: " + report.Format(MultiRegionCheckMode.Warn));
        }
        finally
        {
            await ExecAsync(ConnStr, $"DROP DATABASE {dbName} CASCADE");
        }
    }

    private static async Task ExecAsync(string connStr, string sql)
    {
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
