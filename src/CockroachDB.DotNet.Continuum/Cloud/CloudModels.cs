using System.Collections.Generic;

namespace CockroachDB.DotNet.Continuum.Cloud;

/// <summary>A CockroachDB Cloud cluster (control-plane view).</summary>
public sealed record Cluster(
    string Id,
    string Name,
    string Plan = "",
    string State = "",
    string CloudProvider = "",
    IReadOnlyList<string>? Regions = null);

/// <summary>
/// A Cockroach virtual cluster. <see cref="RoutingId"/> is used when connecting
/// (it differs from the control-plane cluster ID); see
/// <see cref="VirtualClusterManager.RoutingConnString"/>.
/// </summary>
public sealed record VirtualCluster(string Name, string Id = "", string RoutingId = "", string State = "");

/// <summary>A Cloud metric-export sink.</summary>
public enum MetricExportTarget
{
    Datadog,
    CloudWatch,
    Prometheus,
}

/// <summary>
/// Metric-export configuration. <see cref="Settings"/> carries the target-specific
/// fields (datadog: <c>site</c>, <c>api_key</c>; cloudwatch: <c>role_arn</c>, etc.).
/// </summary>
public sealed record MetricExportConfig(MetricExportTarget Target, IReadOnlyDictionary<string, object> Settings);

/// <summary>Options for the Continuum Cloud control-plane client.</summary>
public sealed class CloudOptions
{
    /// <summary>Whether the Cloud integration is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Cloud API base URL. Blank selects the default.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Service-account secret key (Bearer). Blank selects the simulated client.</summary>
    public string ApiKey { get; set; } = "";
}

/// <summary>The Cloud control-plane surface used by Continuum integrations.</summary>
public interface ICloudClient
{
    Task<IReadOnlyList<Cluster>> ListClustersAsync(CancellationToken cancellationToken = default);

    Task<Cluster> GetClusterAsync(string clusterId, CancellationToken cancellationToken = default);

    Task ConfigureMetricExportAsync(string clusterId, MetricExportConfig config,
        CancellationToken cancellationToken = default);
}

internal static class MetricExportTargetExtensions
{
    public static string Path(this MetricExportTarget target) => target switch
    {
        MetricExportTarget.Datadog => "datadog",
        MetricExportTarget.CloudWatch => "cloudwatch",
        MetricExportTarget.Prometheus => "prometheus",
        _ => target.ToString().ToLowerInvariant(),
    };
}
