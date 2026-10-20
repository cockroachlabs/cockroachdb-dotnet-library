using System.Collections.Generic;

namespace CockroachDB.DotNet.Continuum.Cloud;

/// <summary>
/// Typed builders for the three metric-export sinks. Each returns a
/// <see cref="MetricExportConfig"/> ready to pass to
/// <see cref="ICloudClient.ConfigureMetricExportAsync"/>, so callers don't have to
/// remember the Cloud API's field names. The raw <see cref="MetricExportConfig.Settings"/>
/// dictionary remains available for sinks or fields not covered here (e.g. Azure Monitor).
/// </summary>
public static class MetricExport
{
    /// <summary>Builds a Datadog config. <paramref name="site"/> is the Datadog site (e.g. "US1").</summary>
    public static MetricExportConfig Datadog(string apiKey, string site)
        => new(MetricExportTarget.Datadog,
            new Dictionary<string, object> { ["api_key"] = apiKey, ["site"] = site });

    /// <summary>
    /// Builds an Amazon CloudWatch config. <paramref name="roleArn"/> is the
    /// CockroachCloudMetricsExportRole ARN; <paramref name="targetRegion"/> may be null/blank
    /// to use the cluster's region.
    /// </summary>
    public static MetricExportConfig CloudWatch(
        string roleArn, string logGroupName, string? targetRegion = null)
    {
        var settings = new Dictionary<string, object>
        {
            ["role_arn"] = roleArn,
            ["log_group_name"] = logGroupName,
        };
        if (!string.IsNullOrWhiteSpace(targetRegion))
        {
            settings["target_region"] = targetRegion;
        }

        return new MetricExportConfig(MetricExportTarget.CloudWatch, settings);
    }

    /// <summary>Builds a Prometheus config (enabled without extra settings).</summary>
    public static MetricExportConfig Prometheus()
        => new(MetricExportTarget.Prometheus, new Dictionary<string, object>());
}
