using CockroachDB.DotNet.EntityFramework.Interceptors;
using CockroachDB.DotNet.EntityFramework.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CockroachDB.DotNet.EntityFramework.Extensions;

public static class DbContextOptionsExtensions
{
    /// <summary>
    /// Configures EF Core for CockroachDB with a connection string.
    /// Registers Npgsql, the CockroachDB command interceptor,
    /// migration SQL generator, and no-op migration lock.
    /// </summary>
    public static DbContextOptionsBuilder UseCockroachDB(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString)
    {
        optionsBuilder
            .UseNpgsql(connectionString)
            .AddInterceptors(new CockroachDBCommandInterceptor())
            .ReplaceService<IMigrationsSqlGenerator, CockroachDBMigrationsSqlGenerator>()
            .ReplaceService<IHistoryRepository, CockroachDBHistoryRepository>();

        return optionsBuilder;
    }

    /// <summary>
    /// Configures EF Core for CockroachDB on an already-configured Npgsql connection.
    /// Use when UseNpgsql() was called separately.
    /// </summary>
    public static DbContextOptionsBuilder UseCockroachDB(
        this DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .AddInterceptors(new CockroachDBCommandInterceptor())
            .ReplaceService<IMigrationsSqlGenerator, CockroachDBMigrationsSqlGenerator>()
            .ReplaceService<IHistoryRepository, CockroachDBHistoryRepository>();

        return optionsBuilder;
    }
}
