using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using CockroachDB.DotNet.EntityFramework.Extensions;
using CockroachDB.DotNet.EntityFramework.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace CockroachDB.DotNet.IntegrationTests;

/// <summary>
/// Integration tests that verify EF Core features against a live CockroachDB cluster.
/// Requires: docker-compose -f docker/docker-compose.1node.yml up -d
/// </summary>
[Trait("Category", "Integration")]
public class MigrationIntegrationTests : IAsyncLifetime
{
    private const string ConnStr =
        "Host=localhost;Port=26257;Database=defaultdb;Username=root;SSL Mode=Disable";

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void UseCockroachDB_RegistersMigrationSqlGenerator()
    {
        var options = new DbContextOptionsBuilder()
            .UseCockroachDB(ConnStr)
            .Options;

        using var ctx = new TestDbContext(
            new DbContextOptionsBuilder()
                .UseNpgsql(ConnStr).UseCockroachDB().Options);
        var generator = ctx.GetService<IMigrationsSqlGenerator>();
        Assert.IsType<CockroachDBMigrationsSqlGenerator>(generator);
    }

    [Fact]
    public void UseCockroachDB_RegistersHistoryRepository()
    {
        using var ctx = new TestDbContext(
            new DbContextOptionsBuilder()
                .UseNpgsql(ConnStr).UseCockroachDB().Options);
        var repo = ctx.GetService<IHistoryRepository>();
        Assert.IsType<CockroachDBHistoryRepository>(repo);
    }

    [Fact]
    public void NoopLock_DoesNotThrow()
    {
        using var ctx = new TestDbContext(
            new DbContextOptionsBuilder()
                .UseNpgsql(ConnStr).UseCockroachDB().Options);
        var repo = ctx.GetService<IHistoryRepository>();
        Assert.IsType<CockroachDBHistoryRepository>(repo);

        var crdbRepo = (CockroachDBHistoryRepository)repo;
        using var dbLock = crdbRepo.AcquireDatabaseLock();
        Assert.IsType<CockroachDBNoopDatabaseLock>(dbLock);
    }

    [Fact]
    public async Task CanConnectToCockroachDB()
    {
        await using var conn = new NpgsqlConnection(ConnStr);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT version()";
        var version = (string?)await cmd.ExecuteScalarAsync();

        Assert.NotNull(version);
        Assert.Contains("CockroachDB", version);
    }

    [Fact]
    public async Task CanExecuteCRUD_WithEFCore()
    {
        var dbName = $"test_ef_{Guid.NewGuid():N}".Substring(0, 20);

        await using (var conn = new NpgsqlConnection(ConnStr))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE {dbName}";
            await cmd.ExecuteNonQueryAsync();
        }

        var testConnStr = ConnStr.Replace("Database=defaultdb", $"Database={dbName}");
        var options = new DbContextOptionsBuilder()
            .UseNpgsql(testConnStr).UseCockroachDB()
            .Options;

        try
        {
            await using var setupConn = new NpgsqlConnection(testConnStr);
            await setupConn.OpenAsync();
            await using var createTable = setupConn.CreateCommand();
            createTable.CommandText = @"
                CREATE TABLE products (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    name TEXT NOT NULL,
                    price DECIMAL(10,2) NOT NULL
                )";
            await createTable.ExecuteNonQueryAsync();

            using var ctx = new TestDbContext(options);
            ctx.Products.Add(new Product { Name = "Widget", Price = 9.99m });
            await ctx.SaveChangesAsync();

            var product = await ctx.Products.FirstAsync(p => p.Name == "Widget");
            Assert.Equal(9.99m, product.Price);
        }
        finally
        {
            await using var conn = new NpgsqlConnection(ConnStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE {dbName} CASCADE";
            await cmd.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task ComputedColumn_CalculatesCorrectly()
    {
        var dbName = $"test_comp_{Guid.NewGuid():N}".Substring(0, 20);

        await using (var conn = new NpgsqlConnection(ConnStr))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE {dbName}";
            await cmd.ExecuteNonQueryAsync();
        }

        var testConnStr = ConnStr.Replace("Database=defaultdb", $"Database={dbName}");

        try
        {
            await using var conn = new NpgsqlConnection(testConnStr);
            await conn.OpenAsync();

            await using var createCmd = conn.CreateCommand();
            createCmd.CommandText = @"
                CREATE TABLE orders (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    price INT8 NOT NULL,
                    qty INT8 NOT NULL,
                    total INT8 AS (price * qty) STORED
                )";
            await createCmd.ExecuteNonQueryAsync();

            await using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = "INSERT INTO orders (price, qty) VALUES (10, 5)";
            await insertCmd.ExecuteNonQueryAsync();

            await using var selectCmd = conn.CreateCommand();
            selectCmd.CommandText = "SELECT total FROM orders LIMIT 1";
            var total = (long?)await selectCmd.ExecuteScalarAsync();

            Assert.Equal(50L, total);
        }
        finally
        {
            await using var conn = new NpgsqlConnection(ConnStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE {dbName} CASCADE";
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions options) : base(options) { }
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.Name).HasColumnName("name");
            e.Property(p => p.Price).HasColumnName("price");
        });
    }
}
