namespace CockroachDB.DotNet.AI;

public enum DistanceMetric
{
    L2,
    Cosine,
    InnerProduct
}

public static class DistanceMetricExtensions
{
    public static string ToOperator(this DistanceMetric metric) => metric switch
    {
        DistanceMetric.L2 => "<->",
        DistanceMetric.Cosine => "<=>",
        DistanceMetric.InnerProduct => "<#>",
        _ => throw new ArgumentOutOfRangeException(nameof(metric))
    };

    /// <summary>
    /// Returns the CockroachDB v26.2+ opclass name for CREATE VECTOR INDEX.
    /// </summary>
    public static string ToOpclass(this DistanceMetric metric) => metric switch
    {
        DistanceMetric.L2 => "vector_l2_ops",
        DistanceMetric.Cosine => "vector_cosine_ops",
        DistanceMetric.InnerProduct => "vector_ip_ops",
        _ => throw new ArgumentOutOfRangeException(nameof(metric))
    };

    public static double ToSimilarity(this DistanceMetric metric, double distance) => metric switch
    {
        DistanceMetric.Cosine => 1.0 - distance,
        DistanceMetric.InnerProduct => -distance,
        DistanceMetric.L2 => 1.0 / (1.0 + distance),
        _ => throw new ArgumentOutOfRangeException(nameof(metric))
    };
}
