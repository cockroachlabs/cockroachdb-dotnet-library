using CockroachDB.DotNet.Abstractions.Metrics;
using CockroachDB.DotNet.Abstractions.Pipeline;
using CockroachDB.DotNet.Core.Middleware;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CockroachDB.DotNet.Tests.Middleware;

public sealed class FollowerReadMiddlewareTests
{
    private readonly ICockroachDBMetrics _metrics = Substitute.For<ICockroachDBMetrics>();

    [Fact]
    public async Task Bypasses_WhenFollowerReadIsFalse()
    {
        var middleware = new FollowerReadMiddleware(
            null!, _metrics, NullLogger<FollowerReadMiddleware>.Instance);

        var context = new CockroachDBContext { UseFollowerRead = false };
        var called = false;

        await middleware.ExecuteAsync(context, _ =>
        {
            called = true;
            return Task.FromResult(42);
        });

        Assert.True(called);
        _metrics.DidNotReceive().RecordFollowerRead(Arg.Any<long>(), Arg.Any<bool>());
    }

    [Fact]
    public void FollowerReadSql_ContainsCorrectStatement()
    {
        Assert.Equal(
            "SET TRANSACTION AS OF SYSTEM TIME follower_read_timestamp()",
            FollowerReadMiddleware.FollowerReadSql);
    }

    [Fact]
    public void Bypass_DoesNotSetReadOnly()
    {
        var context = new CockroachDBContext { UseFollowerRead = false };
        Assert.False(context.ReadOnly);
    }
}
