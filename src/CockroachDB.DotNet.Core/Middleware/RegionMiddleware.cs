using CockroachDB.DotNet.Abstractions.Configuration;
using CockroachDB.DotNet.Abstractions.Pipeline;
using CockroachDB.DotNet.Abstractions.Region;
using CockroachDB.DotNet.Core.Region;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CockroachDB.DotNet.Core.Middleware;

public sealed class RegionMiddleware(
    IOptions<CockroachDBOptions> options,
    IRegionResolver regionResolver,
    ILogger<RegionMiddleware> logger) : ICockroachDBMiddleware
{
    public async Task<T> ExecuteAsync<T>(
        CockroachDBContext context,
        Func<CockroachDBContext, Task<T>> next)
    {
        var region = ResolveRegion(context);

        if (string.IsNullOrEmpty(region))
            return await next(context);

        var opts = options.Value.Region;
        if (opts.Validate && opts.Allowed.Count > 0 &&
            !opts.Allowed.Contains(region, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Region '{region}' is not in the allowed list: [{string.Join(", ", opts.Allowed)}]");
        }

        context.Region = region;

        // CockroachDB has no `crdb_region` session variable — a REGIONAL BY ROW row
        // is homed by writing its `crdb_region` COLUMN, not with `SET`. Scope the
        // region so the EF CrdbRegionSaveChangesInterceptor stamps that column on
        // insert; the scope flows (AsyncLocal) through the operation run by next().
        using var _ = RegionContext.Scoped(region);

        logger.LogDebug("CockroachDB region scoped to '{Region}'.", region);

        return await next(context);
    }

    private string? ResolveRegion(CockroachDBContext context)
    {
        if (context.Attribute?.Region is { Length: > 0 } attrRegion)
            return attrRegion;

        if (RegionContext.Current is { Length: > 0 } contextRegion)
            return contextRegion;

        var resolved = regionResolver.Resolve();
        if (!string.IsNullOrEmpty(resolved))
            return resolved;

        var defaultRegion = options.Value.Region.DefaultRegion;
        return string.IsNullOrEmpty(defaultRegion) ? null : defaultRegion;
    }
}
