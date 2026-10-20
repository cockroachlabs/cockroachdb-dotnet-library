using System.Diagnostics;
using System.Text;
using CockroachDB.DotNet.AI.Metrics;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CockroachDB.DotNet.AI.Template;

public sealed class VectorSearchTemplate
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IAIMetrics _metrics;
    private readonly ILogger<VectorSearchTemplate> _logger;

    public VectorSearchTemplate(
        NpgsqlDataSource dataSource,
        IAIMetrics metrics,
        ILogger<VectorSearchTemplate> logger)
    {
        _dataSource = dataSource;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<IReadOnlyList<VectorSearchResult<object>>> SearchAsync(
        string table,
        string vectorColumn,
        float[] queryVector,
        DistanceMetric metric,
        int limit,
        CancellationToken ct = default)
    {
        return await SearchAsync(table, vectorColumn, queryVector, metric, limit,
            columns: [], filter: "", beamSize: 0, ct: ct);
    }

    public async Task<IReadOnlyList<VectorSearchResult<object>>> SearchAsync(
        string table,
        string vectorColumn,
        float[] queryVector,
        DistanceMetric metric,
        int limit,
        string[] columns,
        string filter,
        int beamSize,
        CancellationToken ct = default)
    {
        ValidateInputs(table, vectorColumn, queryVector);

        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            if (beamSize > 0)
            {
                await using var beamCmd = conn.CreateCommand();
                beamCmd.Transaction = tx;
                beamCmd.CommandText = $"SET LOCAL vector_search_beam_size = {beamSize}";
                await beamCmd.ExecuteNonQueryAsync(ct);
            }

            var vectorLiteral = ToVectorLiteral(queryVector);
            var selectCols = columns.Length > 0 ? string.Join(", ", columns) : "*";
            var distanceExpr = $"{vectorColumn} {metric.ToOperator()} '{vectorLiteral}'::vector";

            var sql = new StringBuilder();
            sql.Append($"SELECT {selectCols}, {distanceExpr} AS distance FROM {table}");

            if (!string.IsNullOrWhiteSpace(filter))
                sql.Append($" WHERE {filter}");

            sql.Append($" ORDER BY {distanceExpr} LIMIT {limit}");

            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql.ToString();

            var results = new List<VectorSearchResult<object>>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetValue(0);
                var distance = reader.GetDouble(reader.GetOrdinal("distance"));
                results.Add(new VectorSearchResult<object>(id, distance));
            }

            await tx.CommitAsync(ct);

            _metrics.RecordVectorSearch(startTimestamp, results.Count, true);
            _logger.LogDebug("Vector search on {Table} returned {Count} results", table, results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _metrics.RecordVectorSearch(startTimestamp, 0, false);
            _logger.LogError(ex, "Vector search failed on table {Table}", table);
            throw;
        }
    }

    public async Task StoreEmbeddingAsync(
        string table,
        string idColumn,
        object idValue,
        string vectorColumn,
        float[] embedding,
        CancellationToken ct = default)
    {
        ValidateInputs(table, vectorColumn, embedding);
        var vectorLiteral = ToVectorLiteral(embedding);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO {table} ({idColumn}, {vectorColumn})
            VALUES ($1, '{vectorLiteral}'::vector)
            ON CONFLICT ({idColumn}) DO UPDATE SET {vectorColumn} = '{vectorLiteral}'::vector
            """;
        cmd.Parameters.AddWithValue(idValue);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task StoreEmbeddingWithMetadataAsync(
        string table,
        string idColumn,
        object idValue,
        string vectorColumn,
        float[] embedding,
        Dictionary<string, object?> metadata,
        CancellationToken ct = default)
    {
        ValidateInputs(table, vectorColumn, embedding);
        var vectorLiteral = ToVectorLiteral(embedding);

        var cols = new StringBuilder($"{idColumn}, {vectorColumn}");
        var vals = new StringBuilder($"$1, '{vectorLiteral}'::vector");
        var updates = new StringBuilder($"{vectorColumn} = '{vectorLiteral}'::vector");

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.Parameters.AddWithValue(idValue);

        var paramIdx = 2;
        foreach (var (key, value) in metadata)
        {
            cols.Append($", {key}");
            vals.Append($", ${paramIdx}");
            updates.Append($", {key} = ${paramIdx}");
            cmd.Parameters.AddWithValue(value ?? DBNull.Value);
            paramIdx++;
        }

        cmd.CommandText = $"""
            INSERT INTO {table} ({cols}) VALUES ({vals})
            ON CONFLICT ({idColumn}) DO UPDATE SET {updates}
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public static string ToVectorLiteral(float[] vector)
    {
        var sb = new StringBuilder("[", vector.Length * 10);
        for (var i = 0; i < vector.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(vector[i]);
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static void ValidateInputs(string table, string vectorColumn, float[] vector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentException.ThrowIfNullOrWhiteSpace(vectorColumn);
        ArgumentNullException.ThrowIfNull(vector);
        if (vector.Length == 0)
            throw new ArgumentException("Query vector must not be empty", nameof(vector));
    }
}
