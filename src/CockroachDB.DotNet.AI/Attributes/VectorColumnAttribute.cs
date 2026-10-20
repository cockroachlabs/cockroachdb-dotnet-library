namespace CockroachDB.DotNet.AI.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class VectorColumnAttribute : Attribute
{
    public required int Dimensions { get; init; }
}
