using CockroachDB.DotNet.Abstractions.Pipeline;
using CockroachDB.DotNet.Continuum.VCluster;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace CockroachDB.DotNet.Continuum.Tests.Integration;

/// <summary>
/// Verifies Cockroach vCluster workload tagging against a live CockroachDB: the
/// <c>SET LOCAL application_name</c> / <c>default_transaction_quality_of_service</c>
/// statements take effect within the transaction (asserted via <c>SHOW</c>).
/// </summary>
[Trait("Category", "Integration")]
[Collection(ContinuumCollection.Name)]
public class VClusterTaggingIntegrationTests(ContinuumContainerFixture fixture)
{
    [Fact]
    public async Task ApplyAsync_SetsApplicationNameAndQos_WithinTransaction()
    {
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await VirtualClusterCommands.ApplyAsync(
            conn, tx, new VirtualClusterBinding("analytics", WorkloadPriority.Low));

        Assert.Equal("analytics", await ShowAsync(conn, tx, "application_name"));
        Assert.Equal("background", await ShowAsync(conn, tx, "default_transaction_quality_of_service"));

        await tx.RollbackAsync();
    }

    [Theory]
    [InlineData(WorkloadPriority.High, "critical")]
    [InlineData(WorkloadPriority.Normal, "regular")]
    [InlineData(WorkloadPriority.Low, "background")]
    public async Task ApplyAsync_MapsEachPriorityToQos(WorkloadPriority priority, string expectedQos)
    {
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await VirtualClusterCommands.ApplyAsync(
            conn, tx, new VirtualClusterBinding("oltp", priority));

        Assert.Equal(expectedQos,
            await ShowAsync(conn, tx, "default_transaction_quality_of_service"));

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Middleware_RunsAndTagsWithoutThrowing()
    {
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);
        var middleware = new VirtualClusterMiddleware(
            dataSource, NullLogger<VirtualClusterMiddleware>.Instance);

        using (VirtualClusterContext.Scoped("reporting", WorkloadPriority.Low))
        {
            var result = await middleware.ExecuteAsync(
                new CockroachDBContext(),
                _ => Task.FromResult(42));

            Assert.Equal(42, result);
        }
    }

    private static async Task<string?> ShowAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, string setting)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SHOW {setting}";
        return (await cmd.ExecuteScalarAsync()) as string;
    }
}
