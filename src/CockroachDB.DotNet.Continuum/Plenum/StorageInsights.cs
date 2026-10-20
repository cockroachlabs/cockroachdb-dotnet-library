using Npgsql;

namespace CockroachDB.DotNet.Continuum.Plenum;

/// <summary>
/// Read-only Cockroach Plenum storage insights.
///
/// <para>Surfaces usage-based storage signals — per-table estimated row counts and,
/// where the running version exposes it, approximate on-disk size — using only
/// customer-safe <c>SHOW</c> surfaces (never <c>crdb_internal</c>, which is
/// restricted in v26.2). Version-tolerant: signals a given version does not expose
/// are returned as <c>null</c> rather than raising, and any query failure degrades
/// to an empty result instead of throwing the whole summary.</para>
/// </summary>
public sealed class StorageInsights(NpgsqlDataSource dataSource) : IStorageInsights
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TableStorage>> SummaryAsync(
        CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);

        var sizes = await TableSizesAsync(conn, cancellationToken);
        var tables = await SafeQueryAsync(conn, "SHOW TABLES", cancellationToken);

        var result = new List<TableStorage>();
        foreach (var row in tables)
        {
            var table = Str(row, "table_name");
            if (table is null)
                continue;

            var schema = Str(row, "schema_name") ?? "public";
            var rows = AsLong(Get(row, "estimated_row_count"));
            var bytes = sizes.TryGetValue(table, out var size) ? size : (long?)null;

            result.Add(new TableStorage(schema, table, rows, bytes));
        }

        return result;
    }

    /// <summary>
    /// Best-effort approximate on-disk size (bytes) per table, aggregated from
    /// <c>SHOW RANGES ... WITH TABLES, DETAILS</c>. Fully defensive: any failure or
    /// missing column yields an empty map.
    /// </summary>
    private async Task<Dictionary<string, long>> TableSizesAsync(
        NpgsqlConnection conn,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, long>();
        var db = await CurrentDatabaseAsync(conn, cancellationToken);
        var sql = $"SHOW RANGES FROM DATABASE {QuoteIdent(db)} WITH TABLES, DETAILS";

        foreach (var row in await SafeQueryAsync(conn, sql, cancellationToken))
        {
            var table = Str(row, "table_name");
            var size = AsLong(Get(row, "range_size"));
            if (table is not null && size is { } s)
                map[table] = map.TryGetValue(table, out var existing) ? existing + s : s;
        }

        return map;
    }

    private static async Task<string> CurrentDatabaseAsync(
        NpgsqlConnection conn,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT current_database()";
            var db = await cmd.ExecuteScalarAsync(cancellationToken);
            return db as string ?? "defaultdb";
        }
        catch (NpgsqlException)
        {
            return "defaultdb";
        }
    }

    private static async Task<List<Dictionary<string, object?>>> SafeQueryAsync(
        NpgsqlConnection conn,
        string sql,
        CancellationToken cancellationToken)
    {
        var rows = new List<Dictionary<string, object?>>();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = await reader.IsDBNullAsync(i, cancellationToken)
                        ? null
                        : reader.GetValue(i);
                    row[reader.GetName(i)] = value;
                }

                rows.Add(row);
            }
        }
        catch (NpgsqlException)
        {
            // Optional signal — this version/shape differs; degrade gracefully.
            return [];
        }

        return rows;
    }

    private static object? Get(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) ? v : null;

    private static string? Str(Dictionary<string, object?> row, string key)
        => Get(row, key)?.ToString();

    private static long? AsLong(object? value)
    {
        return value switch
        {
            null => null,
            long l => l,
            int i => i,
            short s => s,
            decimal d => (long)d,
            _ => long.TryParse(value.ToString()?.Trim(), out var parsed) ? parsed : null
        };
    }

    private static string QuoteIdent(string ident) => $"\"{ident.Replace("\"", "\"\"")}\"";
}
