using System.Collections.Generic;
using System.Text.Json;

namespace CockroachDB.DotNet.Continuum.Mcp;

/// <summary>
/// In-memory <see cref="IMcpClient"/> for development and tests. Advertises a small
/// canned tool set and echoes tool calls, so code that drives an MCP server runs
/// offline. Records every call for assertions via <see cref="Calls"/>.
/// </summary>
public sealed class SimulatedMcpClient : IMcpClient
{
    private readonly List<(string Name, IReadOnlyDictionary<string, object> Args)> _calls = [];

    /// <summary>Recorded (name, arguments) tool calls, in order.</summary>
    public IReadOnlyList<(string Name, IReadOnlyDictionary<string, object> Args)> Calls => _calls;

    public Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Tool>>(
        [
            new Tool("ping", "Ping", "Health check; returns pong."),
            new Tool("echo", "Echo", "Echoes its arguments as JSON text."),
        ]);

    public Task<CallResult> CallToolAsync(
        string name,
        IReadOnlyDictionary<string, object>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        var args = arguments ?? new Dictionary<string, object>();
        _calls.Add((name, args));
        var text = name == "ping" ? "pong" : JsonSerializer.Serialize(args);
        return Task.FromResult(new CallResult([new ContentBlock("text", text)], IsError: false));
    }
}
