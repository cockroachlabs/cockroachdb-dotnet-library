using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CockroachDB.DotNet.Continuum.Mcp;

namespace CockroachDB.DotNet.Continuum.Aegis;

/// <summary>
/// Maps Aegis operations to the tool names an MCP server advertises.
/// The Cockroach Continuum MCP tool contract is not pinned here: names differ by
/// server and may change. Discover them with <see cref="IMcpClient.ListToolsAsync"/>
/// and set the ones you need; an unset name throws rather than guessing.
/// </summary>
/// <param name="Findings">Tool returning a JSON array of findings.</param>
/// <param name="Recommendations">Tool returning a JSON array of recommendations.</param>
/// <param name="Emit">Tool accepting an app event.</param>
public sealed record McpToolNames(string Findings = "", string Recommendations = "", string Emit = "");

/// <summary>
/// Adapts an <see cref="IMcpClient"/> to <see cref="IAegisClient"/>. Aegis is
/// reachable read-only over the Cockroach Continuum Cloud MCP server; this adapter
/// calls the configured tools and parses their text content as JSON into
/// <see cref="Finding"/>/<see cref="Recommendation"/>.
/// </summary>
public sealed class McpAegisClient : IAegisClient
{
    private readonly IMcpClient _mcp;
    private readonly McpToolNames _tools;

    public McpAegisClient(IMcpClient mcp, McpToolNames tools)
    {
        _mcp = mcp;
        _tools = tools;
    }

    public async Task<IReadOnlyList<Finding>> GetFindingsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await ReadListAsync(_tools.Findings, "findings", cancellationToken).ConfigureAwait(false);
        return rows
            .Select(e => new Finding(Str(e, "id"), Str(e, "severity"), Str(e, "title"), Str(e, "detail")))
            .ToList();
    }

    public async Task<IReadOnlyList<Recommendation>> GetRecommendationsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await ReadListAsync(_tools.Recommendations, "recommendations", cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(e => new Recommendation(Str(e, "id"), Str(e, "title"), Str(e, "detail"))).ToList();
    }

    public async Task EmitAsync(AppEvent appEvent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_tools.Emit))
        {
            throw new InvalidOperationException("aegis: MCP tool name not configured: emit");
        }

        var args = new Dictionary<string, object>
        {
            ["kind"] = appEvent.Kind,
            ["source"] = appEvent.Source,
            ["attrs"] = appEvent.Attrs,
            ["epochMillis"] = appEvent.EpochMillis,
        };
        var res = await _mcp.CallToolAsync(_tools.Emit, args, cancellationToken).ConfigureAwait(false);
        if (res.IsError)
        {
            throw new InvalidOperationException($"aegis: emit tool reported error: {res.Text()}");
        }
    }

    private async Task<List<JsonElement>> ReadListAsync(string tool, string what, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tool))
        {
            throw new InvalidOperationException($"aegis: MCP tool name not configured: {what}");
        }

        var res = await _mcp.CallToolAsync(tool, null, ct).ConfigureAwait(false);
        if (res.IsError)
        {
            throw new InvalidOperationException($"aegis: {what} tool reported error: {res.Text()}");
        }

        var text = res.Text();
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"aegis: {what}: expected a JSON array");
        }

        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static string Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
