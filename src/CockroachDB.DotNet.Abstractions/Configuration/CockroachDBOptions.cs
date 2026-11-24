namespace CockroachDB.DotNet.Abstractions.Configuration;

public sealed class CockroachDBOptions
{
    public const string SectionName = "CockroachDB";

    public RetryOptions Retry { get; set; } = new();

    public RegionOptions Region { get; set; } = new();

    public MetricsOptions Metrics { get; set; } = new();

    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();

    public MultiRegionOptions MultiRegion { get; set; } = new();
}

public sealed class MultiRegionOptions
{
    /// <summary>
    /// How the startup multi-region check behaves. Defaults to <see cref="MultiRegionCheckMode.Warn"/>:
    /// mismatches are logged, but a contradiction (an entity whose annotation names one locality while
    /// its table has the other, or an entity carrying both annotations) fails startup even in warn.
    /// <see cref="MultiRegionCheckMode.None"/> turns the check off entirely.
    /// </summary>
    public MultiRegionCheckMode Mode { get; set; } = MultiRegionCheckMode.Warn;
}

public enum MultiRegionCheckMode
{
    None,
    Warn,
    Validate
}

public sealed class RetryOptions
{
    public int MaxRetries { get; set; } = 3;

    public long DelayMs { get; set; } = 100;

    public double Multiplier { get; set; } = 2.0;

    public long MaxDelayMs { get; set; } = 5000;

    public long JitterMs { get; set; } = 50;
}

public sealed class RegionOptions
{
    public string DefaultRegion { get; set; } = "";

    public bool Validate { get; set; }

    public List<string> Allowed { get; set; } = [];

    public RegionResolverStrategy Resolver { get; set; } = RegionResolverStrategy.Context;

    public string HeaderName { get; set; } = "X-CockroachDB-Region";
}

public enum RegionResolverStrategy
{
    Static,
    Header,
    Context,
    Custom
}

public sealed class MetricsOptions
{
    public bool Enabled { get; set; } = true;
}

public sealed class CircuitBreakerOptions
{
    public int FailureThreshold { get; set; } = 5;

    public int DurationOfBreakSeconds { get; set; } = 30;

    public int SamplingDurationSeconds { get; set; } = 60;
}
