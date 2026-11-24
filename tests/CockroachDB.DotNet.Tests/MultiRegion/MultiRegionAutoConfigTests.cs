using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CockroachDB.DotNet.Core.Extensions;
using CockroachDB.DotNet.Core.MultiRegion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CockroachDB.DotNet.Tests.MultiRegion;

/// <summary>
/// The startup multi-region check is on by default: calling <c>AddCockroachDB</c> registers it for
/// every EF Core context, with no explicit <c>AddCockroachDBMultiRegionCheck</c> call.
/// <c>CockroachDB:MultiRegion:Mode=none</c> turns it off, and a non-CockroachDB datasource is a quiet
/// no-op. These run without Docker; the live-cluster path is covered by the integration tests.
/// </summary>
public class MultiRegionAutoConfigTests
{
    private sealed class SqliteThing
    {
        public Guid Id { get; set; }
    }

    private sealed class SqliteDbContext(DbContextOptions<SqliteDbContext> options) : DbContext(options)
    {
        public DbSet<SqliteThing> Things => Set<SqliteThing>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SqliteThing>(e => { e.ToTable("things"); e.HasKey(t => t.Id); });
    }

    private static IConfiguration Config(string? mode = null)
    {
        var values = new Dictionary<string, string?>();
        if (mode is not null)
        {
            values["CockroachDB:MultiRegion:Mode"] = mode;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static ServiceProvider Build(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddCockroachDB(configuration);
        services.AddDbContext<SqliteDbContext>(o => o.UseSqlite("DataSource=:memory:"));
        return services.BuildServiceProvider();
    }

    private static MultiRegionStartupCheck Check(IServiceProvider provider)
        => provider.GetServices<IHostedService>().OfType<MultiRegionStartupCheck>().Single();

    [Fact]
    public void AddCockroachDB_RegistersCheck_WithoutExplicitCall()
    {
        // Only AddCockroachDB + a DbContext -- no AddCockroachDBMultiRegionCheck.
        using var provider = Build(Config());

        Assert.NotNull(Check(provider));
    }

    [Fact]
    public async Task ModeNone_DisablesCheck()
    {
        using var provider = Build(Config("None"));

        // With mode=none the check short-circuits before touching any context or database.
        await Check(provider).StartAsync(default);
    }

    [Fact]
    public async Task NonCockroachDBDatasource_IsQuietNoOp()
    {
        // Default (warn) mode against SQLite: SELECT version() is not CockroachDB, so the check skips
        // quietly rather than throwing.
        using var provider = Build(Config());

        await Check(provider).StartAsync(default);
    }
}
