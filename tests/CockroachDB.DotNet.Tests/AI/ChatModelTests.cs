using CockroachDB.DotNet.AI.Chat;
using CockroachDB.DotNet.AI.VectorStore;

namespace CockroachDB.DotNet.Tests.AI;

public class ChatModelTests
{
    [Fact]
    public void ChatMessage_Record_Creates_Correctly()
    {
        var msg = new ChatMessage(ChatRole.User, "Hello");

        Assert.Equal(ChatRole.User, msg.Role);
        Assert.Equal("Hello", msg.Content);
    }

    [Fact]
    public void ChatResponse_Default_Values()
    {
        var response = new ChatResponse();

        Assert.Equal("", response.Content);
        Assert.Empty(response.SourceDocuments);
        Assert.Equal(0, response.TokensUsed);
    }

    [Fact]
    public void ChatResponse_With_Source_Documents()
    {
        var docs = new List<VectorDocument>
        {
            new() { Id = "1", Text = "context doc" }
        };
        var response = new ChatResponse
        {
            Content = "Answer based on context",
            SourceDocuments = docs,
            TokensUsed = 150
        };

        Assert.Equal("Answer based on context", response.Content);
        Assert.Single(response.SourceDocuments);
        Assert.Equal(150, response.TokensUsed);
    }

    [Fact]
    public void ChatOptions_Default_Values()
    {
        var options = new CockroachDB.DotNet.AI.Chat.ChatOptions();

        Assert.Null(options.SystemPrompt);
        Assert.Equal(0.7, options.Temperature);
        Assert.Equal(1024, options.MaxTokens);
        Assert.Null(options.History);
    }

    [Fact]
    public void ChatRole_Enum_Has_All_Values()
    {
        var values = Enum.GetValues<ChatRole>();
        Assert.Equal(3, values.Length);
        Assert.Contains(ChatRole.System, values);
        Assert.Contains(ChatRole.User, values);
        Assert.Contains(ChatRole.Assistant, values);
    }
}
