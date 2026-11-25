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

## Learn CockroachDB fundamentals

This library handles the .NET side; these CockroachDB docs explain the database underneath. The
links below go to the Markdown version of each page, which is easier for an agent to read; drop the
`.md` for the HTML page. Every docs page has one, and <https://docs.cockroachlabs.com/llms.txt>
indexes the whole documentation site.

**Start here**

- [Developer Basics](https://docs.cockroachlabs.com/docs/stable/developer-basics.md): what's different about building on CockroachDB.
- [PostgreSQL Compatibility](https://docs.cockroachlabs.com/docs/stable/postgresql-compatibility.md): CockroachDB speaks the PostgreSQL wire protocol (this library uses Npgsql), with some differences.
- [Connection Pooling](https://docs.cockroachlabs.com/docs/stable/connection-pooling.md): sizing an `NpgsqlDataSource`/pool.
- [Common Errors and Solutions](https://docs.cockroachlabs.com/docs/stable/common-errors.md)

**Transactions and contention**

- [Transactions](https://docs.cockroachlabs.com/docs/stable/transactions.md): SERIALIZABLE by default, and why clients see retry errors.
- [Transaction Retry Error Reference](https://docs.cockroachlabs.com/docs/stable/transaction-retry-error-reference.md): what each `40001` (`RETRY_SERIALIZABLE`, `RETRY_WRITE_TOO_OLD`, ...) means. The pipeline retries them; this tells you why they happen.
- [Reduce transaction contention](https://docs.cockroachlabs.com/docs/stable/performance-best-practices-overview.md#reduce-transaction-contention): the checklist. Keep transactions small, limit the rows they touch, use `SELECT ... FOR UPDATE` for read-then-write, `UPSERT` whole rows.
- [Understand Hotspots](https://docs.cockroachlabs.com/docs/stable/understand-hotspots.md)
- [Performance Tuning Recipes](https://docs.cockroachlabs.com/docs/stable/performance-recipes.md)
- [`SELECT FOR UPDATE`](https://docs.cockroachlabs.com/docs/stable/select-for-update.md)
- [Read Committed Transactions](https://docs.cockroachlabs.com/docs/stable/read-committed.md): an alternative isolation level, with trade-offs. Note the Npgsql READ COMMITTED warning above — don't switch to it just to avoid retries.

**Schema design**

- [Database Schemas](https://docs.cockroachlabs.com/docs/stable/schema-design-overview.md): design and change schemas with a migration tool rather than through the ORM.
- [Unique ID best practices](https://docs.cockroachlabs.com/docs/stable/performance-best-practices-overview.md#unique-id-best-practices): prefer `UUID` keys (`gen_random_uuid()`) over sequences or auto-increment, which create hotspots.
- [`UUID`](https://docs.cockroachlabs.com/docs/stable/uuid.md) and [Hash-sharded Indexes](https://docs.cockroachlabs.com/docs/stable/hash-sharded-indexes.md).
- [Online Schema Changes](https://docs.cockroachlabs.com/docs/stable/online-schema-changes.md)

**Multi-region**

- [Multi-Region Capabilities Overview](https://docs.cockroachlabs.com/docs/stable/multiregion-overview.md)
- [Table Localities](https://docs.cockroachlabs.com/docs/stable/table-localities.md): `REGIONAL BY TABLE`, `REGIONAL BY ROW` and `GLOBAL`, which `[RegionalByRow]` and `[GlobalTable]` map to.
- [How to Choose a Multi-Region Configuration](https://docs.cockroachlabs.com/docs/stable/choosing-a-multi-region-configuration.md)
- [Follower Reads](https://docs.cockroachlabs.com/docs/stable/follower-reads.md) and [`AS OF SYSTEM TIME`](https://docs.cockroachlabs.com/docs/stable/as-of-system-time.md), behind `ExecuteFollowerReadAsync`.

**Change data capture**

- [`CREATE CHANGEFEED`](https://docs.cockroachlabs.com/docs/stable/create-changefeed.md) and [Changefeed Sinks](https://docs.cockroachlabs.com/docs/stable/changefeed-sinks.md) (the webhook sink is what the CDC helpers receive).

**Vector search**

- [`VECTOR`](https://docs.cockroachlabs.com/docs/stable/vector.md) and [Vector Indexes](https://docs.cockroachlabs.com/docs/stable/vector-indexes.md)
