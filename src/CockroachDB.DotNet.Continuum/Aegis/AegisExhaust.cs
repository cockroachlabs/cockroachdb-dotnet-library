namespace CockroachDB.DotNet.Continuum.Aegis;

/// <summary>
/// Publishes application-side operational signals ("exhaust") to Cockroach Aegis.
///
/// <para>The library sees app-attributed events — serialization retries, circuit
/// trips — that the cluster alone cannot tie back to a specific service/method.
/// Call these convenience methods from your retry/circuit hooks (or wire them into
/// the metrics layer) to feed Aegis. Backed by any <see cref="IAegisClient"/>
/// (the <see cref="SimulatedAegisClient"/> by default).</para>
/// </summary>
public sealed class AegisExhaust(IAegisClient client)
{
    /// <summary>Emit an arbitrary event.</summary>
    public Task RecordAsync(AppEvent appEvent, CancellationToken cancellationToken = default)
        => client.EmitAsync(appEvent, cancellationToken);

    /// <summary>Emit a serialization-retry event attributed to <paramref name="source"/>.</summary>
    public Task RecordRetryAsync(string source, int attempt, CancellationToken cancellationToken = default)
        => client.EmitAsync(
            AppEvent.Now("retry", source, new Dictionary<string, object> { ["attempt"] = attempt }),
            cancellationToken);

    /// <summary>Emit a circuit-open event attributed to <paramref name="source"/>.</summary>
    public Task RecordCircuitOpenAsync(string source, CancellationToken cancellationToken = default)
        => client.EmitAsync(
            AppEvent.Now("circuit_open", source, new Dictionary<string, object>()),
            cancellationToken);
}
