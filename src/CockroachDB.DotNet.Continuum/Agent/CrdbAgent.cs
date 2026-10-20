namespace CockroachDB.DotNet.Continuum.Agent;

/// <summary>
/// Agent / MCP facade over <c>[AgentTool]</c>-annotated providers.
///
/// <code>
/// var agent = CrdbAgent.From(new CrdbOpsTools(dataSource, storageInsights));
/// var tools = agent.ListTools();                        // MCP tools/list
/// var result = await agent.InvokeAsync("health", null); // MCP tools/call
/// </code>
///
/// <para>Least-privilege: destructive tools are excluded unless
/// <see cref="AllowDestructive"/> is enabled. A full MCP stdio/HTTP server is out of
/// scope for this increment; adapt <see cref="ListTools"/>/<see cref="InvokeAsync"/>
/// onto your transport of choice.</para>
/// </summary>
public sealed class CrdbAgent
{
    private readonly IReadOnlyList<object> _providers;
    private bool _allowDestructive;
    private ToolRegistry? _registry;

    private CrdbAgent(IReadOnlyList<object> providers) => _providers = providers;

    /// <summary>Create an agent over the given tool providers.</summary>
    public static CrdbAgent From(params object[] providers) => new(providers);

    /// <summary>Allow destructive tools to be registered. Call before listing/invoking.</summary>
    public CrdbAgent AllowDestructive(bool allow)
    {
        _allowDestructive = allow;
        _registry = null; // force rebuild with the new policy
        return this;
    }

    /// <summary>MCP <c>tools/list</c>.</summary>
    public IReadOnlyList<ToolDescriptor> ListTools() => Registry().Descriptors();

    /// <summary>Find a described tool by name, or <c>null</c>.</summary>
    public ToolDescriptor? Tool(string name)
        => Registry().Descriptors().FirstOrDefault(d => d.Name == name);

    /// <summary>MCP <c>tools/call</c>.</summary>
    public Task<object?> InvokeAsync(string name, IReadOnlyDictionary<string, object>? args = null)
        => Registry().InvokeAsync(name, args);

    private ToolRegistry Registry()
    {
        if (_registry is not null)
            return _registry;

        var registry = new ToolRegistry();
        foreach (var provider in _providers)
            registry.Register(provider, _allowDestructive);

        _registry = registry;
        return registry;
    }
}
