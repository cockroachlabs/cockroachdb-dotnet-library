using System.Diagnostics;

namespace CockroachDB.DotNet.AI.Metrics;

public sealed class InMemoryAIMetrics : IAIMetrics
{
    private long _vectorSearchCount;
    private long _vectorSearchFailureCount;
    private long _vectorSearchDurationTicks;
    private long _totalResults;
    private long _embeddingGenerationCount;
    private long _embeddingTokenCount;
    private long _chatCompletionCount;

    public void RecordVectorSearch(long startTimestamp, int resultCount, bool success)
    {
        var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
        Interlocked.Increment(ref _vectorSearchCount);
        Interlocked.Add(ref _vectorSearchDurationTicks, elapsed);
        Interlocked.Add(ref _totalResults, resultCount);
        if (!success) Interlocked.Increment(ref _vectorSearchFailureCount);
    }

    public void RecordEmbeddingGeneration(long startTimestamp, int tokenCount, bool success)
    {
        Interlocked.Increment(ref _embeddingGenerationCount);
        Interlocked.Add(ref _embeddingTokenCount, tokenCount);
    }

    public void RecordChatCompletion(long startTimestamp, bool success)
    {
        Interlocked.Increment(ref _chatCompletionCount);
    }

    public long VectorSearchCount => Interlocked.Read(ref _vectorSearchCount);
    public long VectorSearchFailureCount => Interlocked.Read(ref _vectorSearchFailureCount);
    public long EmbeddingGenerationCount => Interlocked.Read(ref _embeddingGenerationCount);
    public long ChatCompletionCount => Interlocked.Read(ref _chatCompletionCount);
    public long TotalResults => Interlocked.Read(ref _totalResults);
    public long EmbeddingTokenCount => Interlocked.Read(ref _embeddingTokenCount);

    public double AverageVectorSearchDurationMs
    {
        get
        {
            var count = VectorSearchCount;
            if (count == 0) return 0;
            var totalTicks = Interlocked.Read(ref _vectorSearchDurationTicks);
            return (double)totalTicks / Stopwatch.Frequency * 1000 / count;
        }
    }
}
