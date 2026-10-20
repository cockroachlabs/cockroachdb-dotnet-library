using System.Collections.Generic;
using System.Linq;

namespace CockroachDB.DotNet.Continuum.Mcp;

/// <summary>A tool advertised by an MCP server.</summary>
/// <param name="Name">Unique tool name.</param>
/// <param name="Title">Human-readable title.</param>
/// <param name="Description">What the tool does.</param>
public sealed record Tool(string Name, string Title, string Description);

/// <summary>One content block of a tool-call result.</summary>
/// <param name="Type">Block type, e.g. <c>text</c>.</param>
/// <param name="Text">Text payload (for <c>text</c> blocks).</param>
public sealed record ContentBlock(string Type, string Text);

/// <summary>The result of a <c>tools/call</c>.</summary>
/// <param name="Content">Content blocks returned by the tool.</param>
/// <param name="IsError">Whether the tool reported an execution error.</param>
public sealed record CallResult(IReadOnlyList<ContentBlock> Content, bool IsError)
{
    /// <summary>Concatenated text of all text content blocks.</summary>
    public string Text() => string.Concat(Content.Where(c => c.Type == "text").Select(c => c.Text));
}

/// <summary>
/// Minimal client for the Model Context Protocol (MCP). MCP is a public, versioned
/// protocol, so unlike the not-yet-public Aegis REST API this speaks the real wire
/// format: discover tools and call them on a remote MCP server (for example the
/// Cockroach Continuum Cloud MCP server).
/// </summary>
public interface IMcpClient
{
    /// <summary>Discover the tools the server advertises.</summary>
    Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>Invoke a tool by name with the given arguments.</summary>
    Task<CallResult> CallToolAsync(
        string name,
        IReadOnlyDictionary<string, object>? arguments = null,
        CancellationToken cancellationToken = default);
}
