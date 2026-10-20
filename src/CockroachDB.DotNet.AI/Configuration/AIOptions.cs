namespace CockroachDB.DotNet.AI.Configuration;

public sealed class AIOptions
{
    public const string SectionName = "CockroachDB:AI";

    public bool Enabled { get; set; }

    public VectorSearchOptions VectorSearch { get; set; } = new();

    public ChatOptions Chat { get; set; } = new();

    public EmbeddingOptions Embedding { get; set; } = new();
}

public sealed class VectorSearchOptions
{
    public DistanceMetric DefaultMetric { get; set; } = DistanceMetric.Cosine;

    public int DefaultLimit { get; set; } = 10;

    public int DefaultBeamSize { get; set; }

    public string DefaultTable { get; set; } = "vector_store";

    public string DefaultVectorColumn { get; set; } = "embedding";

    public bool InitializeSchema { get; set; }

    public int Dimensions { get; set; }

    /// <summary>
    /// Whether to create a vector index during schema initialization (v26.2+).
    /// </summary>
    public bool CreateVectorIndex { get; set; }

    /// <summary>
    /// Prefix columns for the vector index, enabling multi-tenant workloads (v26.2+).
    /// Example: ["tenant_id"] produces CREATE VECTOR INDEX ON table (tenant_id, embedding vector_cosine_ops).
    /// </summary>
    public string[] VectorIndexPrefixColumns { get; set; } = [];
}

public sealed class ChatOptions
{
    public string SystemPrompt { get; set; } = "You are a helpful assistant. Use the provided context to answer questions accurately.";

    public int MaxContextDocuments { get; set; } = 5;

    public double SimilarityThreshold { get; set; } = 0.7;
}

public sealed class EmbeddingOptions
{
    public string Provider { get; set; } = "local";

    public int Dimensions { get; set; } = 1536;
}
