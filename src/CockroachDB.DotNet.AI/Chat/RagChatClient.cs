using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using CockroachDB.DotNet.AI.Configuration;
using CockroachDB.DotNet.AI.Metrics;
using CockroachDB.DotNet.AI.VectorStore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CockroachDB.DotNet.AI.Chat;

public interface IChatCompletionProvider
{
    Task<string> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        double temperature,
        int maxTokens,
        CancellationToken ct = default);

    IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        double temperature,
        int maxTokens,
        CancellationToken ct = default);
}

public sealed class RagChatClient : IChatClient
{
    private readonly CockroachDBVectorStore _vectorStore;
    private readonly IChatCompletionProvider _completionProvider;
    private readonly IAIMetrics _metrics;
    private readonly ILogger<RagChatClient> _logger;
    private readonly ChatOptions _defaultOptions;
    private readonly int _maxContextDocuments;
    private readonly double _similarityThreshold;

    public RagChatClient(
        CockroachDBVectorStore vectorStore,
        IChatCompletionProvider completionProvider,
        IAIMetrics metrics,
        ILogger<RagChatClient> logger,
        IOptions<AIOptions> options)
    {
        _vectorStore = vectorStore;
        _completionProvider = completionProvider;
        _metrics = metrics;
        _logger = logger;
        _maxContextDocuments = options.Value.Chat.MaxContextDocuments;
        _similarityThreshold = options.Value.Chat.SimilarityThreshold;
        _defaultOptions = new ChatOptions
        {
            SystemPrompt = options.Value.Chat.SystemPrompt
        };
    }

    public async Task<ChatResponse> CompleteAsync(
        string userMessage,
        ChatOptions? options = null,
        CancellationToken ct = default)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var contextDocs = await _vectorStore.SimilaritySearchAsync(
                userMessage,
                topK: _maxContextDocuments,
                similarityThreshold: _similarityThreshold,
                ct: ct);

            var messages = BuildMessages(userMessage, contextDocs, options);

            var response = await _completionProvider.CompleteAsync(
                messages,
                options?.Temperature ?? _defaultOptions.Temperature,
                options?.MaxTokens ?? _defaultOptions.MaxTokens,
                ct);

            _metrics.RecordChatCompletion(startTimestamp, true);
            _logger.LogDebug("RAG chat completed with {DocCount} context documents", contextDocs.Count);

            return new ChatResponse
            {
                Content = response,
                SourceDocuments = contextDocs
            };
        }
        catch (Exception ex)
        {
            _metrics.RecordChatCompletion(startTimestamp, false);
            _logger.LogError(ex, "RAG chat completion failed");
            throw;
        }
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string userMessage,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        var contextDocs = await _vectorStore.SimilaritySearchAsync(
            userMessage,
            topK: _maxContextDocuments,
            similarityThreshold: _similarityThreshold,
            ct: ct);

        var messages = BuildMessages(userMessage, contextDocs, options);

        await foreach (var chunk in _completionProvider.StreamAsync(
            messages,
            options?.Temperature ?? _defaultOptions.Temperature,
            options?.MaxTokens ?? _defaultOptions.MaxTokens,
            ct))
        {
            yield return chunk;
        }

        _metrics.RecordChatCompletion(startTimestamp, true);
    }

    private List<ChatMessage> BuildMessages(
        string userMessage,
        IReadOnlyList<VectorDocument> contextDocs,
        ChatOptions? options)
    {
        var messages = new List<ChatMessage>();

        var systemPrompt = options?.SystemPrompt ?? _defaultOptions.SystemPrompt
            ?? "You are a helpful assistant. Use the provided context to answer questions accurately.";

        if (contextDocs.Count > 0)
        {
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("Context from the knowledge base:");
            contextBuilder.AppendLine();
            for (var i = 0; i < contextDocs.Count; i++)
            {
                contextBuilder.AppendLine($"[Document {i + 1}]");
                contextBuilder.AppendLine(contextDocs[i].Text);
                contextBuilder.AppendLine();
            }

            systemPrompt += "\n\n" + contextBuilder;
        }

        messages.Add(new ChatMessage(ChatRole.System, systemPrompt));

        if (options?.History is { Count: > 0 } history)
        {
            messages.AddRange(history);
        }

        messages.Add(new ChatMessage(ChatRole.User, userMessage));
        return messages;
    }
}
