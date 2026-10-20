namespace CockroachDB.DotNet.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class CockroachDBAttribute : Attribute
{
    public int MaxRetries { get; set; } = -1;

    public long DelayMs { get; set; } = -1;

    public double Multiplier { get; set; } = -1;

    public long MaxDelayMs { get; set; } = -1;

    public TransactionIsolation Isolation { get; set; } = TransactionIsolation.Serializable;

    public bool ReadOnly { get; set; }

    public string Region { get; set; } = "";
}
