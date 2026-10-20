using CockroachDB.DotNet.Abstractions.Attributes;
using CockroachDB.DotNet.Abstractions.Configuration;
using CockroachDB.DotNet.Abstractions.Pipeline;
using CockroachDB.DotNet.Core.Middleware;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CockroachDB.DotNet.Tests.Middleware;

public sealed class CircuitBreakerMiddlewareTests
{
    private static IOptions<CockroachDBOptions> CreateOptions(int threshold = 2, int durationSec = 1) =>
        Options.Create(new CockroachDBOptions
        {
            CircuitBreaker = new CircuitBreakerOptions
            {
                FailureThreshold = threshold,
                DurationOfBreakSeconds = durationSec
            }
        });

    private static CircuitBreakerMiddleware CreateMiddleware(int threshold = 2, int durationSec = 1) =>
        new(CreateOptions(threshold, durationSec), NullLogger<CircuitBreakerMiddleware>.Instance);

    [Fact]
    public async Task Closed_Success_StaysClosed()
    {
        var middleware = CreateMiddleware();
        var context = new CockroachDBContext();

        var result = await middleware.ExecuteAsync(context, _ => Task.FromResult("ok"));
        Assert.Equal("ok", result);

        result = await middleware.ExecuteAsync(context, _ => Task.FromResult("still ok"));
        Assert.Equal("still ok", result);
    }

    [Fact]
    public async Task Closed_FailuresBelowThreshold_StaysClosed()
    {
        var middleware = CreateMiddleware(threshold: 3);
        var context = new CockroachDBContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.ExecuteAsync<string>(context, _ => throw new InvalidOperationException()));

        var result = await middleware.ExecuteAsync(context, _ => Task.FromResult("recovered"));
        Assert.Equal("recovered", result);
    }

    [Fact]
    public async Task Closed_FailuresReachThreshold_TripsToOpen()
    {
        var middleware = CreateMiddleware(threshold: 2);
        var context = new CockroachDBContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.ExecuteAsync<string>(context, _ => throw new InvalidOperationException()));

        await Assert.ThrowsAsync<CockroachDBCircuitBreakerOpenException>(() =>
            middleware.ExecuteAsync<string>(context, _ => throw new InvalidOperationException()));
    }

    [Fact]
    public async Task Open_BlocksExecution_ThrowsWithoutFallback()
    {
        var middleware = CreateMiddleware(threshold: 1);
        var context = new CockroachDBContext();

        await Assert.ThrowsAsync<CockroachDBCircuitBreakerOpenException>(() =>
            middleware.ExecuteAsync<string>(context, _ => throw new InvalidOperationException()));

        var executed = false;
        await Assert.ThrowsAsync<CockroachDBCircuitBreakerOpenException>(() =>
            middleware.ExecuteAsync<string>(context, _ =>
            {
                executed = true;
                return Task.FromResult("should not reach");
            }));

        Assert.False(executed);
    }

    [Fact]
    public async Task Open_ExecutesFallback_WhenConfigured()
    {
        var middleware = CreateMiddleware(threshold: 1);
        var context = new CockroachDBContext
        {
            FallbackAttribute = new CockroachDBFallbackAttribute
            {
                FallbackType = typeof(StringFallback),
                FailureThreshold = 1,
                DurationOfBreakSeconds = 1
            }
        };

        var result = await middleware.ExecuteAsync<string>(context,
            _ => throw new InvalidOperationException());

        Assert.Equal("fallback-value", result);
    }

    [Fact]
    public async Task Open_TransitionsToHalfOpen_AfterDuration()
    {
        var middleware = CreateMiddleware(threshold: 1, durationSec: 1);
        var context = new CockroachDBContext();

        await Assert.ThrowsAsync<CockroachDBCircuitBreakerOpenException>(() =>
            middleware.ExecuteAsync<string>(context, _ => throw new InvalidOperationException()));

        await Task.Delay(1100);

        var result = await middleware.ExecuteAsync(context, _ => Task.FromResult("probe ok"));
        Assert.Equal("probe ok", result);
    }

    [Fact]
    public async Task HalfOpen_Success_ClosesCircuit()
    {
        var middleware = CreateMiddleware(threshold: 1, durationSec: 1);
        var context = new CockroachDBContext();

        await Assert.ThrowsAsync<CockroachDBCircuitBreakerOpenException>(() =>
            middleware.ExecuteAsync<string>(context, _ => throw new InvalidOperationException()));

        await Task.Delay(1100);

        await middleware.ExecuteAsync(context, _ => Task.FromResult("probe ok"));

        var result = await middleware.ExecuteAsync(context, _ => Task.FromResult("fully closed"));
        Assert.Equal("fully closed", result);
    }

    private sealed class StringFallback : ICockroachDBFallback<string>
    {
        public Task<string> ExecuteAsync(CockroachDBContext context) =>
            Task.FromResult("fallback-value");
    }
}
