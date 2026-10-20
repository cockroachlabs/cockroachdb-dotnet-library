namespace CockroachDB.DotNet.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class CockroachDBFallbackAttribute : Attribute
{
    public Type? FallbackType { get; set; }

    public int FailureThreshold { get; set; } = 5;

    public int DurationOfBreakSeconds { get; set; } = 30;
}
