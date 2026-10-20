using System.Diagnostics;
using CockroachDB.DotNet.Abstractions.Metrics;
using CockroachDB.DotNet.Abstractions.Pipeline;

namespace CockroachDB.DotNet.Core.Middleware;

public sealed class MetricsMiddleware(
    ICockroachDBMetrics metrics) : ICockroachDBMiddleware
{
    public async Task<T> ExecuteAsync<T>(
        CockroachDBContext context,
        Func<CockroachDBContext, Task<T>> next)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var success = false;

        try
        {
            var result = await next(context);
            success = true;
            return result;
        }
        finally
        {
            metrics.RecordTransaction(startTimestamp, context.AttemptNumber, success);
        }
    }
}
