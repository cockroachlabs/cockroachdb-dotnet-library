using System;
using CockroachDB.DotNet.EntityFramework.MultiRegion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CockroachDB.DotNet.EntityFramework.Extensions;

/// <summary>
/// EF-layer registration for CockroachDB. Registering a context through these helpers turns the
/// startup multi-region check <b>on by default</b> (mirroring the Spring starter, which gates its
/// autoconfig on Hibernate being present): the check lives in the EF integration, so an application
/// that uses only the raw Core/Npgsql pipeline pulls no EF Core and gets no check — it has no entity
/// model to check. <c>CockroachDB:MultiRegion:Mode=none</c> turns the check off.
/// </summary>
public static class MultiRegionServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TContext"/> via <see cref="EntityFrameworkServiceCollectionExtensions.AddDbContext{TContext}(IServiceCollection, Action{DbContextOptionsBuilder}, ServiceLifetime, ServiceLifetime)"/>
    /// and turns on the startup multi-region check. Configure the context (including
    /// <c>UseCockroachDB</c>) in <paramref name="optionsAction"/>.
    /// </summary>
    public static IServiceCollection AddCockroachDBDbContext<TContext>(
        this IServiceCollection services, Action<DbContextOptionsBuilder> optionsAction)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>(optionsAction);
        RegisterCheck(services);
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IDbContextFactory{TContext}"/> for <typeparamref name="TContext"/>
    /// and turns on the startup multi-region check. Configure the context (including
    /// <c>UseCockroachDB</c>) in <paramref name="optionsAction"/>.
    /// </summary>
    public static IServiceCollection AddCockroachDBDbContextFactory<TContext>(
        this IServiceCollection services, Action<DbContextOptionsBuilder> optionsAction)
        where TContext : DbContext
    {
        services.AddDbContextFactory<TContext>(optionsAction);
        RegisterCheck(services);
        return services;
    }

    /// <summary>
    /// Registers the startup multi-region check explicitly, for every registered EF Core
    /// <see cref="DbContext"/>. Normally unnecessary — <see cref="AddCockroachDBDbContext{TContext}"/>
    /// and <see cref="AddCockroachDBDbContextFactory{TContext}"/> already turn it on — but useful when
    /// a context is registered with plain <c>AddDbContext</c>. Safe to call more than once.
    /// </summary>
    public static IServiceCollection AddCockroachDBMultiRegionCheck(this IServiceCollection services)
    {
        RegisterCheck(services);
        return services;
    }

    /// <summary>
    /// <typeparamref name="TContext"/> overload of <see cref="AddCockroachDBMultiRegionCheck(IServiceCollection)"/>,
    /// kept as a no-op-safe explicit opt-in. The check covers every registered context, so the type
    /// argument is advisory.
    /// </summary>
    public static IServiceCollection AddCockroachDBMultiRegionCheck<TContext>(this IServiceCollection services)
        where TContext : DbContext
        => services.AddCockroachDBMultiRegionCheck();

    private static void RegisterCheck(IServiceCollection services)
    {
        // The registry reads the live service collection at startup to discover contexts added after
        // this call. TryAddSingleton + AddHostedService's type de-duplication keep repeated
        // registration (e.g. several contexts) to a single registry and a single hosted service.
        services.TryAddSingleton(new MultiRegionContextRegistry(services));
        services.AddHostedService<MultiRegionStartupCheck>();
    }
}
