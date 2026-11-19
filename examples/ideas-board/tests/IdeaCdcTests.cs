using CockroachDB.DotNet.Abstractions.Cdc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IdeasBoard.Tests;

/// <summary>
/// Exercises the changefeed handler without a live changefeed. The Testing package
/// has no CDC simulator (ConcurrencyRunner, RetrySimulator, FollowerReadSimulator
/// only), so the test builds a synthetic ChangefeedPayload&lt;Idea&gt; — the same
/// shape the webhook endpoint deserializes from CockroachDB — and invokes the
/// handler directly, asserting every event was processed.
/// </summary>
public class IdeaCdcTests
{
    [Fact]
    public async Task HandleAsync_ProcessesEveryChangefeedEvent()
    {
        var handler = new IdeaChangefeedHandler(NullLogger<IdeaChangefeedHandler>.Instance);

        // tag::cdc-test[]
        // Synthetic changefeed payload: two inserted/updated ideas, as CockroachDB
        // would POST them to the webhook endpoint.
        var payload = new ChangefeedPayload<Idea>
        {
            Payload =
            [
                new ChangefeedEvent<Idea>
                {
                    After = new Idea { Id = Guid.NewGuid(), Text = "dark mode", Votes = 7 },
                    Updated = "1700000000000000000.0000000000",
                },
                new ChangefeedEvent<Idea>
                {
                    After = new Idea { Id = Guid.NewGuid(), Text = "export to CSV", Votes = 3 },
                    Updated = "1700000000000000001.0000000000",
                },
            ],
        };

        await handler.HandleAsync(payload, CancellationToken.None);

        Assert.Equal(2, handler.ProcessedCount);
        // end::cdc-test[]
    }
}
