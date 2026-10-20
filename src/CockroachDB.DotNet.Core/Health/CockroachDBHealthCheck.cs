using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace CockroachDB.DotNet.Core.Health;

public sealed class CockroachDBHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("CockroachDB is reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("CockroachDB is unreachable", ex);
        }
    }
}
