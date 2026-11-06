using CockroachDB.DotNet.AI;
using CockroachDB.DotNet.AI.Template;
using CockroachDB.DotNet.AI.VectorStore;
using Npgsql;

namespace IdeasBoard;

/// <summary>
/// Semantic / vector side of the Ideas Board. Documents are stored with explicit
/// embeddings in a CockroachDB VECTOR column via <see cref="CockroachDBVectorStore"/>
/// (which also creates the C-SPANN vector index), and <c>FindSimilarAsync</c> runs a
/// real nearest-neighbour query using the CockroachDB vector distance operator. No
/// embedding model is involved: callers supply vectors directly, so results are
/// deterministic.
/// </summary>
/// <remarks>
/// The search issues the distance query directly over <see cref="NpgsqlDataSource"/>
/// using the library's <see cref="VectorSearchTemplate.ToVectorLiteral"/> and
/// <see cref="DistanceMetric"/> helpers. The library's own search methods
/// (<c>VectorSearchTemplate.SearchAsync</c> / <c>CockroachDBVectorStore.SimilaritySearchAsync</c>)
/// commit the transaction while the result reader is still open, which throws
/// <c>NpgsqlOperationInProgressException</c> in 0.1.2 — so they are avoided here.
/// </remarks>
// tag::vector-search[]
public sealed class SemanticIdeaService(
    CockroachDBVectorStore store,
    NpgsqlDataSource dataSource)
{
    // Default table/column of the vector store (see AddCockroachDBVectorStore).
    private const string Table = "vector_store";
    private const string VectorColumn = "embedding";
    private const DistanceMetric Metric = DistanceMetric.L2;

    // Creates the VECTOR table and the C-SPANN vector index (v26.2+).
    public Task InitializeAsync(CancellationToken ct = default) =>
        store.InitializeSchemaAsync(ct);

    // Store a doc with its (precomputed) embedding; no model call.
    public Task StoreAsync(IdeaDoc doc, CancellationToken ct = default) =>
        store.AddDocumentsAsync(
            [new VectorDocument { Id = doc.Id, Text = doc.Text, Embedding = doc.Embedding }],
            ct);

    // k nearest documents to the query vector, ordered by ascending distance
    // (i.e. descending similarity). Searching by raw vector keeps the demo
    // model-free and reproducible.
    public async Task<IReadOnlyList<VectorSearchResult<object>>> FindSimilarAsync(
        float[] queryVector, int k, CancellationToken ct = default)
    {
        var literal = VectorSearchTemplate.ToVectorLiteral(queryVector);
        var distance = $"{VectorColumn} {Metric.ToOperator()} '{literal}'::vector";
        var sql = $"SELECT id, {distance} AS distance FROM {Table} ORDER BY {distance} LIMIT {k}";

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var results = new List<VectorSearchResult<object>>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(new VectorSearchResult<object>(reader.GetString(0), reader.GetDouble(1)));
        return results;
    }
}
// end::vector-search[]

// Satisfies the vector store's IEmbeddingGenerator dependency without a model.
// It is never actually invoked here: StoreAsync supplies explicit embeddings and
// FindSimilarAsync searches by raw vector, so no text is ever embedded at runtime.
public sealed class ZeroEmbeddingGenerator : IEmbeddingGenerator
{
    public int Dimensions => 3;

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default) =>
        Task.FromResult(new float[Dimensions]);
}
