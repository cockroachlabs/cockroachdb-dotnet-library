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
using CockroachDB.DotNet.Core.Pipeline;
using CockroachDB.DotNet.Core.Region;
using CockroachDB.DotNet.Core.Template;
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

        return services;
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
