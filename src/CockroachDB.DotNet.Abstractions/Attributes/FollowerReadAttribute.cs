namespace CockroachDB.DotNet.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class FollowerReadAttribute : Attribute
{
    /// <summary>
    /// When true, every invocation uses follower reads unconditionally.
    /// When false, the caller controls activation at runtime via the pipeline context.
    /// </summary>
    public bool AlwaysStale { get; set; } = true;
}
