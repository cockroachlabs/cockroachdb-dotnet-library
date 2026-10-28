using CockroachDB.DotNet.Abstractions.Attributes;
using CockroachDB.DotNet.Abstractions.Pipeline;
using CockroachDB.DotNet.Abstractions.Template;

namespace CockroachDB.DotNet.Core.Template;

public sealed class CockroachDBTemplate(ICockroachDBExecutor executor) : ICockroachDBTemplate
{
    public Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        return executor.ExecuteAsync(operation);
    }

    public Task ExecuteAsync(Func<Task> operation)
    {
        return executor.ExecuteAsync(operation);
    }

    public Task<T> ExecuteReadCommittedAsync<T>(Func<Task<T>> operation)
    {
        return executor.ExecuteAsync(operation, new CockroachDBAttribute
        {
            Isolation = TransactionIsolation.ReadCommitted
        });
    }

    public Task<T> ExecuteFollowerReadAsync<T>(Func<Task<T>> operation)
        => ExecuteFollowerReadAsync(operation, default);

    public Task ExecuteFollowerReadAsync(Func<Task> operation)
        => ExecuteFollowerReadAsync(operation, default);

    public Task<T> ExecuteFollowerReadAsync<T>(Func<Task<T>> operation, FollowerReadStaleness staleness)
    {
        return executor.ExecuteAsync(operation, ctx =>
        {
            ctx.UseFollowerRead = true;
            ctx.ReadOnly = true;
            ctx.Staleness = staleness;
        });
    }

    public Task ExecuteFollowerReadAsync(Func<Task> operation, FollowerReadStaleness staleness)
    {
        return executor.ExecuteAsync(operation, ctx =>
        {
            ctx.UseFollowerRead = true;
            ctx.ReadOnly = true;
            ctx.Staleness = staleness;
        });
    }

    public IRegionalExecution InRegion(string region)
    {
        return new RegionalExecution(executor, region);
    }

    private sealed class RegionalExecution(ICockroachDBExecutor executor, string region) : IRegionalExecution
    {
        public Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            return executor.ExecuteAsync(operation, new CockroachDBAttribute { Region = region });
        }

        public Task ExecuteAsync(Func<Task> operation)
        {
            return executor.ExecuteAsync(operation, new CockroachDBAttribute { Region = region });
        }
    }
}
