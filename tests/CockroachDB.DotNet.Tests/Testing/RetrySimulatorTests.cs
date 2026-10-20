using CockroachDB.DotNet.Abstractions.Exceptions;
using CockroachDB.DotNet.Testing;

namespace CockroachDB.DotNet.Tests.Testing;

public sealed class RetrySimulatorTests
{
    [Fact]
    public void CreateSerializationError_SqlState40001()
    {
        var ex = RetrySimulator.CreateSerializationError();
        Assert.Equal("40001", ex.SqlState);
        Assert.Contains("TransactionRetryWithProtoRefreshError", ex.Message);
    }

    [Fact]
    public void CreateSerializationError_CustomMessage()
    {
        var ex = RetrySimulator.CreateSerializationError("custom");
        Assert.Equal("40001", ex.SqlState);
        Assert.Equal("custom", ex.Message);
    }

    [Fact]
    public void CreateDeadlockError_SqlState40P01()
    {
        var ex = RetrySimulator.CreateDeadlockError();
        Assert.Equal("40P01", ex.SqlState);
        Assert.Contains("deadlock", ex.Message);
    }

    [Fact]
    public void CreateRetryableError_ArbitrarySqlState()
    {
        var ex = RetrySimulator.CreateRetryableError("08006", "connection failure");
        Assert.Equal("08006", ex.SqlState);
        Assert.Equal("connection failure", ex.Message);
    }

    [Fact]
    public void AllErrors_AreCockroachDBRetryableException()
    {
        Assert.IsType<CockroachDBRetryableException>(RetrySimulator.CreateSerializationError());
        Assert.IsType<CockroachDBRetryableException>(RetrySimulator.CreateDeadlockError());
        Assert.IsType<CockroachDBRetryableException>(RetrySimulator.CreateRetryableError("X", "Y"));
    }
}
