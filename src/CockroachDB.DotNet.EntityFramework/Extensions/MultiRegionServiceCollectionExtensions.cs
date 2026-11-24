using CockroachDB.DotNet.Abstractions.Configuration;
using CockroachDB.DotNet.EntityFramework.MultiRegion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CockroachDB.DotNet.EntityFramework.Extensions;

public static class MultiRegionServiceCollectionExtensions
{
    /// <summary>
    /// Registers a startup check that compares <typeparamref name="TContext"/>'s
    /// <c>[GlobalTable]</c> / <c>[RegionalByRow]</c> / <c>[CrdbRegion]</c> attributes against the
    /// live CockroachDB schema and database multi-region configuration, suggesting fix-up SQL for
    /// every mismatch. It only reads; it never changes the schema, and it skips quietly when the
    /// database is not CockroachDB.
    ///
    /// <para>Behaviour is driven by <c>CockroachDB:MultiRegion:Mode</c>
    /// (<see cref="MultiRegionOptions.Mode"/>), which defaults to <see cref="MultiRegionCheckMode.Warn"/>:
    /// mismatches are logged, but a contradiction (an entity whose attribute names one locality while
    /// its table has the other, or an entity carrying both attributes) fails startup even in warn.
    /// <see cref="MultiRegionCheckMode.Validate"/> fails startup on any mismatch;
    /// <see cref="MultiRegionCheckMode.None"/> turns the check off.</para>
    ///
    /// <para>Call after binding <see cref="CockroachDBOptions"/> (e.g. via <c>AddCockroachDB</c>)
    /// and registering <typeparamref name="TContext"/> (e.g. via <c>AddDbContext</c>).</para>
    /// </summary>
    public static IServiceCollection AddCockroachDBMultiRegionCheck<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddHostedService<MultiRegionCheckHostedService<TContext>>();
        return services;
    }
}
