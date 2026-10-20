using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CockroachDB.DotNet.Continuum.Aegis;
using CockroachDB.DotNet.Continuum.Mcp;
using Xunit;

namespace CockroachDB.DotNet.Continuum.Tests;

public class McpTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        public readonly List<HttpRequestMessage> Requests = [];
        public readonly List<string> Bodies = [];
        public bool Sse;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(request);
            Bodies.Add(body);

            using var reqDoc = JsonDocument.Parse(body);
            var root = reqDoc.RootElement;
            if (!root.TryGetProperty("id", out _))
            {
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }

            var id = root.GetProperty("id").GetRawText();
            var method = root.GetProperty("method").GetString();
            string resultJson = method switch
            {
                "initialize" => """{"protocolVersion":"2025-06-18"}""",
                "tools/list" => """{"tools":[{"name":"list_findings","description":"d"}]}""",
                "tools/call" => """{"content":[{"type":"text","text":"ok"}]}""",
                _ => "{}",
            };
            var env = $$"""{"jsonrpc":"2.0","id":{{id}},"result":{{resultJson}}}""";

            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            if (method == "initialize")
            {
                resp.Headers.TryAddWithoutValidation("Mcp-Session-Id", "sess-123");
            }

            if (Sse)
            {
                resp.Content = new StringContent($"event: message\ndata: {env}\n\n", Encoding.UTF8);
                resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
            }
            else
            {
                resp.Content = new StringContent(env, Encoding.UTF8, "application/json");
            }

            return resp;
        }
    }

    private static HttpMcpClient ClientWith(FakeHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://mcp/") };
        http.DefaultRequestHeaders.Add("Authorization", "Bearer tok");
        return new HttpMcpClient(http);
    }

    [Fact]
    public async Task Simulated_ListsToolsAndEchoes()
    {
        var c = new SimulatedMcpClient();
        Assert.NotEmpty(await c.ListToolsAsync());
        Assert.Equal("pong", (await c.CallToolAsync("ping")).Text());
        var echoed = (await c.CallToolAsync("echo", new Dictionary<string, object> { ["k"] = "v" })).Text();
        Assert.Contains("\"k\":\"v\"", echoed);
        Assert.Equal(2, c.Calls.Count);
    }

    [Fact]
    public async Task Http_InitializeListCall_Json()
    {
        var handler = new FakeHandler();
        var c = ClientWith(handler);

        var tools = await c.ListToolsAsync();
        Assert.Single(tools);
        Assert.Equal("list_findings", tools[0].Name);
        Assert.Equal("ok", (await c.CallToolAsync("list_findings")).Text());

        var methods = handler.Bodies
            .Select(b => JsonDocument.Parse(b).RootElement.GetProperty("method").GetString())
            .ToList();
        Assert.Equal(["initialize", "notifications/initialized", "tools/list", "tools/call"], methods);
        // tools/list (index 2) echoes the server-assigned session id.
        Assert.True(handler.Requests[2].Headers.TryGetValues("Mcp-Session-Id", out var sids));
        Assert.Equal("sess-123", sids!.First());
        Assert.True(handler.Requests[2].Headers.Contains("MCP-Protocol-Version"));
    }

    [Fact]
    public async Task Http_SseResponseParsed()
    {
        var c = ClientWith(new FakeHandler { Sse = true });
        Assert.Equal("ok", (await c.CallToolAsync("list_findings")).Text());
    }

    [Fact]
    public async Task Http_ServerErrorSurfaced()
    {
        var handler = new ErrorHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://mcp/") };
        var c = new HttpMcpClient(http);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.ListToolsAsync());
    }

    private sealed class ErrorHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("id", out _))
            {
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }

            var id = root.GetProperty("id").GetRawText();
            var method = root.GetProperty("method").GetString();
            var env = method == "initialize"
                ? "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{}}"
                : "{\"jsonrpc\":\"2.0\",\"id\":" + id
                    + ",\"error\":{\"code\":-32601,\"message\":\"method not found\"}}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(env, Encoding.UTF8, "application/json"),
            };
        }
    }

    // ---- Aegis-over-MCP adapter ----

    private sealed class FakeMcp : IMcpClient
    {
        public required Dictionary<string, CallResult> Results;
        public readonly List<(string Name, IReadOnlyDictionary<string, object>? Args)> Calls = [];

        public Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Tool>>([]);

        public Task<CallResult> CallToolAsync(
            string name,
            IReadOnlyDictionary<string, object>? arguments = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((name, arguments));
            return Task.FromResult(Results.TryGetValue(name, out var r) ? r : new CallResult([], false));
        }
    }

    private static CallResult TextResult(string s) => new([new ContentBlock("text", s)], false);

    [Fact]
    public async Task Aegis_FindingsAndRecommendationsParsed()
    {
        var mcp = new FakeMcp
        {
            Results = new()
            {
                ["list_findings"] = TextResult(
                    """[{"id":"f1","severity":"warning","title":"hot","detail":"d"}]"""),
                ["list_recs"] = TextResult("""[{"id":"r1","title":"split","detail":"ALTER"}]"""),
            },
        };
        var c = new McpAegisClient(mcp, new McpToolNames(Findings: "list_findings", Recommendations: "list_recs"));

        var findings = await c.GetFindingsAsync();
        Assert.Single(findings);
        Assert.Equal("f1", findings[0].Id);
        Assert.Equal("warning", findings[0].Severity);

        var recs = await c.GetRecommendationsAsync();
        Assert.Equal("ALTER", recs[0].Detail);
    }

    [Fact]
    public async Task Aegis_EmitSendsArgs()
    {
        var mcp = new FakeMcp { Results = [] };
        var c = new McpAegisClient(mcp, new McpToolNames(Emit: "emit_event"));
        await c.EmitAsync(new AppEvent("retry", "svc", new Dictionary<string, object> { ["attempt"] = 2 }, 99));
        Assert.Equal("emit_event", mcp.Calls[0].Name);
        Assert.Equal("retry", mcp.Calls[0].Args!["kind"]);
        Assert.Equal(99L, mcp.Calls[0].Args!["epochMillis"]);
    }

    [Fact]
    public async Task Aegis_UnconfiguredToolThrows()
    {
        var c = new McpAegisClient(new FakeMcp { Results = [] }, new McpToolNames());
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.GetFindingsAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.GetRecommendationsAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => c.EmitAsync(new AppEvent("x", "y", new Dictionary<string, object>(), 0)));
    }

    [Fact]
    public async Task Aegis_ToolErrorSurfaced()
    {
        var mcp = new FakeMcp
        {
            Results = new() { ["list_findings"] = new CallResult([new ContentBlock("text", "boom")], true) },
        };
        var c = new McpAegisClient(mcp, new McpToolNames(Findings: "list_findings"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.GetFindingsAsync());
    }
}
