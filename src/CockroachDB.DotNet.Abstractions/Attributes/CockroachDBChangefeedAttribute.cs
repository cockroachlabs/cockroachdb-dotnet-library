namespace CockroachDB.DotNet.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class CockroachDBChangefeedAttribute : Attribute
{
    public string? WebhookUrl { get; set; }

    public string Format { get; set; } = "json";

    public bool FullTableName { get; set; }

    public string? Options { get; set; }
}
