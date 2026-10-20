using System.Collections.Generic;
using System.Threading.Tasks;
using CockroachDB.DotNet.Continuum.Cloud;
using Xunit;

namespace CockroachDB.DotNet.Continuum.Tests;

public class MetricExportTests
{
    [Fact]
    public void Datadog_BuildsSiteAndApiKey()
    {
        var cfg = MetricExport.Datadog("key-1", "US1");
        Assert.Equal(MetricExportTarget.Datadog, cfg.Target);
        Assert.Equal("key-1", cfg.Settings["api_key"]);
        Assert.Equal("US1", cfg.Settings["site"]);
    }

    [Fact]
    public void CloudWatch_BuildsFields_AndOmitsBlankRegion()
    {
        var cfg = MetricExport.CloudWatch("arn:aws:iam::1:role/R", "crdb-logs", "us-east-1");
        Assert.Equal(MetricExportTarget.CloudWatch, cfg.Target);
        Assert.Equal("arn:aws:iam::1:role/R", cfg.Settings["role_arn"]);
        Assert.Equal("crdb-logs", cfg.Settings["log_group_name"]);
        Assert.Equal("us-east-1", cfg.Settings["target_region"]);

        Assert.DoesNotContain("target_region", MetricExport.CloudWatch("arn", "lg").Settings.Keys);
    }

    [Fact]
    public void Prometheus_HasNoSettings()
    {
        var cfg = MetricExport.Prometheus();
        Assert.Equal(MetricExportTarget.Prometheus, cfg.Target);
        Assert.Empty(cfg.Settings);
    }

    [Fact]
    public async Task SimulatedClient_RecordsBuiltConfig()
    {
        var c = new SimulatedCloudClient();
        var clusters = await c.ListClustersAsync();
        await c.ConfigureMetricExportAsync(clusters[0].Id, MetricExport.Datadog("k", "US1"));
        var recorded = c.MetricExports[clusters[0].Id];
        Assert.Equal(MetricExportTarget.Datadog, recorded.Target);
    }
}
