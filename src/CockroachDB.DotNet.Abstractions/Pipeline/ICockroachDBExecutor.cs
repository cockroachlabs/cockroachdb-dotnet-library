using CockroachDB.DotNet.Abstractions.Attributes;

namespace CockroachDB.DotNet.Abstractions.Pipeline;

public interface ICockroachDBExecutor
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CockroachDBAttribute? attribute = null);

    Task ExecuteAsync(Func<Task> operation, CockroachDBAttribute? attribute = null);

    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, Action<CockroachDBContext> configureContext);

    Task ExecuteAsync(Func<Task> operation, Action<CockroachDBContext> configureContext);
}
