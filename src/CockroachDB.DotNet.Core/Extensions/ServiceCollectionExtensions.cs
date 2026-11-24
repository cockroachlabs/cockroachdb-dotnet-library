using CockroachDB.DotNet.Abstractions.Configuration;
using CockroachDB.DotNet.Abstractions.Exceptions;
using CockroachDB.DotNet.Abstractions.Metrics;
using CockroachDB.DotNet.Abstractions.Pipeline;
using CockroachDB.DotNet.Abstractions.Region;
using CockroachDB.DotNet.Abstractions.Template;
using CockroachDB.DotNet.Core.Exceptions;
using CockroachDB.DotNet.Core.Health;
using CockroachDB.DotNet.Core.Metrics;
using CockroachDB.DotNet.Core.Middleware;
using CockroachDB.DotNet.Core.MultiRegion;
using CockroachDB.DotNet.Core.Pipeline;
using CockroachDB.DotNet.Core.Region;
using CockroachDB.DotNet.Core.Template;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CockroachDB.DotNet.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCockroachDB(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<CockroachDBPipelineBuilder>? configurePipeline = null)
    {
        services.Configure<CockroachDBOptions>(
            configuration.GetSection(CockroachDBOptions.SectionName));

        services.AddSingleton<IExceptionClassifier, NpgsqlExceptionClassifier>();
        services.AddSingleton<ICockroachDBMetrics, InMemoryMetrics>();
        services.AddSingleton<IRegionResolver, DefaultRegionResolver>();
        services.AddSingleton<ICockroachDBExecutor, CockroachDBPipelineExecutor>();
        services.AddSingleton<ICockroachDBTemplate, CockroachDBTemplate>();

        var builder = new CockroachDBPipelineBuilder(services);

        if (configurePipeline is not null)
        {
            configurePipeline(builder);
        }
        else
        {
            builder.UseMetrics();
            builder.UseCircuitBreaker();
            builder.UseRetry();
            builder.UseRegion();
            builder.UseFollowerRead();
        }

        services.AddHealthChecks()
            .AddCheck<CockroachDBHealthCheck>("cockroachdb");

        // On by default, like the Spring starter: check every registered EF Core context's
        // [GlobalTable]/[RegionalByRow] attributes against the live schema at startup. It is a quiet
        // no-op when no context is registered or the database is not CockroachDB, and
        // CockroachDB:MultiRegion:Mode=none turns it off entirely.
        AddMultiRegionCheck(services);

        return services;
    }

    /// <summary>
    /// Registers the startup multi-region check explicitly. This is normally unnecessary:
    /// <see cref="AddCockroachDB"/> already registers it on by default, for every EF Core
    /// <see cref="DbContext"/> in the application. It is kept as a no-op-safe explicit opt-in;
    /// calling it more than once, or alongside <see cref="AddCockroachDB"/>, registers the check once.
    /// <typeparamref name="TContext"/> is accepted for intent; the check covers every registered context.
    /// </summary>
    public static IServiceCollection AddCockroachDBMultiRegionCheck<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        AddMultiRegionCheck(services);
        return services;
    }

    private static void AddMultiRegionCheck(IServiceCollection services)
    {
        // The registry reads the live service collection at startup to discover contexts added after
        // this call. AddHostedService de-duplicates by type, so repeated registration is harmless.
        services.AddSingleton(new MultiRegionContextRegistry(services));
        services.AddHostedService<MultiRegionStartupCheck>();
    }
}

public sealed class CockroachDBPipelineBuilder(IServiceCollection services)
{
    public CockroachDBPipelineBuilder UseMetrics()
    {
        services.AddSingleton<ICockroachDBMiddleware, MetricsMiddleware>();
        return this;
    }

    public CockroachDBPipelineBuilder UseCircuitBreaker()
    {
        services.AddSingleton<ICockroachDBMiddleware, CircuitBreakerMiddleware>();
        return this;
    }

    public CockroachDBPipelineBuilder UseRetry()
    {
        services.AddSingleton<ICockroachDBMiddleware, RetryMiddleware>();
        return this;
    }

    public CockroachDBPipelineBuilder UseRegion()
    {
        services.AddSingleton<ICockroachDBMiddleware, RegionMiddleware>();
        return this;
    }

    public CockroachDBPipelineBuilder UseFollowerRead()
    {
        services.AddSingleton<ICockroachDBMiddleware, FollowerReadMiddleware>();
        return this;
    }

    public CockroachDBPipelineBuilder Use<TMiddleware>() where TMiddleware : class, ICockroachDBMiddleware
    {
        services.AddSingleton<ICockroachDBMiddleware, TMiddleware>();
        return this;
    }
}
