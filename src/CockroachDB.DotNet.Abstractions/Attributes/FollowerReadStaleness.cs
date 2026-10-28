namespace CockroachDB.DotNet.Abstractions.Attributes;

/// <summary>
/// Additive follower-read staleness. The parts ADD UP and compose into a single
/// <c>AS OF SYSTEM TIME</c> argument. All-zero (the default value) means
/// "no explicit staleness" and uses <c>follower_read_timestamp()</c>.
/// </summary>
public readonly record struct FollowerReadStaleness(
    long HoursAgo = 0,
    long MinutesAgo = 0,
    long SecondsAgo = 0)
{
    /// <summary>True when no staleness is configured (use <c>follower_read_timestamp()</c>).</summary>
    public bool IsDefault => HoursAgo == 0 && MinutesAgo == 0 && SecondsAgo == 0;

    /// <summary>
    /// Total staleness in seconds. Throws on any negative part — a follower read must
    /// never silently degrade into a current read. Overflow-safe via checked arithmetic.
    /// </summary>
    public long TotalSeconds
    {
        get
        {
            if (HoursAgo < 0 || MinutesAgo < 0 || SecondsAgo < 0)
                throw new ArgumentException(
                    $"Follower-read staleness cannot be negative " +
                    $"(hours={HoursAgo}, minutes={MinutesAgo}, seconds={SecondsAgo}).");

            return checked((HoursAgo * 3600) + (MinutesAgo * 60) + SecondsAgo);
        }
    }

    /// <summary>
    /// The <c>AS OF SYSTEM TIME</c> argument: <c>follower_read_timestamp()</c> when default,
    /// otherwise a validated <c>'-Ns'</c> literal. Only the validated integer is inlined.
    /// </summary>
    public string ToAsOfSystemTime()
        => IsDefault ? "follower_read_timestamp()" : $"'-{TotalSeconds}s'";

    /// <summary>Composes the staleness declared on a <see cref="FollowerReadAttribute"/>.</summary>
    public static FollowerReadStaleness FromAttribute(FollowerReadAttribute attribute)
        => new(attribute.HoursAgo, attribute.MinutesAgo, attribute.SecondsAgo);
}
