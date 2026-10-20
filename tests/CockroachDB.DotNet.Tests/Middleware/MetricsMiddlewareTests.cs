using CockroachDB.DotNet.Abstractions.Metrics;
using CockroachDB.DotNet.Abstractions.Pipeline;
using CockroachDB.DotNet.Core.Middleware;
using NSubstitute;

namespace CockroachDB.DotNet.Tests.Middleware;

public sealed class MetricsMiddlewareTests
{
    private readonly ICockroachDBMetrics _metrics = Substitute.For<ICockroachDBMetrics>();

    [Fact]
    public async Task Success_RecordsWithSuccessFlag()
    {
        var middleware = new MetricsMiddleware(_metrics);
        var context = new CockroachDBContext();

        await middleware.ExecuteAsync(context, _ => Task.FromResult(42));

        _metrics.Received(1).RecordTransaction(Arg.Any<long>(), Arg.Any<int>(), true);
    }

    [Fact]
    public async Task Failure_RecordsWithFailureFlag()
    {
        var middleware = new MetricsMiddleware(_metrics);
        var context = new CockroachDBContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.ExecuteAsync<int>(context, _ => throw new InvalidOperationException()));

        _metrics.Received(1).RecordTransaction(Arg.Any<long>(), Arg.Any<int>(), false);
    }

    [Fact]
    public async Task PassesThroughReturnValue()
    {
        var middleware = new MetricsMiddleware(_metrics);
        var context = new CockroachDBContext();

        var result = await middleware.ExecuteAsync(context, _ => Task.FromResult("hello"));

        Assert.Equal("hello", result);
    }
}
