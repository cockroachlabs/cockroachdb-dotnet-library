namespace CockroachDB.DotNet.Continuum.Aegis;

/// <summary>
/// Placeholder for the real Cockroach Aegis HTTP client.
///
/// <para>The Aegis API is not yet public, so every operation throws
/// <see cref="NotSupportedException"/> — callers never silently believe they are
/// talking to a live Aegis. Use <see cref="SimulatedAegisClient"/> for development
/// and tests.</para>
/// </summary>
public sealed class HttpAegisClient(string endpoint, string token) : IAegisClient
{
    private const string NotPublic = "Aegis API not yet public";

    /// <summary>The configured Aegis endpoint.</summary>
    public string Endpoint { get; } = endpoint;

    /// <summary>The configured Aegis bearer token.</summary>
    public string Token { get; } = token;

    /// <inheritdoc />
    public Task<IReadOnlyList<Finding>> GetFindingsAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NotPublic);

    /// <inheritdoc />
    public Task<IReadOnlyList<Recommendation>> GetRecommendationsAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NotPublic);

    /// <inheritdoc />
    public Task EmitAsync(AppEvent appEvent, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(NotPublic);
}
