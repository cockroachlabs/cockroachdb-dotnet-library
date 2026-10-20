using System.Diagnostics;
using CockroachDB.DotNet.Abstractions.Metrics;
using CockroachDB.DotNet.Abstractions.Pipeline;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CockroachDB.DotNet.Core.Middleware;

public sealed class FollowerReadMiddleware(
    NpgsqlDataSource dataSource,
    ICockroachDBMetrics metrics,
    ILogger<FollowerReadMiddleware> logger) : ICockroachDBMiddleware
{
    public const string FollowerReadSql =
        "SET TRANSACTION AS OF SYSTEM TIME follower_read_timestamp()";

    public async Task<T> ExecuteAsync<T>(
        CockroachDBContext context,
        Func<CockroachDBContext, Task<T>> next)
    {
        if (!context.UseFollowerRead)
            return await next(context);

        var startTimestamp = Stopwatch.GetTimestamp();
        var success = false;

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = FollowerReadSql;
            await cmd.ExecuteNonQueryAsync();

            logger.LogDebug("CockroachDB follower read activated");

            context.ReadOnly = true;
            context.Items["cockroachdb.follower_read.connection"] = connection;

            var result = await next(context);
            success = true;
            return result;
        }
        finally
        {
            metrics.RecordFollowerRead(startTimestamp, success);
        }
    }
}
