namespace CockroachDB.DotNet.Abstractions.Exceptions;

public class CockroachDBRetryableException(
    string sqlState,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string SqlState { get; } = sqlState;
}
