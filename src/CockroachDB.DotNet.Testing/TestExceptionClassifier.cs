using CockroachDB.DotNet.Abstractions.Exceptions;

namespace CockroachDB.DotNet.Testing;

public sealed class TestExceptionClassifier : IExceptionClassifier
{
    public bool IsSerializationError(Exception ex) =>
        ExtractSqlState(ex) == "40001";

    public bool IsDeadlock(Exception ex) =>
        ExtractSqlState(ex) == "40P01";

    public string? ExtractSqlState(Exception? ex)
    {
        while (ex is not null)
        {
            if (ex is CockroachDBRetryableException crdbEx)
                return crdbEx.SqlState;

            ex = ex.InnerException;
        }

        return null;
    }
}
