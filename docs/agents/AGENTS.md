# CockroachDB .NET Library — guide for coding agents

You are editing a .NET app that depends on `CockroachDB.DotNet.*`. This library already
handles the hard CockroachDB-specific concerns through a composable middleware pipeline.
**Use the provided helpers; do not re-implement them.** Full detail: [README.md](../../README.md).

## Already handled — do NOT rebuild

- **Serialization-retry on SQLSTATE `40001`.** Automatic. Wrap work in
  `ICockroachDBTemplate.ExecuteAsync(...)` (or register `pipeline.UseRetry()`). Never hand-roll
  a retry loop or catch `40001` yourself.
- **Multi-region startup check.** Registering a context via the EF-layer
  `AddCockroachDBDbContext<T>()` / `AddCockroachDBDbContextFactory<T>()` turns on an on-by-default
  check of `[GlobalTable]`/`[RegionalByRow]`/`[CrdbRegion]` vs the live schema. It lives in the
  **EF layer, not Core** — raw Npgsql/Core users get no check and no EF dependency.
- **Metrics, Continuum, CDC plumbing** are provided. Don't reimplement.

## Which helper for each job

| Job | Use |
|-----|-----|
| Retried transaction | `crdb.ExecuteAsync(...)` (`ICockroachDBTemplate`) |
| Follower (stale) read | `crdb.ExecuteFollowerReadAsync(...)` or `using FollowerReadContext.Scoped()` |
| Read further back | `new FollowerReadStaleness(SecondsAgo: 30)` (negative values throw) |
| Pin to a region | `crdb.InRegion("us-east1").ExecuteAsync(...)` or `using RegionContext.Scoped("eu-west1")` |
| Regional-by-row table | `[RegionalByRow]` + `[CrdbRegion]` string column; `[GlobalTable]` for global |
| Register EF context | `AddCockroachDBDbContext<AppDbContext>(o => o.UseCockroachDB(cs))` |
| CDC | `AddCockroachDBCdc()` + `IChangefeedHandler<T>` + `MapCockroachDBChangefeed<T>(...)` |
| Vector search | `VectorSearchTemplate` / `CockroachDBVectorStore` (`AddCockroachDBAI`) |
| Continuum / MCP | `CockroachDB.DotNet.Continuum.*` (`HttpCloudClient`, `HttpMcpClient`) |
| Testing | `CockroachDB.DotNet.Testing` (`RetrySimulator`, `ConcurrencyRunner`, `CockroachDBFixture`) |

Regional-by-row homing is the **`crdb_region` COLUMN** set through the `[CrdbRegion]` property.
There is no `SET crdb_region` session statement — never emit one.

## Config that matters (`appsettings.json`, section `CockroachDB`)

`Retry:MaxRetries`, `Retry:DelayMs`; `Region:Resolver` + `Region:HeaderName`
(`X-CockroachDB-Region`, when web-integrated); `MultiRegion:Mode` (`Warn`/`Validate`/`None` —
set `None` to disable the startup check); follower staleness via `FollowerReadStaleness`.

## Anti-patterns — do NOT

- Hand-roll a `40001` retry loop — the pipeline does it.
- Emit a raw `SET crdb_region = '...'` statement — use the `[CrdbRegion]` column.
- Write changefeed/webhook plumbing by hand — use the CDC helpers.
- **Leave Npgsql on its default READ COMMITTED isolation.** Under READ COMMITTED, `40001`
  never fires, so the retry middleware silently no-ops. Force Serializable (CockroachDB's
  default) — e.g. `crdb.ExecuteAsync(...)` runs Serializable; only call
  `ExecuteReadCommittedAsync(...)` when you deliberately want relaxed isolation.
- **AI vector search:** do not open a reader and then commit in the same transaction (an early
  AI release did this and threw `NpgsqlOperationInProgressException`). Query the
  `NpgsqlDataSource` directly for vector search.
