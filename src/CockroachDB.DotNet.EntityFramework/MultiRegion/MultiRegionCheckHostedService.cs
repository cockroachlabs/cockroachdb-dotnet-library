using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using CockroachDB.DotNet.Abstractions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CockroachDB.DotNet.EntityFramework.MultiRegion;

/// <summary>
/// Runs the multi-region check for <typeparamref name="TContext"/> once at startup, skipping when
/// the database isn't CockroachDB. In <see cref="MultiRegionCheckMode.Warn"/> it logs what it
/// finds, unless the report holds a contradiction; in <see cref="MultiRegionCheckMode.Validate"/>,
/// or on a contradiction, it fails startup by throwing. <see cref="MultiRegionCheckMode.None"/>
/// turns it off.
/// </summary>
internal sealed class MultiRegionCheckHostedService<TContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<CockroachDBOptions> options,
    ILogger<MultiRegionCheckHostedService<TContext>> logger) : IHostedService
    where TContext : DbContext
{
    private readonly MultiRegionCheckMode _mode = options.Value.MultiRegion.Mode;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_mode == MultiRegionCheckMode.None)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
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
                    "CockroachDB multiregion check ({Source}) skipped: the database is not CockroachDB",
                    MultiRegionReport.Source);
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
                "CockroachDB multiregion check ({Source}) could not read the database: {Message}",
                MultiRegionReport.Source, ex.Message);
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
                "CockroachDB multiregion check ({Source}) passed for {Count} entities",
                MultiRegionReport.Source, tables.Count);
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
