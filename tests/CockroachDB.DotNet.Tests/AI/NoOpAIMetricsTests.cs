using System.Diagnostics;
using CockroachDB.DotNet.AI.Metrics;

namespace CockroachDB.DotNet.Tests.AI;

public class NoOpAIMetricsTests
{
    [Fact]
    public void All_Counters_Return_Zero()
    {
        var metrics = NoOpAIMetrics.Instance;

        Assert.Equal(0, metrics.VectorSearchCount);
        Assert.Equal(0, metrics.VectorSearchFailureCount);
        Assert.Equal(0, metrics.EmbeddingGenerationCount);
        Assert.Equal(0, metrics.ChatCompletionCount);
        Assert.Equal(0, metrics.AverageVectorSearchDurationMs);
    }

    [Fact]
    public void Record_Methods_Do_Not_Throw()
    {
        var metrics = NoOpAIMetrics.Instance;
        var start = Stopwatch.GetTimestamp();

        metrics.RecordVectorSearch(start, 5, true);
        metrics.RecordEmbeddingGeneration(start, 100, true);
        metrics.RecordChatCompletion(start, false);

        Assert.Equal(0, metrics.VectorSearchCount);
    }

    [Fact]
    public void Singleton_Returns_Same_Instance()
    {
        Assert.Same(NoOpAIMetrics.Instance, NoOpAIMetrics.Instance);
    }
}
