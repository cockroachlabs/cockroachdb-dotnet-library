namespace CockroachDB.DotNet.Continuum.Agent;

/// <summary>
/// A described, agent-callable tool.
/// </summary>
/// <param name="Name">The tool name (MCP tool name).</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="InputSchemaJson">JSON Schema (as a string) for the tool's arguments.</param>
public sealed record ToolDescriptor(string Name, string Description, string InputSchemaJson);
