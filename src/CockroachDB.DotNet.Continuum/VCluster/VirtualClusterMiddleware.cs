using CockroachDB.DotNet.Abstractions.Pipeline;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CockroachDB.DotNet.Continuum.VCluster;

/// <summary>
/// Pipeline middleware that applies Cockroach vCluster workload tagging. Mirrors
/// <c>RegionMiddleware</c>: when a binding is in scope (via the
/// <c>OnVirtualCluster(...)</c> wrapper, <see cref="VirtualClusterContext"/>, or a
/// binding placed on the context) it opens a transaction and issues the
/// <c>SET LOCAL application_name</c> / <c>SET LOCAL default_transaction_quality_of_service</c>
/// statements so the workload is attributable, then proceeds.
///
/// <para>Register with <c>pipeline.UseVirtualCluster()</c>.</para>
/// </summary>
public sealed class VirtualClusterMiddleware(
    NpgsqlDataSource dataSource,
    ILogger<VirtualClusterMiddleware> logger) : ICockroachDBMiddleware
{
    /// <summary>The <see cref="CockroachDBContext.Items"/> key holding an explicit binding.</summary>
    public const string ContextKey = "CockroachDB.Continuum.VirtualCluster";

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(
        CockroachDBContext context,
        Func<CockroachDBContext, Task<T>> next)
    {
        var binding = ResolveBinding(context);
        if (binding is null)
            return await next(context);

        await using var conn = await dataSource.OpenConnectionAsync();
        await using (var tx = await conn.BeginTransactionAsync())
        {
            await VirtualClusterCommands.ApplyAsync(conn, tx, binding);
            await tx.CommitAsync();
        }

        context.Items[ContextKey] = binding;
        logger.LogDebug(
            "Cockroach vCluster '{Name}' tagged (qos={Qos}).",
            binding.Name,
            binding.Priority?.Qos() ?? "default");

        return await next(context);
    }

    private static VirtualClusterBinding? ResolveBinding(CockroachDBContext context)
    {
        if (context.Items.TryGetValue(ContextKey, out var value) &&
            value is VirtualClusterBinding fromContext)
            return fromContext;

        return VirtualClusterContext.Current;
    }
}
