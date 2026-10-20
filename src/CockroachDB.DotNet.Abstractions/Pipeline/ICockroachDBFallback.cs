namespace CockroachDB.DotNet.Abstractions.Pipeline;

public interface ICockroachDBFallback<T>
{
    Task<T> ExecuteAsync(CockroachDBContext context);
}
