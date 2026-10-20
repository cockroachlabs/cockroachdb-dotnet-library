using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Npgsql;

namespace CockroachDB.DotNet.Continuum.Cloud;

/// <summary>A minimal SQL executor the <see cref="VirtualClusterManager"/> needs.</summary>
public interface ISqlRunner
{
    Task ExecuteAsync(string sql, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string[]>> QueryAsync(string sql, CancellationToken cancellationToken = default);
}

/// <summary><see cref="ISqlRunner"/> backed by an Npgsql data source.</summary>
public sealed class NpgsqlSqlRunner(NpgsqlDataSource dataSource) : ISqlRunner
{
    public async Task ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        await using var cmd = dataSource.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string[]>> QueryAsync(string sql, CancellationToken cancellationToken = default)
    {
        var rows = new List<string[]>();
        await using var cmd = dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[i] = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i).ToString() ?? string.Empty;
            }

            rows.Add(row);
        }

        return rows;
    }
}

/// <summary>
/// Manages Cockroach virtual-cluster lifecycle over a SQL connection using the
/// <c>CREATE / SHOW / DROP VIRTUAL CLUSTER</c> statements (these require a
/// Continuum-enabled cluster). Names are validated before interpolation into DDL.
/// </summary>
public sealed partial class VirtualClusterManager(ISqlRunner runner)
{
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]{0,127}$")]
    private static partial Regex IdentRegex();

    private static void Validate(string name)
    {
        if (string.IsNullOrEmpty(name) || !IdentRegex().IsMatch(name))
        {
            throw new ArgumentException(
                $"cloud: invalid virtual-cluster name '{name}' " +
                "(allowed: letters, digits, underscore; must not start with a digit)", nameof(name));
        }
    }

    public Task CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        Validate(name);
        return runner.ExecuteAsync($"CREATE VIRTUAL CLUSTER {name}", cancellationToken);
    }

    public Task DropAsync(string name, CancellationToken cancellationToken = default)
    {
        Validate(name);
        return runner.ExecuteAsync($"DROP VIRTUAL CLUSTER {name}", cancellationToken);
    }

    public async Task<IReadOnlyList<VirtualCluster>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await runner.QueryAsync("SHOW VIRTUAL CLUSTERS", cancellationToken).ConfigureAwait(false);
        return rows.Select(r =>
        {
            var id = r.Length > 0 ? r[0] : string.Empty;
            var name = r.Length > 1 ? r[1] : string.Empty;
            return new VirtualCluster(name, id, name, string.Empty);
        }).ToList();
    }

    /// <summary>
    /// Appends the Npgsql <c>Options</c> keyword so connections route to the named
    /// virtual cluster via <c>-ccluster</c>. Expects an ADO.NET-style connection string.
    /// </summary>
    public static string RoutingConnString(string baseConn, string vclusterName)
    {
        Validate(vclusterName);
        var trimmed = baseConn.TrimEnd();
        var sep = trimmed.EndsWith(';') ? string.Empty : ";";
        return $"{trimmed}{sep}Options=-ccluster={vclusterName}";
    }
}
