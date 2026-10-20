namespace CockroachDB.DotNet.AI.Metrics;

public interface IAIMetrics
{
    void RecordVectorSearch(long startTimestamp, int resultCount, bool success);

    void RecordEmbeddingGeneration(long startTimestamp, int tokenCount, bool success);

    void RecordChatCompletion(long startTimestamp, bool success);

    long VectorSearchCount { get; }

    long VectorSearchFailureCount { get; }

    long EmbeddingGenerationCount { get; }

    long ChatCompletionCount { get; }

    double AverageVectorSearchDurationMs { get; }
}
