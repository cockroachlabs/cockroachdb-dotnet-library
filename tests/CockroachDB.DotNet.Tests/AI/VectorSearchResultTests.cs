using CockroachDB.DotNet.AI;

namespace CockroachDB.DotNet.Tests.AI;

public class VectorSearchResultTests
{
    [Fact]
    public void Constructor_With_Id_And_Distance_Creates_Empty_Metadata()
    {
        var result = new VectorSearchResult<string>("doc-1", 0.25);

        Assert.Equal("doc-1", result.Id);
        Assert.Equal(0.25, result.Distance);
        Assert.Empty(result.Metadata);
    }

    [Fact]
    public void Constructor_With_Metadata_Preserves_All_Fields()
    {
        var metadata = new Dictionary<string, object?> { ["category"] = "science" };
        var result = new VectorSearchResult<int>(42, 0.1, metadata);

        Assert.Equal(42, result.Id);
        Assert.Equal(0.1, result.Distance);
        Assert.Equal("science", result.Metadata["category"]);
    }

    [Fact]
    public void Similarity_Uses_Correct_Metric()
    {
        var result = new VectorSearchResult<string>("x", 0.3);

        Assert.Equal(0.7, result.Similarity(DistanceMetric.Cosine), precision: 10);
        Assert.Equal(-0.3, result.Similarity(DistanceMetric.InnerProduct), precision: 10);
    }

    [Fact]
    public void Record_Equality_Works_Correctly()
    {
        var meta = new Dictionary<string, object?>();
        var a = new VectorSearchResult<string>("id", 0.5, meta);
        var b = new VectorSearchResult<string>("id", 0.5, meta);

        Assert.Equal(a, b);
    }
}
