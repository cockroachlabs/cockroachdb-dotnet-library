using CockroachDB.DotNet.Abstractions.Attributes;
using CockroachDB.DotNet.Abstractions.Metrics;
using CockroachDB.DotNet.Abstractions.Pipeline;
using CockroachDB.DotNet.Core.FollowerRead;
using CockroachDB.DotNet.Core.Middleware;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CockroachDB.DotNet.Tests.Middleware;

public sealed class FollowerReadMiddlewareTests
{
    private readonly ICockroachDBMetrics _metrics = Substitute.For<ICockroachDBMetrics>();

    private FollowerReadMiddleware CreateMiddleware()
        => new(_metrics, NullLogger<FollowerReadMiddleware>.Instance);

    [Fact]
    public async Task Bypasses_WhenFollowerReadIsFalse()
    {
        var middleware = CreateMiddleware();

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

    [Fact]
    public async Task Active_BindsFollowerReadContext_WhileOperationRuns_AndRecordsSuccess()
    {
        var middleware = CreateMiddleware();
        var context = new CockroachDBContext { UseFollowerRead = true };

        bool boundDuringOperation = false;
        string? clauseDuringOperation = null;

        var result = await middleware.ExecuteAsync(context, _ =>
        {
            boundDuringOperation = FollowerReadContext.Current;
            clauseDuringOperation = FollowerReadContext.CurrentStaleness.ToAsOfSystemTime();
            return Task.FromResult(7);
        });

        Assert.Equal(7, result);
        Assert.True(boundDuringOperation);
        Assert.Equal("follower_read_timestamp()", clauseDuringOperation);
        Assert.True(context.ReadOnly);
        Assert.False(FollowerReadContext.Current); // restored on scope dispose
        _metrics.Received(1).RecordFollowerRead(Arg.Any<long>(), true);
    }

    [Fact]
    public async Task Active_WithStaleness_BindsComposedClause()
    {
        var middleware = CreateMiddleware();
        var context = new CockroachDBContext
        {
            UseFollowerRead = true,
            Staleness = new FollowerReadStaleness(HoursAgo: 0, MinutesAgo: 0, SecondsAgo: 30)
        };

        string? clauseDuringOperation = null;

        await middleware.ExecuteAsync(context, _ =>
        {
            clauseDuringOperation = FollowerReadContext.CurrentStaleness.ToAsOfSystemTime();
            return Task.FromResult(1);
        });

        Assert.Equal("'-30s'", clauseDuringOperation);
        _metrics.Received(1).RecordFollowerRead(Arg.Any<long>(), true);
    }

    [Fact]
    public async Task Active_NegativeStaleness_FailsFast_WithoutRunningOperation_AndRecordsFailure()
    {
        var middleware = CreateMiddleware();
        var context = new CockroachDBContext
        {
            UseFollowerRead = true,
            Staleness = new FollowerReadStaleness(SecondsAgo: -1)
        };

        var called = false;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            middleware.ExecuteAsync(context, _ =>
            {
                called = true;
                return Task.FromResult(1);
            }));

        Assert.False(called);
        Assert.False(FollowerReadContext.Current);
        _metrics.Received(1).RecordFollowerRead(Arg.Any<long>(), false);
    }
}
