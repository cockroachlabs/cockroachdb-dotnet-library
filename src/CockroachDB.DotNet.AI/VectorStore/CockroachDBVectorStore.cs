using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CockroachDB.DotNet.AI.Metrics;
using CockroachDB.DotNet.AI.Template;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CockroachDB.DotNet.AI.VectorStore;

public sealed class VectorDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = "";
    public Dictionary<string, object?> Metadata { get; set; } = new();
    public float[]? Embedding { get; set; }
    public double Score { get; set; }
}

public interface IEmbeddingGenerator
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
    int Dimensions { get; }
}

public sealed class CockroachDBVectorStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly IAIMetrics _metrics;
    private readonly ILogger<CockroachDBVectorStore> _logger;

    private readonly string _tableName;
    private readonly string _contentColumn;
    private readonly string _embeddingColumn;
    private readonly string _metadataColumn;
    private readonly DistanceMetric _distanceMetric;
    private readonly int _beamSize;
    private readonly bool _initializeSchema;
    private readonly int _dimensions;
    private readonly string[] _vectorIndexPrefixColumns;
    private readonly bool _createVectorIndex;

    private CockroachDBVectorStore(Builder builder)
    {
        _dataSource = builder.DataSource;
        _embeddingGenerator = builder.EmbeddingGen;
        _metrics = builder.Metrics;
        _logger = builder.Logger;
        _tableName = builder.TableName;
        _contentColumn = builder.ContentCol;
        _embeddingColumn = builder.EmbeddingCol;
        _metadataColumn = builder.MetadataCol;
        _distanceMetric = builder.DistMetric;
        _beamSize = builder.BeamSz;
        _initializeSchema = builder.InitSchema;
        _dimensions = builder.Dims;
        _vectorIndexPrefixColumns = builder.VectorIndexPrefixCols;
        _createVectorIndex = builder.CreateVectorIdx;
    }

    public async Task InitializeSchemaAsync(CancellationToken ct = default)
    {
        if (!_initializeSchema) return;

        var dims = _dimensions > 0 ? _dimensions : _embeddingGenerator.Dimensions;
        _logger.LogInformation("Initializing CockroachDB vector store table '{Table}' with {Dims} dimensions",
            _tableName, dims);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        // Enable vector index feature (required for CockroachDB v26.2+)
        await using (var enableCmd = conn.CreateCommand())
        {
            enableCmd.CommandText = "SET CLUSTER SETTING feature.vector_index.enabled = true";
            await enableCmd.ExecuteNonQueryAsync(ct);
        }

        // Create table
        await using (var tableCmd = conn.CreateCommand())
        {
            tableCmd.CommandText = $$"""
                CREATE TABLE IF NOT EXISTS {{_tableName}} (
                    id TEXT PRIMARY KEY,
                    {{_contentColumn}} TEXT,
                    {{_metadataColumn}} JSONB DEFAULT '{}'::jsonb,
                    {{_embeddingColumn}} VECTOR({{dims}})
                )
                """;
            await tableCmd.ExecuteNonQueryAsync(ct);
        }

        // Create vector index using v26.2 CREATE VECTOR INDEX syntax
        if (_createVectorIndex)
        {
            await CreateVectorIndexAsync(conn, ct);
        }
    }

    /// <summary>
    /// Creates a vector index using the CockroachDB v26.2+ CREATE VECTOR INDEX syntax.
    /// Supports optional prefix columns for multi-tenant or partitioned workloads.
    /// </summary>
    public async Task CreateVectorIndexAsync(
        NpgsqlConnection? conn = null,
        CancellationToken ct = default)
    {
        var ownsConnection = conn is null;
        conn ??= await _dataSource.OpenConnectionAsync(ct);
        try
        {
            var opclass = _distanceMetric.ToOpclass();

            // Build column list: prefix columns + embedding column with opclass
            var columnParts = new List<string>();
            foreach (var prefixCol in _vectorIndexPrefixColumns)
                columnParts.Add(prefixCol);
            columnParts.Add($"{_embeddingColumn} {opclass}");

            var columns = string.Join(", ", columnParts);
            var indexName = $"idx_{_tableName}_{_embeddingColumn}_vec";

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE VECTOR INDEX IF NOT EXISTS {indexName} ON {_tableName} ({columns})";
            await cmd.ExecuteNonQueryAsync(ct);

            _logger.LogInformation(
                "Created vector index '{Index}' on '{Table}' with opclass {Opclass} and {PrefixCount} prefix column(s)",
                indexName, _tableName, opclass, _vectorIndexPrefixColumns.Length);
        }
        finally
        {
            if (ownsConnection) await conn.DisposeAsync();
        }
    }

    public async Task AddDocumentsAsync(IEnumerable<VectorDocument> documents, CancellationToken ct = default)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var count = 0;
        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            foreach (var doc in documents)
            {
                var embedding = doc.Embedding ?? await _embeddingGenerator.GenerateEmbeddingAsync(doc.Text, ct);
                var vectorLiteral = VectorSearchTemplate.ToVectorLiteral(embedding);
                var metadataJson = JsonSerializer.Serialize(doc.Metadata);

                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = $"""
                    INSERT INTO {_tableName} (id, {_contentColumn}, {_metadataColumn}, {_embeddingColumn})
                    VALUES ($1, $2, $3::jsonb, '{vectorLiteral}'::vector)
                    ON CONFLICT (id) DO UPDATE SET
                        {_contentColumn} = EXCLUDED.{_contentColumn},
                        {_metadataColumn} = EXCLUDED.{_metadataColumn},
                        {_embeddingColumn} = EXCLUDED.{_embeddingColumn}
                    """;
                cmd.Parameters.AddWithValue(doc.Id);
                cmd.Parameters.AddWithValue(doc.Text);
                cmd.Parameters.AddWithValue(metadataJson);
                await cmd.ExecuteNonQueryAsync(ct);
                count++;
            }

            await tx.CommitAsync(ct);
            _metrics.RecordVectorSearch(startTimestamp, count, true);
            _logger.LogDebug("Added {Count} documents to '{Table}'", count, _tableName);
        }
        catch (Exception ex)
        {
            _metrics.RecordVectorSearch(startTimestamp, 0, false);
            _logger.LogError(ex, "Failed to add documents to '{Table}'", _tableName);
            throw;
        }
    }

    public async Task DeleteDocumentsAsync(IEnumerable<string> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {_tableName} WHERE id = ANY($1)";
        cmd.Parameters.AddWithValue(idList.ToArray());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<VectorDocument>> SimilaritySearchAsync(
        string query,
        int topK = 10,
        double similarityThreshold = 0,
        string? filterExpression = null,
        CancellationToken ct = default)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var queryVector = await _embeddingGenerator.GenerateEmbeddingAsync(query, ct);
            var vectorLiteral = VectorSearchTemplate.ToVectorLiteral(queryVector);

            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            if (_beamSize > 0)
            {
                await using var beamCmd = conn.CreateCommand();
                beamCmd.Transaction = tx;
                beamCmd.CommandText = $"SET LOCAL vector_search_beam_size = {_beamSize}";
                await beamCmd.ExecuteNonQueryAsync(ct);
            }

            var distanceExpr = $"{_embeddingColumn} {_distanceMetric.ToOperator()} '{vectorLiteral}'::vector";

            var sql = new StringBuilder();
            sql.Append($"SELECT id, {_contentColumn}, {_metadataColumn}, {distanceExpr} AS distance FROM {_tableName}");

            if (!string.IsNullOrWhiteSpace(filterExpression))
                sql.Append($" WHERE {filterExpression}");

            sql.Append($" ORDER BY {distanceExpr} LIMIT {topK}");

            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql.ToString();

            var results = new List<VectorDocument>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var distance = reader.GetDouble(reader.GetOrdinal("distance"));
                var similarity = _distanceMetric.ToSimilarity(distance);

                if (similarityThreshold > 0 && similarity < similarityThreshold)
                    continue;

                var metadataJson = reader.IsDBNull(reader.GetOrdinal(_metadataColumn))
                    ? "{}"
                    : reader.GetString(reader.GetOrdinal(_metadataColumn));

                var metadata = JsonSerializer.Deserialize<Dictionary<string, object?>>(metadataJson) ?? new();
                metadata["distance"] = distance;
                metadata["similarity"] = similarity;

                results.Add(new VectorDocument
                {
                    Id = reader.GetString(reader.GetOrdinal("id")),
                    Text = reader.GetString(reader.GetOrdinal(_contentColumn)),
                    Metadata = metadata,
                    Score = similarity
                });
            }

            await tx.CommitAsync(ct);
            _metrics.RecordVectorSearch(startTimestamp, results.Count, true);
            return results;
        }
        catch (Exception ex)
        {
            _metrics.RecordVectorSearch(startTimestamp, 0, false);
            _logger.LogError(ex, "Similarity search failed on '{Table}'", _tableName);
            throw;
        }
    }

    public static Builder CreateBuilder(NpgsqlDataSource dataSource, IEmbeddingGenerator embeddingGenerator) =>
        new(dataSource, embeddingGenerator);

    public sealed class Builder
    {
        internal NpgsqlDataSource DataSource { get; }
        internal IEmbeddingGenerator EmbeddingGen { get; }
        internal IAIMetrics Metrics { get; set; } = NoOpAIMetrics.Instance;
        internal ILogger<CockroachDBVectorStore> Logger { get; set; } = Microsoft.Extensions.Logging.Abstractions.NullLogger<CockroachDBVectorStore>.Instance;
        internal string TableName { get; set; } = "vector_store";
        internal string ContentCol { get; set; } = "content";
        internal string EmbeddingCol { get; set; } = "embedding";
        internal string MetadataCol { get; set; } = "metadata";
        internal DistanceMetric DistMetric { get; set; } = DistanceMetric.Cosine;
        internal int BeamSz { get; set; }
        internal bool InitSchema { get; set; }
        internal int Dims { get; set; }
        internal string[] VectorIndexPrefixCols { get; set; } = [];
        internal bool CreateVectorIdx { get; set; }

        internal Builder(NpgsqlDataSource dataSource, IEmbeddingGenerator embeddingGenerator)
        {
            DataSource = dataSource;
            EmbeddingGen = embeddingGenerator;
        }

        public Builder WithMetrics(IAIMetrics metrics) { Metrics = metrics; return this; }
        public Builder WithLogger(ILogger<CockroachDBVectorStore> logger) { Logger = logger; return this; }
        public Builder WithTableName(string table) { TableName = table; return this; }
        public Builder WithContentColumn(string col) { ContentCol = col; return this; }
        public Builder WithEmbeddingColumn(string col) { EmbeddingCol = col; return this; }
        public Builder WithMetadataColumn(string col) { MetadataCol = col; return this; }
        public Builder WithDistanceMetric(DistanceMetric metric) { DistMetric = metric; return this; }
        public Builder WithBeamSize(int beamSize) { BeamSz = beamSize; return this; }
        public Builder WithInitializeSchema(bool init) { InitSchema = init; return this; }
        public Builder WithDimensions(int dimensions) { Dims = dimensions; return this; }
        /// <summary>
        /// Prefix columns for the vector index (v26.2+). Useful for multi-tenant workloads.
        /// Example: WithVectorIndexPrefixColumns("tenant_id") creates:
        /// CREATE VECTOR INDEX ON table (tenant_id, embedding vector_cosine_ops)
        /// </summary>
        public Builder WithVectorIndexPrefixColumns(params string[] columns) { VectorIndexPrefixCols = columns; return this; }
        /// <summary>
        /// Whether to create a vector index during schema initialization.
        /// Uses CREATE VECTOR INDEX syntax (CockroachDB v26.2+).
        /// </summary>
        public Builder WithCreateVectorIndex(bool create = true) { CreateVectorIdx = create; return this; }

        public CockroachDBVectorStore Build() => new(this);
    }
}
