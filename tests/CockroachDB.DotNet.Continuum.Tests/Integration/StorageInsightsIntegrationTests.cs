using CockroachDB.DotNet.Continuum.Plenum;
using Npgsql;
using Xunit;

namespace CockroachDB.DotNet.Continuum.Tests.Integration;

/// <summary>
/// Verifies Cockroach Plenum <see cref="StorageInsights"/> against a live
/// CockroachDB: a created table appears in the summary, optional row/byte signals
/// are non-negative when present, and the summary never throws.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ContinuumCollection.Name)]
public class StorageInsightsIntegrationTests(ContinuumContainerFixture fixture)
{
    [Fact]
    public async Task Summary_IncludesCreatedTable_WithNonNegativeSignals()
    {
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);

        var table = $"plenum_probe_{Guid.NewGuid():N}"[..24];
        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            await using var create = conn.CreateCommand();
            create.CommandText =
                $"CREATE TABLE {table} (id INT PRIMARY KEY, note STRING)";
            await create.ExecuteNonQueryAsync();

            await using var insert = conn.CreateCommand();
            insert.CommandText =
                $"INSERT INTO {table} (id, note) VALUES (1,'a'),(2,'b'),(3,'c')";
            await insert.ExecuteNonQueryAsync();
        }

        var insights = new StorageInsights(dataSource);
        var summary = await insights.SummaryAsync();

        var entry = Assert.Single(summary, t => t.Table == table);
        Assert.Equal($"public.{table}", entry.QualifiedName);
        if (entry.Rows is { } rows)
            Assert.True(rows >= 0, "row estimate should be non-negative");
        if (entry.Bytes is { } bytes)
            Assert.True(bytes >= 0, "byte estimate should be non-negative");
    }

    [Fact]
    public async Task Summary_OnEmptyDatabase_DoesNotThrow()
    {
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);
        var insights = new StorageInsights(dataSource);

        var summary = await insights.SummaryAsync();

        Assert.NotNull(summary);
    }
}
