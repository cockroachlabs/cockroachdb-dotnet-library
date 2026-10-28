using CockroachDB.DotNet.Abstractions.Attributes;

namespace CockroachDB.DotNet.Abstractions.Pipeline;

public sealed class CockroachDBContext
{
    public CockroachDBAttribute? Attribute { get; init; }

    public CockroachDBFallbackAttribute? FallbackAttribute { get; init; }

    public string? Region { get; set; }

    public TransactionIsolation Isolation { get; set; } = TransactionIsolation.Serializable;

    public bool ReadOnly { get; set; }

    public bool UseFollowerRead { get; set; }

    /// <summary>Additive follower-read staleness. Default (all-zero) uses <c>follower_read_timestamp()</c>.</summary>
    public FollowerReadStaleness Staleness { get; set; }

    public int AttemptNumber { get; set; }

    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();
}
