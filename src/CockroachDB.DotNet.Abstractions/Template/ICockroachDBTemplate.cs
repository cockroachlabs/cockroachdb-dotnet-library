namespace CockroachDB.DotNet.Abstractions.Template;

public interface ICockroachDBTemplate
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation);

    Task ExecuteAsync(Func<Task> operation);

    Task<T> ExecuteReadCommittedAsync<T>(Func<Task<T>> operation);

    Task<T> ExecuteFollowerReadAsync<T>(Func<Task<T>> operation);

    Task ExecuteFollowerReadAsync(Func<Task> operation);

    IRegionalExecution InRegion(string region);
}

public interface IRegionalExecution
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation);

    Task ExecuteAsync(Func<Task> operation);
}
