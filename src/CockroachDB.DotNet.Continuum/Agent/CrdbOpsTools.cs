using CockroachDB.DotNet.Continuum.Plenum;
using Npgsql;

namespace CockroachDB.DotNet.Continuum.Agent;

/// <summary>
/// Built-in, safe (non-destructive) <see cref="AgentToolAttribute"/> tools exposing
/// common CockroachDB operations to an agent: connectivity and Cockroach Plenum
/// storage usage.
/// </summary>
public sealed class CrdbOpsTools(NpgsqlDataSource dataSource, IStorageInsights storageInsights)
{
    /// <summary>Check CockroachDB connectivity.</summary>
    [AgentTool("health", Description = "Check CockroachDB connectivity")]
    public async Task<string> HealthAsync()
    {
        try
        {
            await using var conn = await dataSource.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync();
            return "ok";
        }
        catch (NpgsqlException e)
        {
            return $"down: {e.Message}";
        }
    }

    /// <summary>Per-table Cockroach Plenum storage usage (rows/bytes).</summary>
    [AgentTool("storage_summary", Description = "Per-table Cockroach Plenum storage usage (rows/bytes)")]
    public async Task<string> StorageSummaryAsync()
    {
        var tables = await storageInsights.SummaryAsync();
        return tables.Count == 0
            ? "no tables"
            : string.Join("; ", tables.Select(t =>
                $"{t.QualifiedName} rows={t.Rows?.ToString() ?? "-1"} bytes={t.Bytes?.ToString() ?? "-1"}"));
    }
}
