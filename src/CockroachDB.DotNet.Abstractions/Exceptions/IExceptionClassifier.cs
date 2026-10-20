namespace CockroachDB.DotNet.Abstractions.Exceptions;

public interface IExceptionClassifier
{
    bool IsSerializationError(Exception ex);

    bool IsDeadlock(Exception ex);

    bool IsRetryable(Exception ex) => IsSerializationError(ex) || IsDeadlock(ex);

    string? ExtractSqlState(Exception ex);
}
