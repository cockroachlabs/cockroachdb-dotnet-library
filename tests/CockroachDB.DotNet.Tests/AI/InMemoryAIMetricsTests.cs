using System.Diagnostics;
using CockroachDB.DotNet.AI.Metrics;

namespace CockroachDB.DotNet.Tests.AI;

public class InMemoryAIMetricsTests
{
    [Fact]
    public void RecordVectorSearch_Increments_Count()
    {
        var metrics = new InMemoryAIMetrics();
        var start = Stopwatch.GetTimestamp();

        metrics.RecordVectorSearch(start, 5, true);
        metrics.RecordVectorSearch(start, 3, true);

        Assert.Equal(2, metrics.VectorSearchCount);
        Assert.Equal(8, metrics.TotalResults);
        Assert.Equal(0, metrics.VectorSearchFailureCount);
    }

    [Fact]
    public void RecordVectorSearch_Tracks_Failures()
    {
        var metrics = new InMemoryAIMetrics();
        var start = Stopwatch.GetTimestamp();

        metrics.RecordVectorSearch(start, 0, false);

        Assert.Equal(1, metrics.VectorSearchCount);
        Assert.Equal(1, metrics.VectorSearchFailureCount);
    }

    [Fact]
    public void AverageDuration_Returns_Zero_When_No_Searches()
    {
        var metrics = new InMemoryAIMetrics();
        Assert.Equal(0, metrics.AverageVectorSearchDurationMs);
    }

    [Fact]
    public void AverageDuration_Returns_Positive_Value()
    {
        var metrics = new InMemoryAIMetrics();
        var start = Stopwatch.GetTimestamp();
        Thread.Sleep(10);
        metrics.RecordVectorSearch(start, 1, true);

        Assert.True(metrics.AverageVectorSearchDurationMs > 0);
    }

    [Fact]
    public void RecordEmbeddingGeneration_Increments_Count()
    {
        var metrics = new InMemoryAIMetrics();
        var start = Stopwatch.GetTimestamp();

        metrics.RecordEmbeddingGeneration(start, 100, true);
        metrics.RecordEmbeddingGeneration(start, 200, true);

        Assert.Equal(2, metrics.EmbeddingGenerationCount);
        Assert.Equal(300, metrics.EmbeddingTokenCount);
    }

    [Fact]
    public void RecordChatCompletion_Increments_Count()
    {
        var metrics = new InMemoryAIMetrics();
        var start = Stopwatch.GetTimestamp();

        metrics.RecordChatCompletion(start, true);
        metrics.RecordChatCompletion(start, true);
        metrics.RecordChatCompletion(start, false);

        Assert.Equal(3, metrics.ChatCompletionCount);
    }
}
