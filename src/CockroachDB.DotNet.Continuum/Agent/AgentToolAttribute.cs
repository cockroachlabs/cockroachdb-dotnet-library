namespace CockroachDB.DotNet.Continuum.Agent;

/// <summary>
/// Exposes the annotated method as an agent-callable tool (e.g. for Cockroach Aegis
/// or any MCP client). Only <c>[AgentTool]</c> methods are exposed, and
/// <see cref="Destructive"/> tools are hidden unless the caller explicitly opts in —
/// least privilege by default.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AgentToolAttribute(string name) : Attribute
{
    /// <summary>The tool name exposed to agents (MCP tool name).</summary>
    public string Name { get; } = name;

    /// <summary>Human-readable description surfaced in the tool listing.</summary>
    public string Description { get; set; } = "";

    /// <summary>Whether this tool mutates state; hidden unless destructive tools are allowed.</summary>
    public bool Destructive { get; set; }
}
