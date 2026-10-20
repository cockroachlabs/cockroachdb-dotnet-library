using CockroachDB.DotNet.Abstractions.Configuration;
using CockroachDB.DotNet.Abstractions.Pipeline;
using CockroachDB.DotNet.Abstractions.Region;
using CockroachDB.DotNet.Core.Region;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CockroachDB.DotNet.Core.Middleware;

public sealed class RegionMiddleware(
    NpgsqlDataSource dataSource,
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

        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SET crdb_region = '{region.Replace("'", "''")}'";
        await cmd.ExecuteNonQueryAsync();

        logger.LogDebug("CockroachDB region set to '{Region}'.", region);

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
