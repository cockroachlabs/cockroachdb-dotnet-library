using CockroachDB.DotNet.Continuum.Agent;
using Xunit;

namespace CockroachDB.DotNet.Continuum.Tests.Agent;

public class ToolProviderStub
{
    public bool DropCalled { get; private set; }

    [AgentTool("echo", Description = "Echo the message back")]
    public string Echo(string message) => $"echo:{message}";

    [AgentTool("add", Description = "Add two integers")]
    public int Add(int a, int b) => a + b;

    [AgentTool("async_ping", Description = "Async ping")]
    public Task<string> AsyncPingAsync() => Task.FromResult("pong");

    [AgentTool("drop_table", Description = "Drop a table", Destructive = true)]
    public string DropTable(string name)
    {
        DropCalled = true;
        return $"dropped:{name}";
    }

    public string NotATool() => "hidden";
}

public class CrdbAgentTests
{
    [Fact]
    public void ListTools_ExcludesDestructiveByDefault()
    {
        var agent = CrdbAgent.From(new ToolProviderStub());

        var names = agent.ListTools().Select(t => t.Name).ToList();

        Assert.Contains("echo", names);
        Assert.Contains("add", names);
        Assert.Contains("async_ping", names);
        Assert.DoesNotContain("drop_table", names);
        Assert.DoesNotContain("NotATool", names);
    }

    [Fact]
    public void AllowDestructive_ExposesDestructiveTools()
    {
        var agent = CrdbAgent.From(new ToolProviderStub()).AllowDestructive(true);

        Assert.Contains(agent.ListTools(), t => t.Name == "drop_table");
    }

    [Fact]
    public void Descriptor_CarriesJsonSchemaWithParameters()
    {
        var agent = CrdbAgent.From(new ToolProviderStub());

        var add = agent.Tool("add");

        Assert.NotNull(add);
        Assert.Contains("\"a\":{\"type\":\"integer\"}", add!.InputSchemaJson);
        Assert.Contains("\"b\":{\"type\":\"integer\"}", add.InputSchemaJson);
        Assert.Contains("\"required\":[\"a\",\"b\"]", add.InputSchemaJson);
    }

    [Fact]
    public async Task Invoke_CoercesArgumentsAndReturnsResult()
    {
        var agent = CrdbAgent.From(new ToolProviderStub());

        var sum = await agent.InvokeAsync("add",
            new Dictionary<string, object> { ["a"] = "2", ["b"] = 3 });

        Assert.Equal(5, sum);
    }

    [Fact]
    public async Task Invoke_AwaitsAsyncTools()
    {
        var agent = CrdbAgent.From(new ToolProviderStub());

        var result = await agent.InvokeAsync("async_ping");

        Assert.Equal("pong", result);
    }

    [Fact]
    public async Task Invoke_UnknownTool_Throws()
    {
        var agent = CrdbAgent.From(new ToolProviderStub());

        await Assert.ThrowsAsync<ArgumentException>(() => agent.InvokeAsync("nope"));
    }

    [Fact]
    public async Task Invoke_DestructiveToolHidden_ThrowsUntilAllowed()
    {
        var provider = new ToolProviderStub();
        var agent = CrdbAgent.From(provider);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            agent.InvokeAsync("drop_table", new Dictionary<string, object> { ["name"] = "t" }));
        Assert.False(provider.DropCalled);

        agent.AllowDestructive(true);
        var result = await agent.InvokeAsync("drop_table",
            new Dictionary<string, object> { ["name"] = "t" });
        Assert.Equal("dropped:t", result);
        Assert.True(provider.DropCalled);
    }
}
