using CockroachDB.DotNet.Continuum.Aegis;
using CockroachDB.DotNet.Continuum.Agent;
using CockroachDB.DotNet.Continuum.Extensions;
using CockroachDB.DotNet.Continuum.Plenum;
using CockroachDB.DotNet.Continuum.VCluster;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CockroachDB.DotNet.Continuum.Tests.VCluster;

public class WorkloadPriorityTests
{
    [Theory]
    [InlineData(WorkloadPriority.High, "critical")]
    [InlineData(WorkloadPriority.Normal, "regular")]
    [InlineData(WorkloadPriority.Low, "background")]
    public void Qos_MapsToAdmissionControlValue(WorkloadPriority priority, string expected)
    {
        Assert.Equal(expected, priority.Qos());
    }

    [Fact]
    public void VirtualClusterContext_ScopedRestoresPrevious()
    {
        Assert.Null(VirtualClusterContext.Current);

        using (VirtualClusterContext.Scoped("analytics", WorkloadPriority.Low))
        {
            Assert.Equal("analytics", VirtualClusterContext.Current!.Name);
            Assert.Equal(WorkloadPriority.Low, VirtualClusterContext.Current.Priority);
        }

        Assert.Null(VirtualClusterContext.Current);
    }

    [Fact]
    public void Attributes_ExposeNameAndPriority()
    {
        var vc = new VirtualClusterAttribute("reporting");
        var wl = new WorkloadAttribute(WorkloadPriority.High);

        Assert.Equal("reporting", vc.Name);
        Assert.Equal(WorkloadPriority.High, wl.Priority);
    }
}

public class ContinuumDiTests
{
    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton(NpgsqlDataSource.Create(
            "Host=localhost;Port=26257;Database=defaultdb;Username=root;SSL Mode=Disable"));
        configure(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddAegis_DefaultsToSimulatedClient()
    {
        using var sp = BuildProvider(s => s.AddAegis());

        var client = sp.GetRequiredService<IAegisClient>();
        Assert.IsType<SimulatedAegisClient>(client);
        Assert.NotNull(sp.GetRequiredService<AegisExhaust>());
    }

    [Fact]
    public void AddAegis_WithEndpoint_UsesHttpClient()
    {
        using var sp = BuildProvider(s => s.AddAegis(o => o.Endpoint = "https://aegis.example"));

        Assert.IsType<HttpAegisClient>(sp.GetRequiredService<IAegisClient>());
    }

    [Fact]
    public void AddCockroachStorageInsights_RegistersInsights()
    {
        using var sp = BuildProvider(s => s.AddCockroachStorageInsights());

        Assert.IsType<StorageInsights>(sp.GetRequiredService<IStorageInsights>());
    }

    [Fact]
    public void AddCockroachAgentTools_RegistersAgentWithBuiltinTools()
    {
        using var sp = BuildProvider(s => s.AddCockroachAgentTools());

        var agent = sp.GetRequiredService<CrdbAgent>();
        var names = agent.ListTools().Select(t => t.Name).ToList();
        Assert.Contains("health", names);
        Assert.Contains("storage_summary", names);
    }
}
