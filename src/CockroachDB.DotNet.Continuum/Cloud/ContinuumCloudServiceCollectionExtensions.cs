using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace CockroachDB.DotNet.Continuum.Cloud;

/// <summary>
/// Health check for the Cockroach Continuum Cloud control-plane connection.
/// Healthy when clusters can be listed; the result details report whether the
/// client is simulated or live and the cluster count.
/// </summary>
public sealed class ContinuumCloudHealthCheck(ICloudClient cloud) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var mode = cloud is SimulatedCloudClient ? "simulated" : "live";
        try
        {
            var count = (await cloud.ListClustersAsync(cancellationToken).ConfigureAwait(false)).Count;
            return HealthCheckResult.Healthy($"continuum ({mode})",
                new Dictionary<string, object> { ["mode"] = mode, ["clusters"] = count });
        }
        catch (Exception e)
        {
            return HealthCheckResult.Unhealthy($"continuum ({mode})", e,
                new Dictionary<string, object> { ["mode"] = mode });
        }
    }
}

/// <summary>DI registration for the Continuum Cloud control-plane surface.</summary>
public static class ContinuumCloudServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="ICloudClient"/> (simulated until an API key is set),
    /// an <see cref="ISqlRunner"/>/<see cref="VirtualClusterManager"/> pair (which
    /// require an <see cref="NpgsqlDataSource"/> when resolved), and a Continuum
    /// health check.
    /// </summary>
    public static IServiceCollection AddCockroachDBContinuumCloud(
        this IServiceCollection services, Action<CloudOptions>? configure = null)
    {
        var options = new CloudOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddSingleton<ICloudClient>(_ =>
            string.IsNullOrWhiteSpace(options.ApiKey)
                ? new SimulatedCloudClient()
                : HttpCloudClient.Create(options.BaseUrl, options.ApiKey));

        services.AddSingleton<ISqlRunner>(sp => new NpgsqlSqlRunner(sp.GetRequiredService<NpgsqlDataSource>()));
        services.AddSingleton<VirtualClusterManager>();

        services.AddHealthChecks().AddCheck<ContinuumCloudHealthCheck>("continuum");
        return services;
    }
}
