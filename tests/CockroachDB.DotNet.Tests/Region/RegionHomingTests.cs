using System;
using CockroachDB.DotNet.Abstractions.Attributes;
using CockroachDB.DotNet.Core.Region;
using CockroachDB.DotNet.EntityFramework.Interceptors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CockroachDB.DotNet.Tests.Region;

/// <summary>
/// Teeth-check for the multi-region homing fix: rows home via the <c>crdb_region</c>
/// COLUMN, never via <c>SET crdb_region</c> (which CockroachDB rejects — 42704, there
/// is no such session variable). Reverting either half of the fix turns a test red.
/// </summary>
public class RegionHomingTests
{
    private sealed class RegionalThing
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = "";
        [CrdbRegion] public string Region { get; set; } = "";
    }

    private sealed class TestDbContext : DbContext
    {
        public DbSet<RegionalThing> Things => Set<RegionalThing>();

        // A dummy connection string; the tests never open a connection — they call
        // StampRegions directly (it runs before any DB I/O) and only read the
        // ChangeTracker, so no CockroachDB is required.
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseNpgsql("Host=localhost;Database=none");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RegionalThing>().HasKey(t => t.Id);
    }

    [Fact]
    public void SaveChangesInterceptor_StampsCrdbRegionColumn_FromScopedRegion()
    {
        using var ctx = new TestDbContext();
        var thing = new RegionalThing { Id = Guid.NewGuid(), Text = "home me", Region = "" };
        ctx.Add(thing);

        using (RegionContext.Scoped("us-east1"))
            CrdbRegionSaveChangesInterceptor.StampRegions(ctx);

        // The resolved region was written onto the crdb_region column — this is what
        // homes the row. Teeth: with the stamping removed, Region stays "".
        Assert.Equal("us-east1", thing.Region);
    }

    [Fact]
    public void CommandInterceptor_NeverEmitsSetCrdbRegion()
    {
        var command = new NpgsqlCommand("INSERT INTO \"RegionalThings\" DEFAULT VALUES");

        using (RegionContext.Scoped("us-east1"))
            CockroachDBCommandInterceptor.ApplyRegionPrefix(command);

        // Teeth: with the SET crdb_region prefix restored, this assertion fails.
        Assert.DoesNotContain("SET crdb_region", command.CommandText, StringComparison.OrdinalIgnoreCase);
    }
}
