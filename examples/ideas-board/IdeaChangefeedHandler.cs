using CockroachDB.DotNet.Abstractions.Cdc;
using Microsoft.Extensions.Logging;

namespace IdeasBoard;

/// <summary>
/// Reacts to changefeed events for <see cref="Idea"/> rows. In production the
/// webhook endpoint (MapCockroachDBChangefeed) deserializes CockroachDB's
/// changefeed POST body into a <see cref="ChangefeedPayload{T}"/> and invokes this
/// handler. Here it logs each change and keeps a running count so the behavior is
/// observable in a test without a live changefeed.
/// </summary>
// tag::cdc-handler[]
public sealed class IdeaChangefeedHandler(ILogger<IdeaChangefeedHandler> logger)
    : IChangefeedHandler<Idea>
{
    private long _processed;

    /// <summary>Number of changefeed events processed across all payloads.</summary>
    public long ProcessedCount => Interlocked.Read(ref _processed);

    public Task HandleAsync(ChangefeedPayload<Idea> payload, CancellationToken cancellationToken = default)
    {
        foreach (var evt in payload.Payload)
        {
            Interlocked.Increment(ref _processed);
            logger.LogInformation(
                "Idea changefeed event: {Id} now has {Votes} votes ({Text})",
                evt.After?.Id, evt.After?.Votes, evt.After?.Text);
        }

        return Task.CompletedTask;
    }
}
// end::cdc-handler[]
