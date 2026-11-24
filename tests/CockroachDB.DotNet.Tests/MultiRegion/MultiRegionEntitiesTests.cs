using System;
using CockroachDB.DotNet.Abstractions.Attributes;
using CockroachDB.DotNet.Core.MultiRegion;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CockroachDB.DotNet.Tests.MultiRegion;

/// <summary>
/// Verifies the EF Core model walk: attributes resolve to the right <see cref="MultiRegionKind"/>,
/// and the <c>[CrdbRegion]</c> property resolves to its configured column name. The model is built
/// without opening a connection (as in RegionHomingTests).
/// </summary>
public class MultiRegionEntitiesTests
{
    [GlobalTable]
    private sealed class GlobalThing
    {
        public Guid Id { get; set; }
    }

    [RegionalByRow]
    private sealed class RbrThing
    {
        public Guid Id { get; set; }
        [CrdbRegion] public string Region { get; set; } = "";
    }

    [RegionalByRow]
    private sealed class RbrCustomColumnThing
    {
        public Guid Id { get; set; }
        [CrdbRegion] public string Home { get; set; } = "";
    }

    private sealed class PlainThing
    {
        public Guid Id { get; set; }
    }

    private sealed class ModelDbContext : DbContext
    {
        public DbSet<GlobalThing> Globals => Set<GlobalThing>();
        public DbSet<RbrThing> Rbrs => Set<RbrThing>();
        public DbSet<RbrCustomColumnThing> Customs => Set<RbrCustomColumnThing>();
        public DbSet<PlainThing> Plains => Set<PlainThing>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseNpgsql("Host=localhost;Database=none");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GlobalThing>(e => { e.ToTable("globals"); e.HasKey(t => t.Id); });
            modelBuilder.Entity<RbrThing>(e =>
            {
                e.ToTable("accounts");
                e.HasKey(t => t.Id);
                e.Property(t => t.Region).HasColumnName("crdb_region");
            });
            modelBuilder.Entity<RbrCustomColumnThing>(e =>
            {
                e.ToTable("orders");
                e.HasKey(t => t.Id);
                e.Property(t => t.Home).HasColumnName("home_region");
            });
            modelBuilder.Entity<PlainThing>(e => { e.ToTable("plains"); e.HasKey(t => t.Id); });
        }
    }

    [Fact]
    public void Read_ResolvesKindsAndRegionColumns()
    {
        using var ctx = new ModelDbContext();

        var tables = MultiRegionEntities.Read(ctx.Model);

        var global = Assert.Single(tables, t => t.Table == "globals");
        Assert.Equal(MultiRegionKind.Global, global.Kind);
        Assert.Null(global.RegionColumn);

        var rbr = Assert.Single(tables, t => t.Table == "accounts");
        Assert.Equal(MultiRegionKind.RegionalByRow, rbr.Kind);
        Assert.Equal("crdb_region", rbr.RegionColumn);

        var custom = Assert.Single(tables, t => t.Table == "orders");
        Assert.Equal(MultiRegionKind.RegionalByRow, custom.Kind);
        Assert.Equal("home_region", custom.RegionColumn);

        var plain = Assert.Single(tables, t => t.Table == "plains");
        Assert.Equal(MultiRegionKind.None, plain.Kind);
    }
}
