namespace CockroachDB.DotNet.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class FollowerReadAttribute : Attribute
{
    /// <summary>
    /// When true, every invocation uses follower reads unconditionally.
    /// When false, the caller controls activation at runtime via the pipeline context.
    /// </summary>
    public bool AlwaysStale { get; set; } = true;

    /// <summary>Hours of additive staleness. Composes with <see cref="MinutesAgo"/> and <see cref="SecondsAgo"/>.</summary>
    public long HoursAgo { get; set; }

    /// <summary>Minutes of additive staleness. Composes with <see cref="HoursAgo"/> and <see cref="SecondsAgo"/>.</summary>
    public long MinutesAgo { get; set; }

    /// <summary>Seconds of additive staleness. Composes with <see cref="HoursAgo"/> and <see cref="MinutesAgo"/>.</summary>
    public long SecondsAgo { get; set; }

    /// <summary>
    /// Composes the parts into a validated <see cref="FollowerReadStaleness"/>.
    /// Nothing set → <c>follower_read_timestamp()</c>; else <c>AS OF SYSTEM TIME '-Ns'</c>.
    /// </summary>
    public FollowerReadStaleness ToStaleness() => FollowerReadStaleness.FromAttribute(this);
}
