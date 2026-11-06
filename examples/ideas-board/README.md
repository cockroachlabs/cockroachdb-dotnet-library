# Concurrent Ideas Board (.NET)

The .NET twin of the Spring Boot fundamentals demo. It shows the one feature
that makes CockroachDB apps correct under load: **automatic retry on
serialization conflicts (SQLSTATE 40001)** via the library's middleware
pipeline (`ICockroachDBTemplate.ExecuteAsync`).

## What it shows

An idea gets upvoted by many callers at once. Each upvote is a
read-modify-write in a serializable transaction (load → `Votes + 1` → save),
so concurrent callers genuinely contend and CockroachDB raises `40001` on the
losers.

- `IdeaService.UpvoteWithoutRetryAsync` — plain EF save, **no** retry. Under
  contention it loses votes or throws.
- `IdeaService.UpvoteAsync` — the same work wrapped in
  `crdb.ExecuteAsync(async () => { ... })`. The retry middleware absorbs `40001`
  and re-runs the unit of work until it commits, so **every** vote counts.

The run under contention is driven by the library's own
`CockroachDBConcurrencyRunner.RunAsync(threads: 3, iterationsPerThread: 20, ...)`
= 60 concurrent upvotes on one row.

## Prerequisites

- .NET 10 SDK
- Either a local cluster (`cockroach start-single-node --insecure`) for
  `dotnet run`, or Docker for the test (it spins up CockroachDB via
  Testcontainers automatically).

All CockroachDB packages are pinned to **0.1.2** and restored from the
anonymous GitHub Pages feed configured in `NuGet.config`.

## Run the demo

Start a local single-node cluster, then:

```bash
cockroach start-single-node --insecure --background
dotnet run
```

Expected output (numbers vary for the plain path):

```
== Without retry (plain EF save) ==
attempted=60 succeeded=41 failed=19 finalVotes=41
== With retry (crdb.ExecuteAsync) ==
attempted=60 succeeded=60 failed=0 finalVotes=60
```

## Run the test (no local cluster needed)

```bash
dotnet test
```

Testcontainers pulls `cockroachdb/cockroach` and starts a throwaway node. The
test asserts the retry path reaches exactly 60 votes with zero failures, and
that the no-retry path does **not** reliably reach 60.
