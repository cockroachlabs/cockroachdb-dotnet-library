using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CockroachDB.DotNet.Continuum.Cloud;

/// <summary>
/// HTTP-backed <see cref="ICloudClient"/> that talks to the real CockroachDB Cloud
/// API using a service-account Bearer token. Responses are parsed with
/// <see cref="JsonDocument"/> by their documented (snake_case) field names, so the
/// client is independent of any serializer naming configuration. Construct with
/// <see cref="Create"/> in production, or inject an <see cref="HttpClient"/> backed
/// by a fake handler in tests.
/// </summary>
public sealed class HttpCloudClient : ICloudClient
{
    /// <summary>Default CockroachDB Cloud API base URL.</summary>
    public const string DefaultBaseUrl = "https://cockroachlabs.cloud/api/v1";

    private readonly HttpClient _http;

    public HttpCloudClient(HttpClient http) => _http = http;

    /// <summary>Builds an <see cref="HttpCloudClient"/> for the given base URL and API key.</summary>
    public static HttpCloudClient Create(string baseUrl, string apiKey)
    {
        var baseStr = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
        var http = new HttpClient { BaseAddress = new Uri(baseStr + "/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return new HttpCloudClient(http);
    }

    public async Task<IReadOnlyList<Cluster>> ListClustersAsync(CancellationToken cancellationToken = default)
    {
        using var resp = await _http.GetAsync("clusters", cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var list = new List<Cluster>();
        if (doc.RootElement.TryGetProperty("clusters", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            list.AddRange(arr.EnumerateArray().Select(ToCluster));
        }

        return list;
    }

    public async Task<Cluster> GetClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        using var resp = await _http.GetAsync($"clusters/{clusterId}", cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ToCluster(doc.RootElement);
    }

    public async Task ConfigureMetricExportAsync(string clusterId, MetricExportConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        var json = JsonSerializer.Serialize(config.Settings ?? new Dictionary<string, object>());
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(
            $"clusters/{clusterId}/metricexport/{config.Target.Path()}", content, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    private static Cluster ToCluster(JsonElement e)
    {
        var regions = new List<string>();
        if (e.TryGetProperty("regions", out var r) && r.ValueKind == JsonValueKind.Array)
        {
            regions.AddRange(r.EnumerateArray().Select(x => x.GetString() ?? string.Empty));
        }

        return new Cluster(Str(e, "id"), Str(e, "name"), Str(e, "plan"), Str(e, "state"), Str(e, "cloud_provider"), regions);
    }

    private static string Str(JsonElement e, string key)
    {
        if (!e.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        return v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : v.ToString();
    }
}
