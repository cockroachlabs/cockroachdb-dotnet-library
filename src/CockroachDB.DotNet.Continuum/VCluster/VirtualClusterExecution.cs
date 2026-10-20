using CockroachDB.DotNet.Abstractions.Pipeline;

namespace CockroachDB.DotNet.Continuum.VCluster;

/// <summary>
/// Fluent, programmatic Cockroach vCluster execution: mirrors the template's
/// <c>InRegion(...)</c>. Binds a vCluster name/priority for the duration of the
/// operation so <see cref="VirtualClusterMiddleware"/> tags it.
///
/// <code>
/// await executor.OnVirtualCluster("analytics", WorkloadPriority.Low)
///               .ExecuteAsync(() => repository.RunReportAsync());
/// </code>
/// </summary>
public static class VirtualClusterExecutorExtensions
{
    /// <summary>Begin a vCluster-bound execution scope.</summary>
    public static IVirtualClusterExecution OnVirtualCluster(
        this ICockroachDBExecutor executor,
        string name,
        WorkloadPriority? priority = null)
        => new VirtualClusterExecution(executor, new VirtualClusterBinding(name, priority));
}

/// <summary>A vCluster-bound execution surface.</summary>
public interface IVirtualClusterExecution
{
    /// <summary>Execute <paramref name="operation"/> tagged for the bound vCluster.</summary>
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation);

    /// <summary>Execute <paramref name="operation"/> tagged for the bound vCluster.</summary>
    Task ExecuteAsync(Func<Task> operation);
}

internal sealed class VirtualClusterExecution(
    ICockroachDBExecutor executor,
    VirtualClusterBinding binding) : IVirtualClusterExecution
{
    public Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        => executor.ExecuteAsync(operation, ctx =>
            ctx.Items[VirtualClusterMiddleware.ContextKey] = binding);

    public Task ExecuteAsync(Func<Task> operation)
        => executor.ExecuteAsync(operation, ctx =>
            ctx.Items[VirtualClusterMiddleware.ContextKey] = binding);
}
