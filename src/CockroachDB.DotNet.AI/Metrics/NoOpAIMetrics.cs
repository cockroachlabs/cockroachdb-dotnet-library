namespace CockroachDB.DotNet.AI.Metrics;

public sealed class NoOpAIMetrics : IAIMetrics
{
    public static readonly NoOpAIMetrics Instance = new();

    public void RecordVectorSearch(long startTimestamp, int resultCount, bool success) { }
    public void RecordEmbeddingGeneration(long startTimestamp, int tokenCount, bool success) { }
    public void RecordChatCompletion(long startTimestamp, bool success) { }

    public long VectorSearchCount => 0;
    public long VectorSearchFailureCount => 0;
    public long EmbeddingGenerationCount => 0;
    public long ChatCompletionCount => 0;
    public double AverageVectorSearchDurationMs => 0;
}
