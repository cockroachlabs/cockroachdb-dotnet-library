using CockroachDB.DotNet.Abstractions.Attributes;

namespace CockroachDB.DotNet.Core.FollowerRead;

public static class FollowerReadContext
{
    private static readonly AsyncLocal<bool> Holder = new();
    private static readonly AsyncLocal<FollowerReadStaleness> StalenessHolder = new();

    public static bool Current
    {
        get => Holder.Value;
        set => Holder.Value = value;
    }

    /// <summary>Staleness applied by the EF command interceptor while a scope is active.</summary>
    public static FollowerReadStaleness CurrentStaleness
    {
        get => StalenessHolder.Value;
        set => StalenessHolder.Value = value;
    }

    public static FollowerReadScope Scoped() => new(default);

    public static FollowerReadScope Scoped(FollowerReadStaleness staleness) => new(staleness);
}

public readonly struct FollowerReadScope : IDisposable
{
    private readonly bool _previous;
    private readonly FollowerReadStaleness _previousStaleness;

    public FollowerReadScope(FollowerReadStaleness staleness)
    {
        _previous = FollowerReadContext.Current;
        _previousStaleness = FollowerReadContext.CurrentStaleness;
        FollowerReadContext.Current = true;
        FollowerReadContext.CurrentStaleness = staleness;
    }

    public void Dispose()
    {
        FollowerReadContext.Current = _previous;
        FollowerReadContext.CurrentStaleness = _previousStaleness;
    }
}
