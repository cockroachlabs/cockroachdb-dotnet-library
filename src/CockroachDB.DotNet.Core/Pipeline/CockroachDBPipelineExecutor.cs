using CockroachDB.DotNet.Abstractions.Attributes;
using CockroachDB.DotNet.Abstractions.Pipeline;

namespace CockroachDB.DotNet.Core.Pipeline;

public sealed class CockroachDBPipelineExecutor(
    IEnumerable<ICockroachDBMiddleware> middlewares) : ICockroachDBExecutor
{
    private readonly IReadOnlyList<ICockroachDBMiddleware> _middlewares = middlewares.ToList();

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CockroachDBAttribute? attribute = null)
    {
        var context = new CockroachDBContext
        {
            Attribute = attribute,
            Isolation = attribute?.Isolation ?? TransactionIsolation.Serializable,
            ReadOnly = attribute?.ReadOnly ?? false,
            Region = attribute?.Region is { Length: > 0 } r ? r : null
        };

        return await ExecutePipelineAsync(context, 0, operation);
    }

    public async Task ExecuteAsync(Func<Task> operation, CockroachDBAttribute? attribute = null)
    {
        await ExecuteAsync(async () =>
        {
            await operation();
            return true;
        }, attribute);
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, Action<CockroachDBContext> configureContext)
    {
        var context = new CockroachDBContext();
        configureContext(context);
        return await ExecutePipelineAsync(context, 0, operation);
    }

    public async Task ExecuteAsync(Func<Task> operation, Action<CockroachDBContext> configureContext)
    {
        await ExecuteAsync(async () =>
        {
            await operation();
            return true;
        }, configureContext);
    }

    private async Task<T> ExecutePipelineAsync<T>(
        CockroachDBContext context,
        int index,
        Func<Task<T>> operation)
    {
        if (index >= _middlewares.Count)
            return await operation();

        return await _middlewares[index].ExecuteAsync(
            context,
            async ctx => await ExecutePipelineAsync<T>(ctx, index + 1, operation));
    }
}
