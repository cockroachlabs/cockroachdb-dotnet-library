using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations.Internal;

namespace CockroachDB.DotNet.EntityFramework.Migrations;

/// <summary>
/// History repository for CockroachDB that disables migration database locking.
///
/// CockroachDB does not support PostgreSQL advisory locks (pg_advisory_lock)
/// or LOCK TABLE statements. EF Core 10+ requires <see cref="IMigrationsDatabaseLock"/>
/// for concurrent migration safety. This implementation provides a no-op lock,
/// relying on CockroachDB's SERIALIZABLE isolation for conflict handling.
///
/// Migration coordination should happen at the deployment level
/// (e.g., single-pod deploy, migration job, deployment gate).
/// </summary>
public class CockroachDBHistoryRepository : NpgsqlHistoryRepository
{
    public CockroachDBHistoryRepository(HistoryRepositoryDependencies dependencies)
        : base(dependencies)
    {
    }

    public override IMigrationsDatabaseLock AcquireDatabaseLock()
        => new CockroachDBNoopDatabaseLock(this);

    public override Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IMigrationsDatabaseLock>(new CockroachDBNoopDatabaseLock(this));
}

/// <summary>
/// No-op database lock for CockroachDB — advisory locks are not available.
/// </summary>
public sealed class CockroachDBNoopDatabaseLock : IMigrationsDatabaseLock
{
    public CockroachDBNoopDatabaseLock(IHistoryRepository historyRepository)
    {
        HistoryRepository = historyRepository;
    }

    public IHistoryRepository HistoryRepository { get; }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
