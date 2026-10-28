using CockroachDB.DotNet.Core.FollowerRead;
using CockroachDB.DotNet.Core.Region;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace CockroachDB.DotNet.EntityFramework.Interceptors;

public sealed class CockroachDBCommandInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        ApplyRegionPrefix(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        ApplyRegionPrefix(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyRegionPrefix(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyRegionPrefix(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private static void ApplyRegionPrefix(DbCommand command)
    {
        var prefix = "";

        if (FollowerReadContext.Current)
            prefix += $"SET TRANSACTION AS OF SYSTEM TIME {FollowerReadContext.CurrentStaleness.ToAsOfSystemTime()}; ";

        if (RegionContext.Current is { Length: > 0 } region)
        {
            var safeRegion = region.Replace("'", "''");
            prefix += $"SET crdb_region = '{safeRegion}'; ";
        }

        if (prefix.Length > 0)
            command.CommandText = prefix + command.CommandText;
    }
}
