namespace CockroachDB.DotNet.Continuum.Aegis;

/// <summary>
/// A Cockroach Aegis finding: an observation the autonomous-ops agents surfaced
/// about a cluster.
/// </summary>
/// <param name="Id">Stable finding id.</param>
/// <param name="Severity"><c>info</c> / <c>warning</c> / <c>critical</c>.</param>
/// <param name="Title">Short human-readable summary.</param>
/// <param name="Detail">Longer explanation.</param>
public sealed record Finding(string Id, string Severity, string Title, string Detail);

/// <summary>
/// A Cockroach Aegis recommendation: a suggested action tied to one or more findings.
/// </summary>
/// <param name="Id">Stable recommendation id.</param>
/// <param name="Title">Short human-readable summary.</param>
/// <param name="Detail">Longer explanation / suggested action.</param>
public sealed record Recommendation(string Id, string Title, string Detail);

/// <summary>
/// An application-side operational event published to Cockroach Aegis ("exhaust").
/// The library sees app intent + DB behavior the cluster alone cannot attribute
/// (retry storms, circuit trips), so this feeds Aegis high-signal data.
/// </summary>
/// <param name="Kind">Event kind, e.g. <c>retry</c>, <c>circuit_open</c>.</param>
/// <param name="Source">Originating component (class/method).</param>
/// <param name="Attrs">Arbitrary structured attributes.</param>
/// <param name="EpochMillis">Event timestamp; pass explicitly for deterministic tests.</param>
public sealed record AppEvent(
    string Kind,
    string Source,
    IReadOnlyDictionary<string, object> Attrs,
    long EpochMillis)
{
    /// <summary>Convenience factory stamping <see cref="DateTimeOffset.UtcNow"/>.</summary>
    public static AppEvent Now(string kind, string source, IReadOnlyDictionary<string, object> attrs)
        => new(kind, source, attrs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
}
