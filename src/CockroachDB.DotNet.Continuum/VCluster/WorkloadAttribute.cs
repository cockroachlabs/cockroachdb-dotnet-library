namespace CockroachDB.DotNet.Continuum.VCluster;

/// <summary>
/// Assigns a Cockroach vCluster workload priority to the annotated method, mapped
/// to CockroachDB admission-control quality-of-service and applied as
/// <c>SET LOCAL default_transaction_quality_of_service</c> for the transaction.
///
/// <para>Most useful alongside <see cref="VirtualClusterAttribute"/>. Without a
/// vCluster binding (attribute or programmatic <see cref="VirtualClusterContext"/>)
/// there is nothing for the QoS to attach to.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class WorkloadAttribute(WorkloadPriority priority) : Attribute
{
    /// <summary>The workload priority class.</summary>
    public WorkloadPriority Priority { get; } = priority;
}
