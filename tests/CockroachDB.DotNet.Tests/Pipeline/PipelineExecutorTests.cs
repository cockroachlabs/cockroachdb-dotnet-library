using CockroachDB.DotNet.Abstractions.Attributes;
using CockroachDB.DotNet.Abstractions.Pipeline;
using CockroachDB.DotNet.Core.Pipeline;

namespace CockroachDB.DotNet.Tests.Pipeline;

public sealed class PipelineExecutorTests
{
    [Fact]
    public async Task EmptyPipeline_ExecutesOperationDirectly()
    {
        var executor = new CockroachDBPipelineExecutor([]);

        var result = await executor.ExecuteAsync(() => Task.FromResult(42));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task SingleMiddleware_WrapsOperation()
    {
        var log = new List<string>();
        var middleware = new LoggingMiddleware(log, "A");
        var executor = new CockroachDBPipelineExecutor([middleware]);

        var result = await executor.ExecuteAsync(() =>
        {
            log.Add("operation");
            return Task.FromResult(1);
        });

        Assert.Equal(1, result);
        Assert.Equal(["A-before", "operation", "A-after"], log);
    }

    [Fact]
    public async Task MultipleMiddleware_ChainsInOrder()
    {
        var log = new List<string>();
        var executor = new CockroachDBPipelineExecutor([
            new LoggingMiddleware(log, "A"),
            new LoggingMiddleware(log, "B"),
            new LoggingMiddleware(log, "C")
        ]);

        await executor.ExecuteAsync(() =>
        {
            log.Add("operation");
            return Task.FromResult(0);
        });

        Assert.Equal(["A-before", "B-before", "C-before", "operation", "C-after", "B-after", "A-after"], log);
    }

    [Fact]
    public async Task VoidOperation_Executes()
    {
        var executed = false;
        var executor = new CockroachDBPipelineExecutor([]);

        await executor.ExecuteAsync(() => { executed = true; return Task.CompletedTask; });

        Assert.True(executed);
    }

    [Fact]
    public async Task Attribute_IsPassedToContext()
    {
        CockroachDBContext? captured = null;
        var middleware = new CapturingMiddleware(ctx => captured = ctx);
        var executor = new CockroachDBPipelineExecutor([middleware]);
        var attr = new CockroachDBAttribute { MaxRetries = 7, Region = "us-east1" };

        await executor.ExecuteAsync(() => Task.FromResult(0), attr);

        Assert.NotNull(captured);
        Assert.Equal(7, captured!.Attribute!.MaxRetries);
        Assert.Equal("us-east1", captured.Region);
    }

    private sealed class LoggingMiddleware(List<string> log, string name) : ICockroachDBMiddleware
    {
        public async Task<T> ExecuteAsync<T>(CockroachDBContext context, Func<CockroachDBContext, Task<T>> next)
        {
            log.Add($"{name}-before");
            var result = await next(context);
            log.Add($"{name}-after");
            return result;
        }
    }

    private sealed class CapturingMiddleware(Action<CockroachDBContext> capture) : ICockroachDBMiddleware
    {
        public Task<T> ExecuteAsync<T>(CockroachDBContext context, Func<CockroachDBContext, Task<T>> next)
        {
            capture(context);
            return next(context);
        }
    }
}
