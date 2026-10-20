using CockroachDB.DotNet.AI.Chat;
using CockroachDB.DotNet.AI.Configuration;
using CockroachDB.DotNet.AI.Metrics;
using CockroachDB.DotNet.AI.Template;
using CockroachDB.DotNet.AI.VectorStore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CockroachDB.DotNet.AI.Extensions;

public static class AIServiceCollectionExtensions
{
    public static IServiceCollection AddCockroachDBAI(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AIOptions>? configure = null)
    {
        services.Configure<AIOptions>(configuration.GetSection(AIOptions.SectionName));
        if (configure is not null)
            services.PostConfigure(configure);

        services.TryAddSingleton<IAIMetrics, InMemoryAIMetrics>();
        services.TryAddSingleton<VectorSearchTemplate>();

        return services;
    }

    public static IServiceCollection AddCockroachDBVectorStore(
        this IServiceCollection services,
        Action<CockroachDBVectorStore.Builder>? configureBuilder = null)
    {
        services.AddSingleton(sp =>
        {
            var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
            var embeddingGenerator = sp.GetRequiredService<IEmbeddingGenerator>();
            var options = sp.GetRequiredService<IOptions<AIOptions>>().Value;
            var metrics = sp.GetRequiredService<IAIMetrics>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CockroachDBVectorStore>>();

            var builder = CockroachDBVectorStore.CreateBuilder(dataSource, embeddingGenerator)
                .WithMetrics(metrics)
                .WithLogger(logger)
                .WithTableName(options.VectorSearch.DefaultTable)
                .WithDistanceMetric(options.VectorSearch.DefaultMetric)
                .WithBeamSize(options.VectorSearch.DefaultBeamSize)
                .WithInitializeSchema(options.VectorSearch.InitializeSchema)
                .WithDimensions(options.VectorSearch.Dimensions)
                .WithCreateVectorIndex(options.VectorSearch.CreateVectorIndex)
                .WithVectorIndexPrefixColumns(options.VectorSearch.VectorIndexPrefixColumns);

            configureBuilder?.Invoke(builder);
            return builder.Build();
        });

        return services;
    }

    public static IServiceCollection AddCockroachDBRagChat(this IServiceCollection services)
    {
        services.TryAddSingleton<IChatClient, RagChatClient>();
        return services;
    }
}
