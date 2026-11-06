using CockroachDB.DotNet.AI;
using CockroachDB.DotNet.AI.Extensions;
using CockroachDB.DotNet.AI.VectorStore;
using CockroachDB.DotNet.Cdc.Extensions;
using CockroachDB.DotNet.Core.Extensions;
using CockroachDB.DotNet.EntityFramework.Extensions;
using CockroachDB.DotNet.Testing;
using IdeasBoard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("CockroachDB")
    ?? "Host=localhost;Port=26257;Database=defaultdb;Username=root;SSL Mode=Disable;";

// RegionMiddleware resolves the data source to discover the gateway region.
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));

// tag::runner[]
builder.Services.AddCockroachDB(builder.Configuration, pipeline =>
{
    // tag::metrics-config[]
    // Enables transaction/retry/follower-read metrics. UseMetrics() registers an
    // ICockroachDBMetrics singleton that MetricsMiddleware updates on every pipeline
    // operation; read it back via GetRequiredService<ICockroachDBMetrics>().
    pipeline.UseMetrics();
    // end::metrics-config[]
    pipeline.UseCircuitBreaker();
    pipeline.UseRetry();
    pipeline.UseRegion();
    pipeline.UseFollowerRead();
});
// end::runner[]

// tag::cdc-register[]
// CDC: the webhook endpoint (MapCockroachDBChangefeed) would deserialize
// CockroachDB's changefeed POST body and dispatch it to IdeaChangefeedHandler.
builder.Services.AddCockroachDBCdc();
builder.Services.AddChangefeedHandler<Idea, IdeaChangefeedHandler>();
// end::cdc-register[]

// A factory, not a scoped DbContext: the concurrency runner hits the service
// from many threads at once, and each needs its own (non-thread-safe) context.
builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseCockroachDB(connectionString));
builder.Services.AddSingleton<IdeaService>();
builder.Services.AddSingleton<IdeaReadService>();

// tag::vector-register[]
// Semantic search: register the AI services and a deterministic (model-free)
// embedding generator, then a VECTOR store on a 3-dim column with an L2 vector
// index. SemanticIdeaService stores precomputed embeddings and searches by raw
// vector, so results are reproducible without any model.
builder.Services.AddCockroachDBAI(builder.Configuration);
builder.Services.AddSingleton<IEmbeddingGenerator, ZeroEmbeddingGenerator>();
builder.Services.AddCockroachDBVectorStore(b => b
    .WithDimensions(3)
    .WithDistanceMetric(DistanceMetric.L2)
    .WithInitializeSchema(true)
    .WithCreateVectorIndex());
builder.Services.AddSingleton<SemanticIdeaService>();
// end::vector-register[]

// Multi-region writes (meaningful only against a multi-region cluster).
builder.Services.AddSingleton<RegionalIdeaService>();

using var host = builder.Build();

await using (var db = await host.Services
    .GetRequiredService<IDbContextFactory<AppDbContext>>()
    .CreateDbContextAsync())
{
    await db.Database.ExecuteSqlRawAsync(AppDbContext.CreateTableSql);
}

var service = host.Services.GetRequiredService<IdeaService>();
var ideaId = await service.SeedAsync("Add dark mode");

// tag::runner[]
// 3 threads x 20 iterations = 60 concurrent upvotes on ONE row.
Console.WriteLine("== Without retry (plain EF save) ==");
await service.ResetVotesAsync(ideaId);
var plain = await CockroachDBConcurrencyRunner.RunAsync(
    threads: 3,
    iterationsPerThread: 20,
    () => service.UpvoteWithoutRetryAsync(ideaId));
Console.WriteLine(
    $"attempted={plain.TotalOperations} succeeded={plain.Successes} " +
    $"failed={plain.Failures} finalVotes={await service.CurrentVotesAsync(ideaId)}");

Console.WriteLine("== With retry (crdb.ExecuteAsync) ==");
await service.ResetVotesAsync(ideaId);
var retried = await CockroachDBConcurrencyRunner.RunAsync(
    threads: 3,
    iterationsPerThread: 20,
    () => service.UpvoteAsync(ideaId));
Console.WriteLine(
    $"attempted={retried.TotalOperations} succeeded={retried.Successes} " +
    $"failed={retried.Failures} finalVotes={await service.CurrentVotesAsync(ideaId)}");
// end::runner[]

// Leaderboard via a follower read (AS OF SYSTEM TIME follower_read_timestamp()).
// Slightly stale by design; vote counts applied in the last ~4.2s may lag.
Console.WriteLine("== Leaderboard (follower read) ==");
var readService = host.Services.GetRequiredService<IdeaReadService>();
foreach (var idea in await readService.LeaderboardAsync())
    Console.WriteLine($"{idea.Votes,4}  {idea.Text}");
