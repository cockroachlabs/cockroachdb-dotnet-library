using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CockroachDB.DotNet.Continuum.Cloud;
using NSubstitute;
using Xunit;

namespace CockroachDB.DotNet.Continuum.Tests;

public class CloudTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        public readonly List<HttpRequestMessage> Requests = [];
        public required Func<HttpRequestMessage, HttpResponseMessage> Responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Responder(request));
        }
    }

    private static HttpCloudClient HttpClientWith(FakeHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://cloud/") };
        http.DefaultRequestHeaders.Add("Authorization", "Bearer sk-123");
        return new HttpCloudClient(http);
    }

    [Fact]
    public async Task Simulated_ServesClustersAndRecordsMetricExport()
    {
        var c = new SimulatedCloudClient();
        var clusters = await c.ListClustersAsync();
        Assert.Single(clusters);
        Assert.Equal("dev-cluster", (await c.GetClusterAsync(clusters[0].Id)).Name);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => c.GetClusterAsync("missing"));

        await c.ConfigureMetricExportAsync(clusters[0].Id,
            new MetricExportConfig(MetricExportTarget.Datadog, new Dictionary<string, object> { ["site"] = "us5" }));
        Assert.Equal(MetricExportTarget.Datadog, c.MetricExports[clusters[0].Id].Target);
    }

    [Fact]
    public async Task Http_ListClustersParsesSnakeCaseAndSendsBearer()
    {
        var handler = new FakeHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"clusters\":[{\"id\":\"c1\",\"name\":\"prod\",\"plan\":\"STANDARD\"," +
                    "\"state\":\"CREATED\",\"cloud_provider\":\"AWS\",\"regions\":[\"us-east-1\"]}]}",
                    Encoding.UTF8, "application/json"),
            },
        };
        var c = HttpClientWith(handler);
        var clusters = await c.ListClustersAsync();
        Assert.Single(clusters);
        Assert.Equal("AWS", clusters[0].CloudProvider);
        Assert.Equal(new[] { "us-east-1" }, clusters[0].Regions);
        Assert.Equal("Bearer sk-123", handler.Requests[0].Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task Http_MetricExportPostsToTargetPath()
    {
        var handler = new FakeHandler { Responder = _ => new HttpResponseMessage(HttpStatusCode.OK) };
        var c = HttpClientWith(handler);
        await c.ConfigureMetricExportAsync("c1",
            new MetricExportConfig(MetricExportTarget.Datadog, new Dictionary<string, object> { ["site"] = "us5" }));
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.EndsWith("/clusters/c1/metricexport/datadog", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task VClusterManager_CreateDropList()
    {
        var runner = Substitute.For<ISqlRunner>();
        runner.QueryAsync("SHOW VIRTUAL CLUSTERS", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string[]>>(new[] { new[] { "3", "analytics" } }));
        var m = new VirtualClusterManager(runner);

        await m.CreateAsync("analytics");
        await m.DropAsync("reporting");
        await runner.Received().ExecuteAsync("CREATE VIRTUAL CLUSTER analytics", Arg.Any<CancellationToken>());
        await runner.Received().ExecuteAsync("DROP VIRTUAL CLUSTER reporting", Arg.Any<CancellationToken>());

        var vcs = await m.ListAsync();
        Assert.Single(vcs);
        Assert.Equal("analytics", vcs[0].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1abc")]
    [InlineData("drop table users;")]
    [InlineData("has space")]
    public async Task VClusterManager_RejectsBadNames(string bad)
    {
        var m = new VirtualClusterManager(Substitute.For<ISqlRunner>());
        await Assert.ThrowsAsync<ArgumentException>(() => m.CreateAsync(bad));
    }

    [Fact]
    public void RoutingConnString_AddsCClusterOption()
    {
        var outConn = VirtualClusterManager.RoutingConnString(
            "Host=host;Port=26257;Database=defaultdb;SSL Mode=VerifyFull", "analytics");
        Assert.Contains("Options=-ccluster=analytics", outConn);
        Assert.Contains("Database=defaultdb", outConn);
        Assert.Throws<ArgumentException>(() => VirtualClusterManager.RoutingConnString("Host=h", "bad name"));
    }
}
