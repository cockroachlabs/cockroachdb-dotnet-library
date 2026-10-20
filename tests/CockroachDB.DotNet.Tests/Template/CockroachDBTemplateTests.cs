using CockroachDB.DotNet.Abstractions.Attributes;
using CockroachDB.DotNet.Abstractions.Pipeline;
using CockroachDB.DotNet.Core.Template;
using NSubstitute;

namespace CockroachDB.DotNet.Tests.Template;

public sealed class CockroachDBTemplateTests
{
    private readonly ICockroachDBExecutor _executor = Substitute.For<ICockroachDBExecutor>();

    [Fact]
    public async Task ExecuteAsync_DelegatesToExecutor()
    {
        _executor.ExecuteAsync(Arg.Any<Func<Task<int>>>(), Arg.Any<CockroachDBAttribute?>())
            .Returns(42);

        var template = new CockroachDBTemplate(_executor);
        var result = await template.ExecuteAsync(() => Task.FromResult(42));

        Assert.Equal(42, result);
        await _executor.Received(1).ExecuteAsync(Arg.Any<Func<Task<int>>>(), (CockroachDBAttribute?)null);
    }

    [Fact]
    public async Task ExecuteReadCommitted_SetsIsolation()
    {
        _executor.ExecuteAsync(Arg.Any<Func<Task<int>>>(), Arg.Any<CockroachDBAttribute?>())
            .Returns(1);

        var template = new CockroachDBTemplate(_executor);
        await template.ExecuteReadCommittedAsync(() => Task.FromResult(1));

        await _executor.Received(1).ExecuteAsync(
            Arg.Any<Func<Task<int>>>(),
            Arg.Is<CockroachDBAttribute?>(a =>
                a != null && a.Isolation == TransactionIsolation.ReadCommitted));
    }

    [Fact]
    public async Task InRegion_SetsRegionOnAttribute()
    {
        _executor.ExecuteAsync(Arg.Any<Func<Task<int>>>(), Arg.Any<CockroachDBAttribute?>())
            .Returns(1);

        var template = new CockroachDBTemplate(_executor);
        await template.InRegion("us-east1").ExecuteAsync(() => Task.FromResult(1));

        await _executor.Received(1).ExecuteAsync(
            Arg.Any<Func<Task<int>>>(),
            Arg.Is<CockroachDBAttribute?>(a =>
                a != null && a.Region == "us-east1"));
    }

    [Fact]
    public async Task ExecuteFollowerRead_SetsContextFlags()
    {
        CockroachDBContext? capturedContext = null;

        _executor.ExecuteAsync(
                Arg.Any<Func<Task<int>>>(),
                Arg.Any<Action<CockroachDBContext>>())
            .Returns(callInfo =>
            {
                var ctx = new CockroachDBContext();
                callInfo.ArgAt<Action<CockroachDBContext>>(1)(ctx);
                capturedContext = ctx;
                return 1;
            });

        var template = new CockroachDBTemplate(_executor);
        await template.ExecuteFollowerReadAsync(() => Task.FromResult(1));

        Assert.NotNull(capturedContext);
        Assert.True(capturedContext!.UseFollowerRead);
        Assert.True(capturedContext.ReadOnly);
    }

    [Fact]
    public async Task ExecuteFollowerRead_VoidOverload_SetsContextFlags()
    {
        CockroachDBContext? capturedContext = null;

        _executor.ExecuteAsync(
                Arg.Any<Func<Task>>(),
                Arg.Any<Action<CockroachDBContext>>())
            .Returns(callInfo =>
            {
                var ctx = new CockroachDBContext();
                callInfo.ArgAt<Action<CockroachDBContext>>(1)(ctx);
                capturedContext = ctx;
                return Task.CompletedTask;
            });

        var template = new CockroachDBTemplate(_executor);
        await template.ExecuteFollowerReadAsync(() => Task.CompletedTask);

        Assert.NotNull(capturedContext);
        Assert.True(capturedContext!.UseFollowerRead);
        Assert.True(capturedContext.ReadOnly);
    }
}
