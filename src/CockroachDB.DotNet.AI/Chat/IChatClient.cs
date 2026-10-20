namespace CockroachDB.DotNet.AI.Chat;

public interface IChatClient
{
    Task<ChatResponse> CompleteAsync(string userMessage, ChatOptions? options = null, CancellationToken ct = default);

    IAsyncEnumerable<string> StreamAsync(string userMessage, ChatOptions? options = null, CancellationToken ct = default);
}

public sealed class ChatOptions
{
    public string? SystemPrompt { get; set; }
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 1024;
    public IReadOnlyList<ChatMessage>? History { get; set; }
}

public sealed record ChatMessage(ChatRole Role, string Content);

public enum ChatRole
{
    System,
    User,
    Assistant
}

public sealed class ChatResponse
{
    public string Content { get; init; } = "";
    public IReadOnlyList<VectorStore.VectorDocument> SourceDocuments { get; init; } = [];
    public int TokensUsed { get; init; }
}
