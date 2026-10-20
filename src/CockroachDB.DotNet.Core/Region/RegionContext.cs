namespace CockroachDB.DotNet.Core.Region;

public static class RegionContext
{
    private static readonly AsyncLocal<string?> Holder = new();

    public static string? Current
    {
        get => Holder.Value;
        set => Holder.Value = value;
    }

    public static RegionScope Scoped(string region)
    {
        return new RegionScope(region);
    }
}

public readonly struct RegionScope : IDisposable
{
    private readonly string? _previous;

    internal RegionScope(string region)
    {
        _previous = RegionContext.Current;
        RegionContext.Current = region;
    }

    public void Dispose()
    {
        RegionContext.Current = _previous;
    }
}
