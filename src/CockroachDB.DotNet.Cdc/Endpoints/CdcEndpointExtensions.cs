using CockroachDB.DotNet.Abstractions.Cdc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CockroachDB.DotNet.Cdc.Endpoints;

public static class CdcEndpointExtensions
{
    public static IEndpointRouteBuilder MapCockroachDBChangefeed<TEntity>(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/webhooks/cockroachdb")
    {
        endpoints.MapPost(pattern, async (
            HttpContext context) =>
        {
            var handler = context.RequestServices.GetRequiredService<IChangefeedHandler<TEntity>>();
            var payload = await context.Request.ReadFromJsonAsync<ChangefeedPayload<TEntity>>();

            if (payload is null)
                return Results.BadRequest("Invalid changefeed payload");

            await handler.HandleAsync(payload, context.RequestAborted);
            return Results.Ok();
        });

        return endpoints;
    }
}
