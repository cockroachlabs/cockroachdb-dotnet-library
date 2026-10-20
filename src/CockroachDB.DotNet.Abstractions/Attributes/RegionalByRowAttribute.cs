namespace CockroachDB.DotNet.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class RegionalByRowAttribute : Attribute
{
    public string DefaultRegion { get; set; } = "";
}
