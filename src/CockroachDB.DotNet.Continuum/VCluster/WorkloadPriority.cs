namespace CockroachDB.DotNet.Continuum.VCluster;

/// <summary>
/// Cockroach vCluster workload priority classes, mapped to CockroachDB
/// admission-control quality-of-service values.
/// </summary>
public enum WorkloadPriority
{
    /// <summary>Latency-sensitive OLTP work. Maps to <c>critical</c>.</summary>
    High,

    /// <summary>Default priority. Maps to <c>regular</c>.</summary>
    Normal,

    /// <summary>Background / analytics work. Maps to <c>background</c>.</summary>
    Low
}

/// <summary>
/// Maps a <see cref="WorkloadPriority"/> to its CockroachDB
/// <c>default_transaction_quality_of_service</c> value.
/// </summary>
public static class WorkloadPriorityExtensions
{
    /// <summary>The CockroachDB QoS value for this priority.</summary>
    public static string Qos(this WorkloadPriority priority) => priority switch
    {
        WorkloadPriority.High => "critical",
        WorkloadPriority.Normal => "regular",
        WorkloadPriority.Low => "background",
        _ => "regular"
    };
}
