using System.Collections.Generic;

namespace CockroachDB.DotNet.Continuum.Cloud;

/// <summary>
/// In-memory <see cref="ICloudClient"/> for development and tests. Serves a small
/// canned set of clusters and records metric-export calls. Selected automatically
/// when no Cloud API key is configured.
/// </summary>
public sealed class SimulatedCloudClient : ICloudClient
{
    private readonly List<Cluster> _clusters =
    [
        new Cluster("00000000-0000-0000-0000-000000000001", "dev-cluster",
            "STANDARD", "CREATED", "GCP", ["us-east1"]),
    ];

    private readonly Dictionary<string, MetricExportConfig> _exports = new();

    public Task<IReadOnlyList<Cluster>> ListClustersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Cluster>>(_clusters.ToList());

    public Task<Cluster> GetClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        var cluster = _clusters.FirstOrDefault(c => c.Id == clusterId)
                      ?? throw new KeyNotFoundException($"cloud: cluster not found: {clusterId}");
        return Task.FromResult(cluster);
    }

    public Task ConfigureMetricExportAsync(string clusterId, MetricExportConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        _exports[clusterId] = config;
        return Task.CompletedTask;
    }

    /// <summary>The metric-export configs recorded so far, keyed by cluster ID.</summary>
    public IReadOnlyDictionary<string, MetricExportConfig> MetricExports => _exports;
}
