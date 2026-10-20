using CockroachDB.DotNet.Abstractions.Pipeline;
using CockroachDB.DotNet.Continuum.Aegis;
using CockroachDB.DotNet.Continuum.Agent;
using CockroachDB.DotNet.Continuum.Plenum;
using CockroachDB.DotNet.Continuum.VCluster;
using CockroachDB.DotNet.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CockroachDB.DotNet.Continuum.Extensions;

/// <summary>
/// DI registration for the Cockroach Continuum surfaces: Cockroach Plenum storage
/// insights, Cockroach Aegis, and the agent/MCP tool surface. All are additive and
/// opt-in.
/// </summary>
public static class ContinuumServiceCollectionExtensions
{
    /// <summary>
    /// Register the read-only Cockroach Plenum <see cref="IStorageInsights"/>.
    /// Requires an <c>NpgsqlDataSource</c> in the container.
    /// </summary>
    public static IServiceCollection AddCockroachStorageInsights(this IServiceCollection services)
    {
        services.TryAddSingleton<IStorageInsights, StorageInsights>();
        return services;
    }

    /// <summary>
    /// Register a Cockroach Aegis client (simulated unless an endpoint is configured)
    /// and its <see cref="AegisExhaust"/> emitter. No-op when
    /// <see cref="AegisOptions.Enabled"/> is <c>false</c>.
    /// </summary>
    public static IServiceCollection AddAegis(
        this IServiceCollection services,
        Action<AegisOptions>? configure = null)
    {
        if (configure is not null)
            services.Configure(configure);

        return RegisterAegis(services);
    }

    /// <summary>
    /// Register a Cockroach Aegis client bound from configuration
    /// (<see cref="AegisOptions.SectionName"/>), simulated unless an endpoint is set.
    /// </summary>
    public static IServiceCollection AddAegis(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AegisOptions>? configure = null)
    {
        services.Configure<AegisOptions>(configuration.GetSection(AegisOptions.SectionName));
        if (configure is not null)
            services.PostConfigure(configure);

        return RegisterAegis(services);
    }

    private static IServiceCollection RegisterAegis(IServiceCollection services)
    {
        services.AddOptions<AegisOptions>();
        services.TryAddSingleton<IAegisClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AegisOptions>>().Value;
            return string.IsNullOrWhiteSpace(options.Endpoint)
                ? new SimulatedAegisClient()
                : new HttpAegisClient(options.Endpoint, options.Token);
        });
        services.TryAddSingleton<AegisExhaust>();
        return services;
    }

    /// <summary>
    /// Register the built-in <see cref="CrdbOpsTools"/> and a <see cref="CrdbAgent"/>
    /// facade. Pulls in <see cref="AddCockroachStorageInsights"/> so the
    /// <c>storage_summary</c> tool works out of the box.
    /// </summary>
    public static IServiceCollection AddCockroachAgentTools(
        this IServiceCollection services,
        bool allowDestructive = false)
    {
        services.AddCockroachStorageInsights();
        services.TryAddSingleton<CrdbOpsTools>();
        services.TryAddSingleton(sp =>
            CrdbAgent.From(sp.GetRequiredService<CrdbOpsTools>()).AllowDestructive(allowDestructive));
        return services;
    }
}

/// <summary>Pipeline-builder extensions for Cockroach Continuum middleware.</summary>
public static class ContinuumPipelineBuilderExtensions
{
    /// <summary>Add the Cockroach vCluster workload-tagging middleware to the pipeline.</summary>
    public static CockroachDBPipelineBuilder UseVirtualCluster(this CockroachDBPipelineBuilder builder)
        => builder.Use<VirtualClusterMiddleware>();
}
