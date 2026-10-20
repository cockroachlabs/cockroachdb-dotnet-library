using CockroachDB.DotNet.Abstractions.Exceptions;
using CockroachDB.DotNet.Core.Exceptions;
using CockroachDB.DotNet.Testing;
using Npgsql;

namespace CockroachDB.DotNet.Tests.Exceptions;

public sealed class NpgsqlExceptionClassifierTests
{
    private readonly IExceptionClassifier _classifier = new NpgsqlExceptionClassifier();

    [Fact]
    public void CockroachDBRetryable_40001_IsSerializationError()
    {
        var ex = new CockroachDBRetryableException("40001", "serialization failure");
        Assert.True(_classifier.IsSerializationError(ex));
    }

    [Fact]
    public void CockroachDBRetryable_40P01_IsDeadlock()
    {
        var ex = new CockroachDBRetryableException("40P01", "deadlock detected");
        Assert.True(_classifier.IsDeadlock(ex));
    }

    [Fact]
    public void CockroachDBRetryable_40001_IsRetryable()
    {
        var ex = new CockroachDBRetryableException("40001", "conflict");
        Assert.True(_classifier.IsRetryable(ex));
    }

    [Fact]
    public void RegularException_IsNotRetryable()
    {
        var ex = new InvalidOperationException("nope");
        Assert.False(_classifier.IsRetryable(ex));
    }

    [Fact]
    public void NestedCockroachDBRetryable_ExtractsSqlState()
    {
        var inner = new CockroachDBRetryableException("40001", "conflict");
        var outer = new InvalidOperationException("wrapper", inner);

        Assert.Equal("40001", _classifier.ExtractSqlState(outer));
    }

    [Fact]
    public void PostgresException_40001_IsSerializationError()
    {
        var pgEx = new PostgresException("conflict", "ERROR", "ERROR", "40001");
        Assert.True(_classifier.IsSerializationError(pgEx));
    }

    [Fact]
    public void PostgresException_40P01_IsDeadlock()
    {
        var pgEx = new PostgresException("deadlock", "ERROR", "ERROR", "40P01");
        Assert.True(_classifier.IsDeadlock(pgEx));
    }

    [Fact]
    public void NullException_ReturnsNullSqlState()
    {
        Assert.Null(_classifier.ExtractSqlState(null!));
    }
}

public sealed class TestExceptionClassifierTests
{
    private readonly TestExceptionClassifier _classifier = new();

    [Fact]
    public void Identifies_SerializationError()
    {
        var ex = RetrySimulator.CreateSerializationError();
        Assert.True(_classifier.IsSerializationError(ex));
        Assert.False(_classifier.IsDeadlock(ex));
    }

    [Fact]
    public void Identifies_Deadlock()
    {
        var ex = RetrySimulator.CreateDeadlockError();
        Assert.True(_classifier.IsDeadlock(ex));
        Assert.False(_classifier.IsSerializationError(ex));
    }

    [Fact]
    public void RegularException_NotRetryable()
    {
        var ex = new Exception("generic");
        Assert.False(_classifier.IsSerializationError(ex));
        Assert.False(_classifier.IsDeadlock(ex));
    }

    [Fact]
    public void ExtractsSqlState_FromCockroachDBRetryable()
    {
        var ex = RetrySimulator.CreateRetryableError("42000", "syntax error");
        Assert.Equal("42000", _classifier.ExtractSqlState(ex));
    }
}
