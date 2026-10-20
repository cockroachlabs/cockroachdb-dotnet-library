using CockroachDB.DotNet.Abstractions.Configuration;
using CockroachDB.DotNet.Abstractions.Exceptions;
using CockroachDB.DotNet.Abstractions.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CockroachDB.DotNet.Core.Middleware;

public sealed class RetryMiddleware(
    IOptions<CockroachDBOptions> options,
    IExceptionClassifier exceptionClassifier,
    ILogger<RetryMiddleware> logger) : ICockroachDBMiddleware
{
    public async Task<T> ExecuteAsync<T>(
        CockroachDBContext context,
        Func<CockroachDBContext, Task<T>> next)
    {
        var opts = options.Value.Retry;

        var maxRetries = context.Attribute?.MaxRetries is > 0
            ? context.Attribute.MaxRetries
            : opts.MaxRetries;

        long currentDelay = context.Attribute?.DelayMs is > 0
            ? context.Attribute.DelayMs
            : opts.DelayMs;

        var multiplier = context.Attribute?.Multiplier is > 0
            ? context.Attribute.Multiplier
            : opts.Multiplier;

        var maxDelay = context.Attribute?.MaxDelayMs is > 0
            ? context.Attribute.MaxDelayMs
            : opts.MaxDelayMs;

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                context.AttemptNumber = attempt;
                return await next(context);
            }
            catch (Exception ex) when (attempt < maxRetries && exceptionClassifier.IsRetryable(ex))
            {
                var sqlState = exceptionClassifier.ExtractSqlState(ex) ?? "unknown";
                var jitter = Random.Shared.NextInt64(0, opts.JitterMs);
                var delay = Math.Min(currentDelay + jitter, maxDelay);

                logger.LogWarning(
                    "CockroachDB retryable error (SQLSTATE {SqlState}), attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms.",
                    sqlState, attempt + 1, maxRetries, delay);

                await Task.Delay(TimeSpan.FromMilliseconds(delay));
                currentDelay = (long)Math.Min(currentDelay * multiplier, maxDelay);
            }
        }
    }
}
