using System.Diagnostics;
using CockroachDB.DotNet.Abstractions.Metrics;
using CockroachDB.DotNet.Abstractions.Pipeline;
using CockroachDB.DotNet.Core.FollowerRead;
using Microsoft.Extensions.Logging;

namespace CockroachDB.DotNet.Core.Middleware;

public sealed class FollowerReadMiddleware(
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
            // Validate up front so a negative staleness FAILS FAST (metered as a failure
            // by the finally) rather than silently serving a current read.
            var clause = context.Staleness.ToAsOfSystemTime();

            context.ReadOnly = true;

            // Bind the staleness to the connection that actually runs the operation's
            // queries. The EF command interceptor reads this AsyncLocal and prefixes
            // SET TRANSACTION AS OF SYSTEM TIME onto each command on its OWN connection.
            // The previous design opened a throwaway connection, ran SET on it, then
            // abandoned it while the operation read from a different pooled connection —
            // a silent current read. See the port notes for CockroachDB Spring PR #10.
            using (FollowerReadContext.Scoped(context.Staleness))
            {
                logger.LogDebug("CockroachDB follower read activated (AS OF SYSTEM TIME {Clause})", clause);

                var result = await next(context);
                success = true;
                return result;
            }
        }
        finally
        {
            metrics.RecordFollowerRead(startTimestamp, success);
        }
    }
}
