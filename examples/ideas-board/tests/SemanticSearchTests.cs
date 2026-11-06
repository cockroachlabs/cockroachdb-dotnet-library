using CockroachDB.DotNet.AI;
using CockroachDB.DotNet.AI.Extensions;
using CockroachDB.DotNet.AI.VectorStore;
using CockroachDB.DotNet.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace IdeasBoard.Tests;

/// <summary>
/// Proves semantic / vector search against a real CockroachDB (Testcontainers):
/// documents are stored in a VECTOR column and the nearest ones come back in
/// similarity order. Deterministic 3-dim vectors (no model) make it reproducible
/// on a single node. The C-SPANN vector index feature is enabled on the container
/// via <c>EnableVectorIndexFeatureAsync</c> before the store creates its index.
/// </summary>
[Trait("Category", "Integration")]
public class SemanticSearchTests : IAsyncLifetime
{
    private readonly CockroachDBFixture _fixture = new();
    private ServiceProvider _sp = null!;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        // Enable the CockroachDB vector index feature before building the index.
        await _fixture.EnableVectorIndexFeatureAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NpgsqlDataSource.Create(_fixture.ConnectionString));
        services.AddCockroachDBAI(config);
        services.AddSingleton<IEmbeddingGenerator, ZeroEmbeddingGenerator>();
        services.AddCockroachDBVectorStore(b => b
            .WithDimensions(3)
            .WithDistanceMetric(DistanceMetric.L2)
            .WithInitializeSchema(true)
            .WithCreateVectorIndex());
        services.AddSingleton<SemanticIdeaService>();
        _sp = services.BuildServiceProvider();

        // Creates the VECTOR table + vector index.
        await _sp.GetRequiredService<SemanticIdeaService>().InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _sp.DisposeAsync();
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task FindSimilarAsync_ReturnsNearestInSimilarityOrder()
    {
        var svc = _sp.GetRequiredService<SemanticIdeaService>();

        // tag::vector-test[]
        // Four docs with distinct, deterministic 3-dim vectors. "dark mode" and
        // "dark theme" lie on the same x-axis direction (dark theme just farther out);
        // the other two sit on orthogonal axes and are far from the query.
        var dark   = new IdeaDoc { Id = "dark-mode",  Text = "add dark mode",     Embedding = [1f, 0f, 0f] };
        var theme  = new IdeaDoc { Id = "dark-theme", Text = "dark theme toggle", Embedding = [2f, 0f, 0f] };
        var csv    = new IdeaDoc { Id = "export-csv", Text = "export to csv",     Embedding = [0f, 1f, 0f] };
        var mobile = new IdeaDoc { Id = "mobile-app", Text = "mobile app",        Embedding = [0f, 0f, 1f] };
        foreach (var d in new[] { dark, theme, csv, mobile })
            await svc.StoreAsync(d);

        // Query near "dark mode": nearest by L2 is dark-mode (0.1), then dark-theme
        // (0.9); the orthogonal docs are far (~1.49) and must not appear in the top 2.
        var hits = await svc.FindSimilarAsync([1.1f, 0f, 0f], k: 2);

        Assert.Equal(2, hits.Count);
        Assert.Equal("dark-mode", hits[0].Id?.ToString());
        Assert.Equal("dark-theme", hits[1].Id?.ToString());
        // Closer document has the smaller distance (= higher similarity).
        Assert.True(hits[0].Distance < hits[1].Distance,
            $"expected ascending distance, got {hits[0].Distance} then {hits[1].Distance}");
        // end::vector-test[]
    }
}
