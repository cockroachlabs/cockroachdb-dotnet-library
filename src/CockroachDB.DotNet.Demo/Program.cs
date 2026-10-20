using CockroachDB.DotNet.Cdc.Extensions;
using CockroachDB.DotNet.Cdc.Endpoints;
using CockroachDB.DotNet.Core.Extensions;
using CockroachDB.DotNet.Core.FollowerRead;
using CockroachDB.DotNet.Demo.Handlers;
using CockroachDB.DotNet.Demo.Models;
using CockroachDB.DotNet.Demo.Data;
using CockroachDB.DotNet.Abstractions.Template;
using CockroachDB.DotNet.EntityFramework.Extensions;
using CockroachDB.DotNet.AI.Extensions;
using CockroachDB.DotNet.AI.VectorStore;
using CockroachDB.DotNet.AI.Chat;
using CockroachDB.DotNet.AI.Metrics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Register Npgsql data source
var connectionString = builder.Configuration.GetConnectionString("CockroachDB")
    ?? "Host=localhost;Port=26257;Database=defaultdb;Username=root;SSL Mode=Disable;";

builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));

// Register CockroachDB middleware pipeline
builder.Services.AddCockroachDB(builder.Configuration, pipeline =>
{
    pipeline.UseMetrics();
    pipeline.UseCircuitBreaker();
    pipeline.UseRetry();
    pipeline.UseRegion();
    pipeline.UseFollowerRead();
});

// Register AI services (vector store, RAG chat, metrics)
// v26.2: CreateVectorIndex creates a VECTOR INDEX with the new DDL syntax
builder.Services.AddCockroachDBAI(builder.Configuration);
builder.Services.AddCockroachDBVectorStore(b => b
    .WithCreateVectorIndex()
    .WithBeamSize(32));
builder.Services.AddCockroachDBRagChat();

// Register CDC
builder.Services.AddCockroachDBCdc();
builder.Services.AddChangefeedHandler<Order, OrderChangefeedHandler>();

// Register EF Core with CockroachDB interceptor
builder.Services.AddDbContext<DemoDbContext>(options =>
    options.UseCockroachDB(connectionString));

var app = builder.Build();

// Health checks
app.MapHealthChecks("/health");

// CDC webhook endpoint
app.MapCockroachDBChangefeed<Order>("/webhooks/orders");

// Demo endpoints
app.MapGet("/", () => "CockroachDB .NET Library Demo");

app.MapPost("/orders", async (Order order, DemoDbContext db) =>
{
    db.Orders.Add(order);
    await db.SaveChangesAsync();
    return Results.Created($"/orders/{order.Id}", order);
});

app.MapGet("/orders", async (DemoDbContext db) =>
    await db.Orders.ToListAsync());

// Follower-read endpoint — reads from the nearest replica for lower latency
app.MapGet("/orders/stale", async (DemoDbContext db) =>
{
    using (FollowerReadContext.Scoped())
    {
        return await db.Orders.AsNoTracking().ToListAsync();
    }
});

// Template-based follower-read endpoint (programmatic API)
app.MapGet("/orders/stale/template", async (ICockroachDBTemplate template, DemoDbContext db) =>
    await template.ExecuteFollowerReadAsync(async () =>
        await db.Orders.AsNoTracking().ToListAsync()));

// --- AI / Vector Search endpoints ---

// Initialize vector store schema (creates table + v26.2 CREATE VECTOR INDEX)
app.MapPost("/ai/init", async (CockroachDBVectorStore store) =>
{
    await store.InitializeSchemaAsync();
    return Results.Ok(new { status = "Vector store schema initialized with v26.2 vector index" });
});

// Create vector index with prefix columns (multi-tenant example)
app.MapPost("/ai/vector-index", async (CockroachDBVectorStore store) =>
{
    await store.CreateVectorIndexAsync();
    return Results.Ok(new { status = "Vector index created" });
});

// Ingest documents into the vector store
app.MapPost("/ai/documents", async (
    List<VectorDocument> documents,
    CockroachDBVectorStore store) =>
{
    await store.AddDocumentsAsync(documents);
    return Results.Ok(new { ingested = documents.Count });
});

// Delete documents by ID
app.MapDelete("/ai/documents", async (
    string[] ids,
    CockroachDBVectorStore store) =>
{
    await store.DeleteDocumentsAsync(ids);
    return Results.Ok(new { deleted = ids.Length });
});

// Similarity search — accepts a text query, generates embedding internally
app.MapPost("/ai/search", async (
    VectorSearchRequest request,
    CockroachDBVectorStore store) =>
{
    var results = await store.SimilaritySearchAsync(
        request.Query,
        request.Limit,
        request.SimilarityThreshold,
        request.FilterExpression);
    return Results.Ok(results);
});

// RAG chat — retrieves context from vector store, sends to LLM
app.MapPost("/ai/chat", async (
    ChatRequest request,
    IChatClient chatClient) =>
{
    var options = new CockroachDB.DotNet.AI.Chat.ChatOptions
    {
        SystemPrompt = request.SystemPrompt,
        Temperature = request.Temperature,
        MaxTokens = request.MaxTokens,
        History = request.History?.Select(m =>
            new ChatMessage(Enum.Parse<ChatRole>(m.Role, ignoreCase: true), m.Content)).ToList()
    };

    var response = await chatClient.CompleteAsync(request.Message, options);
    return Results.Ok(new
    {
        response.Content,
        response.TokensUsed,
        sourceDocuments = response.SourceDocuments.Select(d => new
        {
            d.Id, d.Text, d.Score
        })
    });
});

// AI metrics endpoint
app.MapGet("/ai/metrics", (IAIMetrics metrics) =>
    Results.Ok(new
    {
        metrics.VectorSearchCount,
        metrics.VectorSearchFailureCount,
        metrics.AverageVectorSearchDurationMs,
        metrics.EmbeddingGenerationCount,
        metrics.ChatCompletionCount
    }));

app.Run();

// --- Request DTOs ---

record VectorSearchRequest(
    string Query,
    int Limit = 10,
    double SimilarityThreshold = 0,
    string? FilterExpression = null);

record ChatRequest(
    string Message,
    string? SystemPrompt = null,
    double Temperature = 0.7,
    int MaxTokens = 1024,
    List<ChatMessageDto>? History = null);

record ChatMessageDto(string Role, string Content);
