using CockroachDB.DotNet.Continuum.Aegis;
using Xunit;

namespace CockroachDB.DotNet.Continuum.Tests.Aegis;

public class SimulatedAegisClientTests
{
    [Fact]
    public async Task GetFindings_ReturnsCannedFindings()
    {
        var client = new SimulatedAegisClient();

        var findings = await client.GetFindingsAsync();

        Assert.NotEmpty(findings);
        Assert.Contains(findings, f => f.Id == "sim-hot-range");
        Assert.All(findings, f => Assert.False(string.IsNullOrWhiteSpace(f.Severity)));
    }

    [Fact]
    public async Task GetRecommendations_ReturnsCannedRecommendations()
    {
        var client = new SimulatedAegisClient();

        var recs = await client.GetRecommendationsAsync();

        Assert.NotEmpty(recs);
        Assert.Contains(recs, r => r.Id == "sim-rec-split");
    }

    [Fact]
    public async Task Emit_RecordsEventsInOrder()
    {
        var client = new SimulatedAegisClient();
        var attrs = new Dictionary<string, object> { ["k"] = "v" };

        await client.EmitAsync(new AppEvent("first", "svc", attrs, 1));
        await client.EmitAsync(new AppEvent("second", "svc", attrs, 2));

        Assert.Equal(2, client.EmittedEvents.Count);
        Assert.Equal("first", client.EmittedEvents[0].Kind);
        Assert.Equal("second", client.EmittedEvents[1].Kind);
    }

    [Fact]
    public void AppEvent_Now_StampsTimestamp()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var e = AppEvent.Now("retry", "svc", new Dictionary<string, object>());

        Assert.True(e.EpochMillis >= before);
        Assert.Equal("retry", e.Kind);
    }
}

public class HttpAegisClientTests
{
    [Fact]
    public async Task AllOperations_ThrowNotSupported()
    {
        var client = new HttpAegisClient("https://aegis.example", "token");

        Assert.Equal("https://aegis.example", client.Endpoint);
        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetFindingsAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetRecommendationsAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            client.EmitAsync(new AppEvent("k", "s", new Dictionary<string, object>(), 0)));
    }
}

public class AegisExhaustTests
{
    [Fact]
    public async Task RecordRetry_EmitsRetryEventWithAttempt()
    {
        var client = new SimulatedAegisClient();
        var exhaust = new AegisExhaust(client);

        await exhaust.RecordRetryAsync("OrderService.transfer", 3);

        var e = Assert.Single(client.EmittedEvents);
        Assert.Equal("retry", e.Kind);
        Assert.Equal("OrderService.transfer", e.Source);
        Assert.Equal(3, Assert.IsType<int>(e.Attrs["attempt"]));
    }

    [Fact]
    public async Task RecordCircuitOpen_EmitsCircuitOpenEvent()
    {
        var client = new SimulatedAegisClient();
        var exhaust = new AegisExhaust(client);

        await exhaust.RecordCircuitOpenAsync("OrderService");

        var e = Assert.Single(client.EmittedEvents);
        Assert.Equal("circuit_open", e.Kind);
        Assert.Equal("OrderService", e.Source);
        Assert.Empty(e.Attrs);
    }

    [Fact]
    public async Task Record_EmitsArbitraryEvent()
    {
        var client = new SimulatedAegisClient();
        var exhaust = new AegisExhaust(client);

        await exhaust.RecordAsync(new AppEvent("custom", "src", new Dictionary<string, object>(), 42));

        Assert.Equal("custom", Assert.Single(client.EmittedEvents).Kind);
    }
}
