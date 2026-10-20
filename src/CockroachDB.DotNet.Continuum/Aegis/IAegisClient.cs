namespace CockroachDB.DotNet.Continuum.Aegis;

/// <summary>
/// Client for Cockroach Aegis autonomous database operations.
///
/// <para>Read-first: list findings/recommendations the agents produced. Also accepts
/// application "exhaust" via <see cref="EmitAsync"/> so app-attributed signals can
/// inform the agents.</para>
///
/// <para>Aegis is preview/EA and its API is not yet public. The default,
/// fully-functional implementation is <see cref="SimulatedAegisClient"/>; a real
/// <see cref="HttpAegisClient"/> is stubbed until the API lands.</para>
/// </summary>
public interface IAegisClient
{
    /// <summary>List findings the autonomous-ops agents surfaced.</summary>
    Task<IReadOnlyList<Finding>> GetFindingsAsync(CancellationToken cancellationToken = default);

    /// <summary>List recommendations tied to findings.</summary>
    Task<IReadOnlyList<Recommendation>> GetRecommendationsAsync(CancellationToken cancellationToken = default);

    /// <summary>Publish an application-side operational event ("exhaust").</summary>
    Task EmitAsync(AppEvent appEvent, CancellationToken cancellationToken = default);
}
