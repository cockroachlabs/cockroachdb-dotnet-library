using CockroachDB.DotNet.Core.FollowerRead;
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

    // Internal for the region-homing unit test (teeth-check): asserts no
    // `SET crdb_region` is ever prefixed. Region homing is done by the
    // CrdbRegionSaveChangesInterceptor stamping the crdb_region column.
    internal static void ApplyRegionPrefix(DbCommand command)
    {
        var prefix = "";

        if (FollowerReadContext.Current)
            prefix += $"SET TRANSACTION AS OF SYSTEM TIME {FollowerReadContext.CurrentStaleness.ToAsOfSystemTime()}; ";

        if (prefix.Length > 0)
            command.CommandText = prefix + command.CommandText;
    }
}
