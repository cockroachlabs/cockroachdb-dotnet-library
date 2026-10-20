namespace CockroachDB.DotNet.AI;

public sealed record VectorSearchResult<TId>(
    TId Id,
    double Distance,
    IReadOnlyDictionary<string, object?> Metadata)
{
    public VectorSearchResult(TId id, double distance)
        : this(id, distance, new Dictionary<string, object?>()) { }

    public double Similarity(DistanceMetric metric) => metric.ToSimilarity(Distance);
}
