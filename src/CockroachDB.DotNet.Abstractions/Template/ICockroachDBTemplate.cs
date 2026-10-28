namespace CockroachDB.DotNet.Abstractions.Template;

using CockroachDB.DotNet.Abstractions.Attributes;

public interface ICockroachDBTemplate
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation);

    Task ExecuteAsync(Func<Task> operation);

    Task<T> ExecuteReadCommittedAsync<T>(Func<Task<T>> operation);

    Task<T> ExecuteFollowerReadAsync<T>(Func<Task<T>> operation);

    Task ExecuteFollowerReadAsync(Func<Task> operation);

    /// <summary>Follower read with additive staleness composed into <c>AS OF SYSTEM TIME '-Ns'</c>.</summary>
    Task<T> ExecuteFollowerReadAsync<T>(Func<Task<T>> operation, FollowerReadStaleness staleness);

    /// <summary>Follower read with additive staleness composed into <c>AS OF SYSTEM TIME '-Ns'</c>.</summary>
    Task ExecuteFollowerReadAsync(Func<Task> operation, FollowerReadStaleness staleness);

    IRegionalExecution InRegion(string region);
}

public interface IRegionalExecution
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation);

    Task ExecuteAsync(Func<Task> operation);
}
