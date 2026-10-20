namespace CockroachDB.DotNet.Continuum.VCluster;

/// <summary>
/// An immutable Cockroach vCluster binding: a workload name and an optional
/// priority. When <see cref="Priority"/> is <c>null</c> only the
/// <c>application_name</c> tag is applied.
/// </summary>
public sealed record VirtualClusterBinding(string Name, WorkloadPriority? Priority = null);

/// <summary>
/// <see cref="AsyncLocal{T}"/>-based Cockroach vCluster context for programmatic use,
/// mirroring <c>RegionContext</c>. When set, <see cref="VirtualClusterMiddleware"/>
/// (and the <c>OnVirtualCluster(...)</c> execution wrapper) apply the tag/QoS to the
/// transaction. Flows correctly across async/await continuations.
/// </summary>
public static class VirtualClusterContext
{
    private static readonly AsyncLocal<VirtualClusterBinding?> Holder = new();

    /// <summary>The vCluster binding in scope for the current async flow, if any.</summary>
    public static VirtualClusterBinding? Current
    {
        get => Holder.Value;
        set => Holder.Value = value;
    }

    /// <summary>Bind <paramref name="name"/>/<paramref name="priority"/> for a <c>using</c> scope.</summary>
    public static VirtualClusterScope Scoped(string name, WorkloadPriority? priority = null)
        => new(new VirtualClusterBinding(name, priority));
}

/// <summary>A disposable scope that restores the previous vCluster binding on dispose.</summary>
public readonly struct VirtualClusterScope : IDisposable
{
    private readonly VirtualClusterBinding? _previous;

    internal VirtualClusterScope(VirtualClusterBinding binding)
    {
        _previous = VirtualClusterContext.Current;
        VirtualClusterContext.Current = binding;
    }

    /// <summary>Restore the previous binding.</summary>
    public void Dispose() => VirtualClusterContext.Current = _previous;
}
