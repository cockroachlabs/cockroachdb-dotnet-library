using CockroachDB.DotNet.AI;
using CockroachDB.DotNet.AI.Configuration;

namespace CockroachDB.DotNet.Tests.AI;

public class AIOptionsTests
{
    [Fact]
    public void SectionName_Is_Correct()
    {
        Assert.Equal("CockroachDB:AI", AIOptions.SectionName);
    }

    [Fact]
    public void Default_Values_Are_Sensible()
    {
        var options = new AIOptions();

        Assert.False(options.Enabled);
        Assert.Equal(DistanceMetric.Cosine, options.VectorSearch.DefaultMetric);
        Assert.Equal(10, options.VectorSearch.DefaultLimit);
        Assert.Equal(0, options.VectorSearch.DefaultBeamSize);
        Assert.Equal("vector_store", options.VectorSearch.DefaultTable);
        Assert.Equal("embedding", options.VectorSearch.DefaultVectorColumn);
        Assert.False(options.VectorSearch.InitializeSchema);
        Assert.False(options.VectorSearch.CreateVectorIndex);
        Assert.Empty(options.VectorSearch.VectorIndexPrefixColumns);
    }

    [Fact]
    public void Chat_Options_Have_Sensible_Defaults()
    {
        var options = new ChatOptions();

        Assert.Equal(5, options.MaxContextDocuments);
        Assert.Equal(0.7, options.SimilarityThreshold);
        Assert.Contains("helpful assistant", options.SystemPrompt);
    }

    [Fact]
    public void Embedding_Options_Have_Sensible_Defaults()
    {
        var options = new EmbeddingOptions();

        Assert.Equal("local", options.Provider);
        Assert.Equal(1536, options.Dimensions);
    }
}
