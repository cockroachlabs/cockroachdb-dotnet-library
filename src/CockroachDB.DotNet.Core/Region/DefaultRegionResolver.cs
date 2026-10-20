using CockroachDB.DotNet.Abstractions.Configuration;
using CockroachDB.DotNet.Abstractions.Region;
using Microsoft.Extensions.Options;

namespace CockroachDB.DotNet.Core.Region;

public sealed class DefaultRegionResolver(
    IOptions<CockroachDBOptions> options) : IRegionResolver
{
    public string? Resolve()
    {
        return options.Value.Region.Resolver switch
        {
            RegionResolverStrategy.Static => options.Value.Region.DefaultRegion,
            RegionResolverStrategy.Context => RegionContext.Current,
            _ => null
        };
    }
}
