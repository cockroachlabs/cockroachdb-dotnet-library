namespace CockroachDB.DotNet.Abstractions.Cdc;

public interface IChangefeedHandler<TEntity>
{
    Task HandleAsync(ChangefeedPayload<TEntity> payload, CancellationToken cancellationToken = default);
}

public sealed record ChangefeedPayload<TEntity>
{
    public IReadOnlyList<ChangefeedEvent<TEntity>> Payload { get; init; } = [];
}

public sealed record ChangefeedEvent<TEntity>
{
    public TEntity? After { get; init; }

    public TEntity? Before { get; init; }

    public ChangefeedKey? Key { get; init; }

    public string? Topic { get; init; }

    public string? Updated { get; init; }
}

public sealed record ChangefeedKey
{
    public IReadOnlyList<object>? Values { get; init; }
}
