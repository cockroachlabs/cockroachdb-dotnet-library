namespace CockroachDB.DotNet.Core.FollowerRead;

public static class FollowerReadContext
{
    private static readonly AsyncLocal<bool> Holder = new();

    public static bool Current
    {
        get => Holder.Value;
        set => Holder.Value = value;
    }

    public static FollowerReadScope Scoped()
    {
        return new FollowerReadScope();
    }
}

public readonly struct FollowerReadScope : IDisposable
{
    private readonly bool _previous;

    public FollowerReadScope()
    {
        _previous = FollowerReadContext.Current;
        FollowerReadContext.Current = true;
    }

    public void Dispose()
    {
        FollowerReadContext.Current = _previous;
    }
}
