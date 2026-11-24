using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CockroachDB.DotNet.Abstractions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CockroachDB.DotNet.Core.MultiRegion;

/// <summary>
/// Runs the multi-region check once at startup for <em>every</em> EF Core <see cref="DbContext"/>
/// registered in the application, without the application naming a context. It is registered
/// automatically by <c>AddCockroachDB</c> and mirrors the Spring starter's on-by-default behaviour:
/// it defaults to <see cref="MultiRegionCheckMode.Warn"/>, and <c>CockroachDB:MultiRegion:Mode=none</c>
/// turns it off entirely.
///
/// <para>It discovers contexts from DI at runtime by looking for the <see cref="DbContextOptions{TContext}"/>
/// registrations that both <c>AddDbContext</c> and <c>AddDbContextFactory</c> add, so it stays a quiet
/// no-op when no context is registered. For each context it reads the model and, when the database is
/// CockroachDB, compares the attributes against the live schema. It only reads; it never changes the
/// schema, and it skips quietly for any context whose database is not CockroachDB.</para>
/// </summary>
internal sealed class MultiRegionStartupCheck(
    IServiceScopeFactory scopeFactory,
    MultiRegionContextRegistry registry,
    IOptions<CockroachDBOptions> options,
    ILogger<MultiRegionStartupCheck> logger) : IHostedService
{
    private readonly MultiRegionCheckMode _mode = options.Value.MultiRegion.Mode;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_mode == MultiRegionCheckMode.None)
        {
            return;
        }

        foreach (var contextType in registry.ContextTypes)
        {
            await CheckContextAsync(contextType, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CheckContextAsync(Type contextType, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        // Resolve the context when it is registered as a service (AddDbContext); otherwise create it
        // from its registered DbContextOptions (AddDbContextFactory does not register the context).
        DbContext context;
        try
        {
            context = (DbContext)ActivatorUtilities.GetServiceOrCreateInstance(scope.ServiceProvider, contextType);
        }
        catch (Exception ex) when (ex is InvalidOperationException or MissingMethodException)
        {
            logger.LogDebug(
                "CockroachDB multiregion check ({Source}) skipped {Context}: could not construct it ({Message})",
                MultiRegionReport.Source, contextType.Name, ex.Message);
            return;
        }

        var tables = MultiRegionEntities.Read(context.Model);
        if (tables.Count == 0)
        {
            return;
        }

        var connection = context.Database.GetDbConnection();
        var openedHere = false;
        MultiRegionReport report;
        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                openedHere = true;
            }

            if (!await MultiRegionCheck.IsCockroachDBAsync(connection, cancellationToken).ConfigureAwait(false))
            {
                logger.LogDebug(
                    "CockroachDB multiregion check ({Source}) skipped {Context}: the database is not CockroachDB",
                    MultiRegionReport.Source, contextType.Name);
                return;
            }

            var state = await MultiRegionCheck.LoadAsync(connection, tables, cancellationToken).ConfigureAwait(false);
            report = MultiRegionCheck.Evaluate(state, tables);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            if (_mode == MultiRegionCheckMode.Validate)
            {
                throw new InvalidOperationException(
                    "CockroachDB multiregion check (" + MultiRegionReport.Source + ") could not read the database", ex);
            }

            logger.LogWarning(
                "CockroachDB multiregion check ({Source}) could not read the database for {Context}: {Message}",
                MultiRegionReport.Source, contextType.Name, ex.Message);
            return;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }

        if (report.IsEmpty)
        {
            logger.LogDebug(
                "CockroachDB multiregion check ({Source}) passed for {Count} entities in {Context}",
                MultiRegionReport.Source, tables.Count, contextType.Name);
        }
        else if (report.FailsStartup(_mode))
        {
            throw new InvalidOperationException(report.Format(_mode));
        }
        else
        {
            logger.LogWarning("{Report}", report.Format(_mode));
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Holds a reference to the application's <see cref="IServiceCollection"/> so the startup check can
/// find every registered EF Core context at run time. <c>AddCockroachDB</c> is typically called
/// before the contexts are added, so a snapshot taken then would miss them; the collection object is
/// the same one the host populates, so reading it after the host is built sees them all.
/// </summary>
/// ponytail: reads the live IServiceCollection at startup; replace with explicit context
/// registration if someone mutates the collection after Build (nobody does).
internal sealed class MultiRegionContextRegistry(IServiceCollection services)
{
    public IEnumerable<Type> ContextTypes => services
        .Select(descriptor => descriptor.ServiceType)
        .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(DbContextOptions<>))
        .Select(type => type.GetGenericArguments()[0])
        .Distinct();
}
