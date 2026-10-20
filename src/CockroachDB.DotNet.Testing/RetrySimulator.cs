using CockroachDB.DotNet.Abstractions.Exceptions;

namespace CockroachDB.DotNet.Testing;

public static class RetrySimulator
{
    public static CockroachDBRetryableException CreateSerializationError(string? message = null)
    {
        return new CockroachDBRetryableException(
            sqlState: "40001",
            message: message ?? "restart transaction: TransactionRetryWithProtoRefreshError");
    }

    public static CockroachDBRetryableException CreateDeadlockError(string? message = null)
    {
        return new CockroachDBRetryableException(
            sqlState: "40P01",
            message: message ?? "deadlock detected");
    }

    public static CockroachDBRetryableException CreateRetryableError(
        string sqlState,
        string message)
    {
        return new CockroachDBRetryableException(sqlState, message);
    }
}
