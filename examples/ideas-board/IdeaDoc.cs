using CockroachDB.DotNet.AI.Attributes;

namespace IdeasBoard;

// tag::vector-entity[]
// A document for the semantic-search demo. The embedding is a small, fixed-dim
// vector mapped to a CockroachDB VECTOR column; [VectorColumn] records the
// dimension the library expects. Embeddings here are deterministic (no model),
// so the similarity search is fully reproducible on a single Testcontainers node.
public sealed class IdeaDoc
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Text { get; set; } = "";

    [VectorColumn(Dimensions = 3)]
    public float[] Embedding { get; set; } = [];
}
// end::vector-entity[]
