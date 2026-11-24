# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Startup multi-region check** (opt-in via `services.AddCockroachDBMultiRegionCheck<TContext>()`).
  Compares `[GlobalTable]` / `[RegionalByRow]` / `[CrdbRegion]` attributes against the live
  CockroachDB schema and database multi-region config, logging mismatches with fix-up SQL
  (`ALTER DATABASE ... SET PRIMARY REGION/ADD REGION`, `ALTER TABLE ... SET LOCALITY ...`). Reads
  only (never mutates the schema), uses `SHOW` + `information_schema` (avoids `crdb_internal`),
  quotes keyword identifiers, and skips quietly when the database is not CockroachDB.
  Behaviour is driven by `CockroachDB:MultiRegion:Mode` (`MultiRegionOptions.Mode`), default `Warn`.
  A **contradiction** (an entity whose attribute names one locality while its table has the other,
  or an entity carrying both attributes) fails startup even in `Warn`; `Validate` fails startup on
  any mismatch; `None` turns the check off. Unlike the Spring starter the check is opt-in, so it
  never changes startup behaviour for existing apps that do not register it.

## [0.1.2] - 2026-10-28

Follower reads can now target a specific staleness, and a bug that caused the
middleware path to silently serve current data has been fixed.

### Added
- `HoursAgo`, `MinutesAgo` and `SecondsAgo` on `[FollowerRead]`. They add up and
  compose to `AS OF SYSTEM TIME '-Ns'`; all-zero keeps `follower_read_timestamp()`.
- `FollowerReadStaleness` readonly record struct (composition + validation; inlines
  only validated integers into SQL).
- `ICockroachDBTemplate.ExecuteFollowerReadAsync(..., FollowerReadStaleness)`
  overloads, and `FollowerReadContext.Scoped(FollowerReadStaleness)` with a
  `CurrentStaleness` ambient value.

### Fixed
- **Follower reads via `FollowerReadMiddleware` silently served current data.** The
  middleware opened a throwaway connection, ran
  `SET TRANSACTION AS OF SYSTEM TIME` on it, disposed it, then executed the user's
  operation on a *different* pooled connection that never received the `SET` — while
  still recording a successful follower read. The middleware now activates the
  ambient `FollowerReadContext` for the duration of the operation, so the
  operation's EF queries receive the `SET` on their own connection via
  `CockroachDBCommandInterceptor`.
- Negative staleness now throws (metered as a failed follower read) instead of
  producing an invalid clause.

### Changed
- `FollowerReadMiddleware`'s constructor no longer takes `NpgsqlDataSource`
  (internal DI type; no effect on consumers using the DI extensions).
- `CockroachDBCommandInterceptor` derives the `AS OF SYSTEM TIME` clause from the
  current staleness instead of a hardcoded `follower_read_timestamp()`.

### Known limitation
- No scanner consumes `[FollowerRead]` on methods yet; the wired paths are
  `template.ExecuteFollowerReadAsync(...)` and `FollowerReadContext.Scoped(...)`.
  The AsyncLocal/interceptor binding covers EF commands — a raw `NpgsqlCommand`
  inside a template operation must set the clause itself.

[0.1.2]: https://github.com/cockroachlabs/cockroachdb-dotnet-library/releases/tag/v0.1.2
