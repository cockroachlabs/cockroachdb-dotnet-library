namespace CockroachDB.DotNet.Continuum.VCluster;

/// <summary>
/// Tags the annotated method's work for a Cockroach vCluster so it is isolated on
/// shared capacity. The vCluster name is applied as <c>SET LOCAL application_name</c>
/// for the transaction, so the workload is attributable in the DB Console and logs.
///
/// <para>Combine with <see cref="WorkloadAttribute"/> to also assign a priority/QoS class.</para>
///
/// <para><b>Scope (v1):</b> workload <em>tagging</em>. Routing to a distinct vCluster
/// endpoint is platform-dependent and is not performed by the library. Because .NET
/// has no runtime method interception here, the attribute is a declarative marker;
/// the runtime application path is <see cref="VirtualClusterContext"/> plus
/// <see cref="VirtualClusterMiddleware"/> (or <c>OnVirtualCluster(...)</c>).</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class VirtualClusterAttribute(string name) : Attribute
{
    /// <summary>The Cockroach vCluster / workload name to tag this work with.</summary>
    public string Name { get; } = name;
}
