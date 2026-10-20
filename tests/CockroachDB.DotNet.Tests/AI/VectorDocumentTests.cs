using CockroachDB.DotNet.AI.VectorStore;

namespace CockroachDB.DotNet.Tests.AI;

public class VectorDocumentTests
{
    [Fact]
    public void Default_Id_Is_Valid_Guid()
    {
        var doc = new VectorDocument();
        Assert.True(Guid.TryParse(doc.Id, out _));
    }

    [Fact]
    public void Properties_Are_Settable()
    {
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var doc = new VectorDocument
        {
            Id = "doc-123",
            Text = "Hello world",
            Metadata = new Dictionary<string, object?> { ["source"] = "test" },
            Embedding = embedding,
            Score = 0.95
        };

        Assert.Equal("doc-123", doc.Id);
        Assert.Equal("Hello world", doc.Text);
        Assert.Equal("test", doc.Metadata["source"]);
        Assert.Equal(embedding, doc.Embedding);
        Assert.Equal(0.95, doc.Score);
    }

    [Fact]
    public void Default_Metadata_Is_Empty_Dictionary()
    {
        var doc = new VectorDocument();
        Assert.NotNull(doc.Metadata);
        Assert.Empty(doc.Metadata);
    }

    [Fact]
    public void Default_Embedding_Is_Null()
    {
        var doc = new VectorDocument();
        Assert.Null(doc.Embedding);
    }
}
