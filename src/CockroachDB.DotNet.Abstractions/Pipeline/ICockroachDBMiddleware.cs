namespace CockroachDB.DotNet.Abstractions.Pipeline;

public interface ICockroachDBMiddleware
{
    Task<T> ExecuteAsync<T>(
        CockroachDBContext context,
        Func<CockroachDBContext, Task<T>> next);
}
