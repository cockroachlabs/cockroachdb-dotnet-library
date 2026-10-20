namespace CockroachDB.DotNet.AI.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SemanticSearchAttribute : Attribute
{
    public required string Table { get; init; }

    public string VectorColumn { get; init; } = "embedding";

    public DistanceMetric Metric { get; init; } = DistanceMetric.Cosine;

    public int Limit { get; init; } = 10;

    public string QueryParam { get; init; } = "queryVector";

    public string Filter { get; init; } = "";

    public string[] Columns { get; init; } = [];

    public bool ReadOnly { get; init; } = true;

    public int BeamSize { get; init; } = 0;

    public double MaxDistance { get; init; } = 0;
}
