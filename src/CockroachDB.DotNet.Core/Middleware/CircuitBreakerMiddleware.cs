using CockroachDB.DotNet.Abstractions.Configuration;
using CockroachDB.DotNet.Abstractions.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CockroachDB.DotNet.Core.Middleware;

public sealed class CircuitBreakerMiddleware(
    IOptions<CockroachDBOptions> options,
    ILogger<CircuitBreakerMiddleware> logger) : ICockroachDBMiddleware
{
    private CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private DateTime _openedAt;
    private readonly object _lock = new();

    public async Task<T> ExecuteAsync<T>(
        CockroachDBContext context,
        Func<CockroachDBContext, Task<T>> next)
    {
        var opts = context.FallbackAttribute is { } fb
            ? new CircuitBreakerOptions
            {
                FailureThreshold = fb.FailureThreshold,
                DurationOfBreakSeconds = fb.DurationOfBreakSeconds
            }
            : options.Value.CircuitBreaker;

        if (ShouldBlock(opts))
        {
            logger.LogWarning("CockroachDB circuit breaker is OPEN. Checking for fallback.");
            return await ExecuteFallbackOrThrow<T>(context);
        }

        try
        {
            var result = await next(context);
            OnSuccess();
            return result;
        }
        catch (Exception ex)
        {
            OnFailure(opts);

            if (_state == CircuitState.Open)
            {
                logger.LogError(ex,
                    "CockroachDB circuit breaker tripped to OPEN after {Count} failures.",
                    _failureCount);

                return await ExecuteFallbackOrThrow<T>(context);
            }

            throw;
        }
    }

    private bool ShouldBlock(CircuitBreakerOptions opts)
    {
        lock (_lock)
        {
            if (_state == CircuitState.Closed)
                return false;

            if (_state == CircuitState.Open &&
                DateTime.UtcNow - _openedAt > TimeSpan.FromSeconds(opts.DurationOfBreakSeconds))
            {
                _state = CircuitState.HalfOpen;
                logger.LogInformation("CockroachDB circuit breaker transitioning to HALF-OPEN.");
                return false;
            }

            return _state == CircuitState.Open;
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Closed;
                logger.LogInformation("CockroachDB circuit breaker CLOSED after successful probe.");
            }
        }
    }

    private void OnFailure(CircuitBreakerOptions opts)
    {
        lock (_lock)
        {
            _failureCount++;

            if (_failureCount >= opts.FailureThreshold)
            {
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
            }
        }
    }

    private static async Task<T> ExecuteFallbackOrThrow<T>(CockroachDBContext context)
    {
        if (context.FallbackAttribute?.FallbackType is { } fallbackType &&
            typeof(ICockroachDBFallback<T>).IsAssignableFrom(fallbackType))
        {
            var fallback = (ICockroachDBFallback<T>)Activator.CreateInstance(fallbackType)!;
            return await fallback.ExecuteAsync(context);
        }

        throw new CockroachDBCircuitBreakerOpenException(
            "CockroachDB circuit breaker is open and no fallback is configured.");
    }

    private enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }
}

public sealed class CockroachDBCircuitBreakerOpenException(string message)
    : Exception(message);
