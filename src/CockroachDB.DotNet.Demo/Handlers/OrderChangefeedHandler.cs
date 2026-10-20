using CockroachDB.DotNet.Abstractions.Cdc;
using CockroachDB.DotNet.Demo.Models;

namespace CockroachDB.DotNet.Demo.Handlers;

public sealed class OrderChangefeedHandler(
    ILogger<OrderChangefeedHandler> logger) : IChangefeedHandler<Order>
{
    public Task HandleAsync(
        ChangefeedPayload<Order> payload,
        CancellationToken cancellationToken)
    {
        foreach (var evt in payload.Payload)
        {
            logger.LogInformation(
                "Order changefeed event: {Product} - ${Amount} in {Region}",
                evt.After?.Product, evt.After?.Amount, evt.After?.Region);
        }

        return Task.CompletedTask;
    }
}
