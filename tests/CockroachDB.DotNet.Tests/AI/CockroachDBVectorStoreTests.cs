using CockroachDB.DotNet.AI;
using CockroachDB.DotNet.AI.Configuration;
using CockroachDB.DotNet.AI.VectorStore;
using NSubstitute;
using Npgsql;

namespace CockroachDB.DotNet.Tests.AI;

public class CockroachDBVectorStoreBuilderTests : IDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IEmbeddingGenerator _embeddingGen;

    public CockroachDBVectorStoreBuilderTests()
    {
        _dataSource = NpgsqlDataSource.Create("Host=localhost;Port=26257;Database=test;Username=root");
        _embeddingGen = Substitute.For<IEmbeddingGenerator>();
    }

    public void Dispose() => _dataSource.Dispose();

    [Fact]
    public void Builder_Build_Returns_Non_Null_Store()
    {
        var store = CockroachDBVectorStore.CreateBuilder(_dataSource, _embeddingGen).Build();
        Assert.NotNull(store);
    }

    [Fact]
    public void Builder_WithCreateVectorIndex_Builds_Successfully()
    {
        var store = CockroachDBVectorStore.CreateBuilder(_dataSource, _embeddingGen)
            .WithCreateVectorIndex()
            .Build();

        Assert.NotNull(store);
    }

    [Fact]
    public void Builder_WithVectorIndexPrefixColumns_Builds_Successfully()
    {
        var store = CockroachDBVectorStore.CreateBuilder(_dataSource, _embeddingGen)
            .WithVectorIndexPrefixColumns("tenant_id", "region")
            .WithCreateVectorIndex()
            .Build();

        Assert.NotNull(store);
    }

    [Fact]
    public void Builder_WithBeamSize_Builds_Successfully()
    {
        var store = CockroachDBVectorStore.CreateBuilder(_dataSource, _embeddingGen)
            .WithBeamSize(64)
            .Build();

        Assert.NotNull(store);
    }

    [Fact]
    public void Builder_WithDistanceMetric_L2_Builds_Successfully()
    {
        var store = CockroachDBVectorStore.CreateBuilder(_dataSource, _embeddingGen)
            .WithDistanceMetric(DistanceMetric.L2)
            .Build();

        Assert.NotNull(store);
    }

    [Fact]
    public void Builder_WithDistanceMetric_InnerProduct_Builds_Successfully()
    {
        var store = CockroachDBVectorStore.CreateBuilder(_dataSource, _embeddingGen)
            .WithDistanceMetric(DistanceMetric.InnerProduct)
            .Build();

        Assert.NotNull(store);
    }

    [Fact]
    public void Builder_Fluent_Chain_All_V26_2_Options()
    {
        var store = CockroachDBVectorStore.CreateBuilder(_dataSource, _embeddingGen)
            .WithTableName("documents")
            .WithDistanceMetric(DistanceMetric.L2)
            .WithBeamSize(32)
            .WithCreateVectorIndex()
            .WithVectorIndexPrefixColumns("tenant_id")
            .WithInitializeSchema(true)
            .WithDimensions(768)
            .WithContentColumn("body")
            .WithEmbeddingColumn("vec")
            .WithMetadataColumn("meta")
            .Build();

        Assert.NotNull(store);
    }

    [Fact]
    public void Builder_WithCreateVectorIndex_False_Builds_Successfully()
    {
        var store = CockroachDBVectorStore.CreateBuilder(_dataSource, _embeddingGen)
            .WithCreateVectorIndex(false)
            .Build();

        Assert.NotNull(store);
    }

    [Fact]
    public void Builder_Empty_PrefixColumns_Builds_Successfully()
    {
        var store = CockroachDBVectorStore.CreateBuilder(_dataSource, _embeddingGen)
            .WithVectorIndexPrefixColumns()
            .WithCreateVectorIndex()
            .Build();

        Assert.NotNull(store);
    }
}

public class VectorSearchOptionsV26Tests
{
    [Fact]
    public void CreateVectorIndex_Default_Is_False()
    {
        var options = new VectorSearchOptions();
        Assert.False(options.CreateVectorIndex);
    }

    [Fact]
    public void VectorIndexPrefixColumns_Default_Is_Empty()
    {
        var options = new VectorSearchOptions();
        Assert.Empty(options.VectorIndexPrefixColumns);
    }

    [Fact]
    public void CreateVectorIndex_Can_Be_Set()
    {
        var options = new VectorSearchOptions { CreateVectorIndex = true };
        Assert.True(options.CreateVectorIndex);
    }

    [Fact]
    public void VectorIndexPrefixColumns_Can_Be_Set()
    {
        var options = new VectorSearchOptions
        {
            VectorIndexPrefixColumns = ["tenant_id", "region"]
        };

        Assert.Equal(2, options.VectorIndexPrefixColumns.Length);
        Assert.Equal("tenant_id", options.VectorIndexPrefixColumns[0]);
        Assert.Equal("region", options.VectorIndexPrefixColumns[1]);
    }

    [Fact]
    public void All_V26_2_Options_Are_Configurable()
    {
        var options = new VectorSearchOptions
        {
            DefaultMetric = DistanceMetric.L2,
            DefaultBeamSize = 32,
            CreateVectorIndex = true,
            VectorIndexPrefixColumns = ["tenant_id"],
            Dimensions = 768,
            InitializeSchema = true
        };

        Assert.Equal(DistanceMetric.L2, options.DefaultMetric);
        Assert.Equal(32, options.DefaultBeamSize);
        Assert.True(options.CreateVectorIndex);
        Assert.Single(options.VectorIndexPrefixColumns);
        Assert.Equal(768, options.Dimensions);
        Assert.True(options.InitializeSchema);
    }
}
