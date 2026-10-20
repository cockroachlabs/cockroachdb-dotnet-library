using CockroachDB.DotNet.Abstractions.Cdc;
using Microsoft.Extensions.DependencyInjection;

namespace CockroachDB.DotNet.Cdc.Extensions;

public static class CdcServiceCollectionExtensions
{
    public static IServiceCollection AddCockroachDBCdc(this IServiceCollection services)
    {
        services.AddSingleton<IChangefeedManager, ChangefeedManager>();
        return services;
    }

    public static IServiceCollection AddChangefeedHandler<TEntity, THandler>(this IServiceCollection services)
        where THandler : class, IChangefeedHandler<TEntity>
    {
        services.AddScoped<IChangefeedHandler<TEntity>, THandler>();
        return services;
    }
}
