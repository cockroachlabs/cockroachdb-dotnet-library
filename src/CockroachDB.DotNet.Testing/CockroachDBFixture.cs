using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Npgsql;

namespace CockroachDB.DotNet.Testing;

public sealed class CockroachDBFixture : IAsyncDisposable
{
    private IContainer? _container;

    public string ConnectionString { get; private set; } = "";

    public string Host { get; private set; } = "localhost";

    public int Port { get; private set; }

    public async Task InitializeAsync(
        string image = "cockroachdb/cockroach:latest-v26.2",
        CancellationToken cancellationToken = default)
    {
        _container = new ContainerBuilder(image)
            .WithCommand("start-single-node", "--insecure")
            .WithPortBinding(26257, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("CockroachDB node starting"))
            .Build();

        await _container.StartAsync(cancellationToken);

        Host = _container.Hostname;
        Port = _container.GetMappedPublicPort(26257);
        ConnectionString = $"Host={Host};Port={Port};Database=defaultdb;Username=root;SSL Mode=Disable;";
    }

    /// <summary>
    /// Enables the vector index feature required for CockroachDB v26.2+.
    /// Call after InitializeAsync() and before running vector index tests.
    /// </summary>
    public async Task EnableVectorIndexFeatureAsync(CancellationToken cancellationToken = default)
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SET CLUSTER SETTING feature.vector_index.enabled = true";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
