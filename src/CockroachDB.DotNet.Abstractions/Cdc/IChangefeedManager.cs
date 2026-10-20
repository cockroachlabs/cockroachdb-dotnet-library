namespace CockroachDB.DotNet.Abstractions.Cdc;

public interface IChangefeedManager
{
    Task<long> CreateChangefeedAsync(
        string tableName,
        string webhookUrl,
        ChangefeedOptions? options = null,
        CancellationToken cancellationToken = default);

    Task CancelChangefeedAsync(
        long jobId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChangefeedJob>> ListChangefeedsAsync(
        CancellationToken cancellationToken = default);
}

public sealed record ChangefeedOptions
{
    public string Format { get; init; } = "json";
    public bool Resolved { get; init; }
    public bool Updated { get; init; }
    public string? EnvelopeType { get; init; }
    public string? SchemaChangePolicy { get; init; }
}

public sealed record ChangefeedJob(
    long JobId,
    string Description,
    string Status);
