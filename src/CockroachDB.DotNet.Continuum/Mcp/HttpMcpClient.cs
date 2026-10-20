using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace CockroachDB.DotNet.Continuum.Mcp;

/// <summary>
/// HTTP-backed <see cref="IMcpClient"/> speaking the MCP Streamable HTTP transport
/// (spec 2025-06-18). Lazily performs the initialize handshake before the first
/// tools call, echoes the server-assigned <c>Mcp-Session-Id</c> and the
/// <c>MCP-Protocol-Version</c> header, and handles both <c>application/json</c> and
/// <c>text/event-stream</c> responses. Responses are parsed with
/// <see cref="JsonDocument"/> by field name. Construct with <see cref="Create"/> in
/// production, or inject an <see cref="HttpClient"/> backed by a fake handler in tests.
/// </summary>
public sealed class HttpMcpClient : IMcpClient
{
    /// <summary>The MCP revision this client negotiates.</summary>
    public const string ProtocolVersion = "2025-06-18";

    private readonly HttpClient _http;
    private readonly string _name;
    private readonly string _version;
    private long _id;
    private string? _sessionId;
    private bool _init;

    public HttpMcpClient(
        HttpClient http,
        string clientName = "cockroachdb-dotnet",
        string clientVersion = "1.0")
    {
        _http = http;
        _name = clientName;
        _version = clientVersion;
    }

    /// <summary>Builds a client posting to <paramref name="endpoint"/> with a Bearer token.</summary>
    public static HttpMcpClient Create(string endpoint, string token)
    {
        var http = new HttpClient { BaseAddress = new Uri(endpoint) };
        if (!string.IsNullOrWhiteSpace(token))
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return new HttpMcpClient(http);
    }

    public async Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitAsync(cancellationToken).ConfigureAwait(false);
        var result = await CallAsync("tools/list", new Dictionary<string, object>(), cancellationToken)
            .ConfigureAwait(false);
        var tools = new List<Tool>();
        if (result.TryGetProperty("tools", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            tools.AddRange(arr.EnumerateArray()
                .Select(t => new Tool(Str(t, "name"), Str(t, "title"), Str(t, "description"))));
        }

        return tools;
    }

    public async Task<CallResult> CallToolAsync(
        string name,
        IReadOnlyDictionary<string, object>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitAsync(cancellationToken).ConfigureAwait(false);
        var prms = new Dictionary<string, object>
        {
            ["name"] = name,
            ["arguments"] = arguments ?? new Dictionary<string, object>(),
        };
        var result = await CallAsync("tools/call", prms, cancellationToken).ConfigureAwait(false);
        var content = new List<ContentBlock>();
        if (result.TryGetProperty("content", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            content.AddRange(arr.EnumerateArray()
                .Select(c => new ContentBlock(Str(c, "type"), Str(c, "text"))));
        }

        var isError = result.TryGetProperty("isError", out var e) && e.ValueKind == JsonValueKind.True;
        return new CallResult(content, isError);
    }

    private async Task EnsureInitAsync(CancellationToken ct)
    {
        if (_init)
        {
            return;
        }

        var init = new Dictionary<string, object>
        {
            ["protocolVersion"] = ProtocolVersion,
            ["capabilities"] = new Dictionary<string, object>(),
            ["clientInfo"] = new Dictionary<string, object> { ["name"] = _name, ["version"] = _version },
        };
        await CallAsync("initialize", init, ct).ConfigureAwait(false);
        try
        {
            await NotifyAsync("notifications/initialized", ct).ConfigureAwait(false);
        }
        catch
        {
            // A rejected notification must not block us.
        }

        _init = true;
    }

    private async Task<JsonElement> CallAsync(string method, Dictionary<string, object> prms, CancellationToken ct)
    {
        var payload = new Dictionary<string, object>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = Interlocked.Increment(ref _id),
            ["method"] = method,
            ["params"] = prms,
        };
        var text = await PostAsync(payload, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"mcp: {method}: unexpected empty response");
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
        {
            var code = err.TryGetProperty("code", out var c) ? c.GetRawText() : "?";
            throw new InvalidOperationException($"mcp: {method}: server error {code}: {Str(err, "message")}");
        }

        return root.TryGetProperty("result", out var res) ? res.Clone() : default;
    }

    private async Task NotifyAsync(string method, CancellationToken ct)
    {
        var payload = new Dictionary<string, object> { ["jsonrpc"] = "2.0", ["method"] = method };
        await PostAsync(payload, ct).ConfigureAwait(false);
    }

    private async Task<string?> PostAsync(Dictionary<string, object> payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var req = new HttpRequestMessage(HttpMethod.Post, (Uri?)null) { Content = content };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (_sessionId is not null)
        {
            req.Headers.TryAddWithoutValidation("Mcp-Session-Id", _sessionId);
        }

        if (_init)
        {
            req.Headers.TryAddWithoutValidation("MCP-Protocol-Version", ProtocolVersion);
        }

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (resp.Headers.TryGetValues("Mcp-Session-Id", out var sids))
        {
            var sid = sids.FirstOrDefault();
            if (!string.IsNullOrEmpty(sid))
            {
                _sessionId = sid;
            }
        }

        if (resp.StatusCode == HttpStatusCode.Accepted)
        {
            return null;
        }

        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"mcp: HTTP {(int)resp.StatusCode}: {Truncate(body, 200)}");
        }

        return resp.Content.Headers.ContentType?.MediaType == "text/event-stream"
            ? ExtractSse(body)
            : body;
    }

    private static string ExtractSse(string body)
    {
        var sb = new StringBuilder();
        foreach (var line in body.Split('\n'))
        {
            var l = line.TrimEnd('\r');
            if (l.StartsWith("data:", StringComparison.Ordinal))
            {
                sb.Append(l[5..].Trim());
            }
        }

        return sb.ToString();
    }

    private static string Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n];
}
