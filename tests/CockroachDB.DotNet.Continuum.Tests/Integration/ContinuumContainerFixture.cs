using CockroachDB.DotNet.Testing;
using Xunit;

namespace CockroachDB.DotNet.Continuum.Tests.Integration;

/// <summary>
/// Shared CockroachDB container for the Continuum integration tests. Spins up its
/// own single-node CockroachDB via Testcontainers, so it never collides with any
/// other project's node.
/// </summary>
public sealed class ContinuumContainerFixture : IAsyncLifetime
{
    private readonly CockroachDBFixture _fixture = new();

    public string ConnectionString => _fixture.ConnectionString;

    public Task InitializeAsync() => _fixture.InitializeAsync();

    public async Task DisposeAsync() => await _fixture.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class ContinuumCollection : ICollectionFixture<ContinuumContainerFixture>
{
    public const string Name = "continuum-integration";
}
