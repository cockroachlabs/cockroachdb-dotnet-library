using CockroachDB.DotNet.Abstractions.Attributes;
using CockroachDB.DotNet.Abstractions.Configuration;
using CockroachDB.DotNet.Abstractions.Exceptions;
using CockroachDB.DotNet.Abstractions.Pipeline;
using CockroachDB.DotNet.Core.Middleware;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CockroachDB.DotNet.Tests.Middleware;

public sealed class RetryMiddlewareTests
{
    private readonly IExceptionClassifier _classifier = Substitute.For<IExceptionClassifier>();

    private readonly IOptions<CockroachDBOptions> _options = Options.Create(new CockroachDBOptions
    {
        Retry = new RetryOptions
        {
            MaxRetries = 3, DelayMs = 1, Multiplier = 1.0, MaxDelayMs = 1, JitterMs = 0
        }
    });

    private RetryMiddleware CreateMiddleware() =>
        new(_options, _classifier, NullLogger<RetryMiddleware>.Instance);

    [Fact]
    public async Task SucceedsOnFirstAttempt_NoRetry()
    {
        var middleware = CreateMiddleware();
        var context = new CockroachDBContext();
        var result = await middleware.ExecuteAsync(context, _ => Task.FromResult(42));
        Assert.Equal(42, result);
        Assert.Equal(0, context.AttemptNumber);
    }

    [Fact]
    public async Task RetriesOnRetryableError_SucceedsOnSecondAttempt()
    {
        var retryable = new CockroachDBRetryableException("40001", "serialization failure");
        _classifier.IsRetryable(retryable).Returns(true);
        _classifier.ExtractSqlState(retryable).Returns("40001");

        var middleware = CreateMiddleware();
        var context = new CockroachDBContext();
        var callCount = 0;

        var result = await middleware.ExecuteAsync(context, _ =>
        {
            callCount++;
            if (callCount == 1) throw retryable;
            return Task.FromResult("ok");
        });

        Assert.Equal("ok", result);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task ExhaustsRetries_ThrowsOriginalException()
    {
        var retryable = new CockroachDBRetryableException("40001", "serialization failure");
        _classifier.IsRetryable(retryable).Returns(true);
        _classifier.ExtractSqlState(retryable).Returns("40001");

        var middleware = CreateMiddleware();
        var context = new CockroachDBContext();
        var callCount = 0;

        var ex = await Assert.ThrowsAsync<CockroachDBRetryableException>(() =>
            middleware.ExecuteAsync<string>(context, _ => { callCount++; throw retryable; }));

        Assert.Same(retryable, ex);
        Assert.Equal(4, callCount);
    }

    [Fact]
    public async Task NonRetryableException_ThrowsImmediately()
    {
        var nonRetryable = new InvalidOperationException("bad state");
        _classifier.IsRetryable(nonRetryable).Returns(false);

        var middleware = CreateMiddleware();
        var context = new CockroachDBContext();
        var callCount = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.ExecuteAsync<string>(context, _ => { callCount++; throw nonRetryable; }));

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task RespectsAttributeOverrides_MaxRetries()
    {
        var retryable = new CockroachDBRetryableException("40001", "conflict");
        _classifier.IsRetryable(retryable).Returns(true);
        _classifier.ExtractSqlState(retryable).Returns("40001");

        var middleware = CreateMiddleware();
        var context = new CockroachDBContext
        {
            Attribute = new CockroachDBAttribute { MaxRetries = 1, DelayMs = 1 }
        };
        var callCount = 0;

        await Assert.ThrowsAsync<CockroachDBRetryableException>(() =>
            middleware.ExecuteAsync<string>(context, _ => { callCount++; throw retryable; }));

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task AttemptNumber_IncrementsOnEachRetry()
    {
        var retryable = new CockroachDBRetryableException("40001", "conflict");
        _classifier.IsRetryable(retryable).Returns(true);
        _classifier.ExtractSqlState(retryable).Returns("40001");

        var middleware = CreateMiddleware();
        var context = new CockroachDBContext();
        var attempts = new List<int>();

        await Assert.ThrowsAsync<CockroachDBRetryableException>(() =>
            middleware.ExecuteAsync<string>(context, ctx =>
            {
                attempts.Add(ctx.AttemptNumber);
                throw retryable;
            }));

        Assert.Equal([0, 1, 2, 3], attempts);
    }
}
