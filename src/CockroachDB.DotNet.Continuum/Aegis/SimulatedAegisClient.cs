using System.Collections.Concurrent;

namespace CockroachDB.DotNet.Continuum.Aegis;

/// <summary>
/// In-memory Cockroach Aegis client for local development and tests. Returns a
/// small canned set of findings/recommendations and records every emitted
/// <see cref="AppEvent"/> so tests (and demos) can inspect the exhaust.
/// </summary>
public sealed class SimulatedAegisClient : IAegisClient
{
    private readonly ConcurrentQueue<AppEvent> _emitted = new();

    /// <inheritdoc />
    public Task<IReadOnlyList<Finding>> GetFindingsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Finding> findings =
        [
            new("sim-hot-range", "warning",
                "Hot range detected",
                "A range is receiving disproportionate traffic; consider load-based splitting."),
            new("sim-retry-rate", "info",
                "Elevated serialization retries",
                "Serialization retries are above baseline for one workload.")
        ];
        return Task.FromResult(findings);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Recommendation>> GetRecommendationsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Recommendation> recommendations =
        [
            new("sim-rec-split",
                "Enable load-based splitting",
                "Allow the cluster to split the hot range to spread load.")
        ];
        return Task.FromResult(recommendations);
    }

    /// <inheritdoc />
    public Task EmitAsync(AppEvent appEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appEvent);
        _emitted.Enqueue(appEvent);
        return Task.CompletedTask;
    }

    /// <summary>All events emitted so far (for tests/inspection).</summary>
    public IReadOnlyList<AppEvent> EmittedEvents => _emitted.ToArray();
}
