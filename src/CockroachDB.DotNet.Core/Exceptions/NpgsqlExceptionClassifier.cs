using CockroachDB.DotNet.Abstractions.Exceptions;
using Npgsql;

namespace CockroachDB.DotNet.Core.Exceptions;

public sealed class NpgsqlExceptionClassifier : IExceptionClassifier
{
    public bool IsSerializationError(Exception ex) =>
        ExtractSqlState(ex) == "40001";

    public bool IsDeadlock(Exception ex) =>
        ExtractSqlState(ex) == "40P01";

    public string? ExtractSqlState(Exception? ex)
    {
        while (ex is not null)
        {
            switch (ex)
            {
                case PostgresException pgEx:
                    return pgEx.SqlState;
                case CockroachDBRetryableException crdbEx:
                    return crdbEx.SqlState;
            }

            ex = ex.InnerException;
        }

        return null;
    }
}
