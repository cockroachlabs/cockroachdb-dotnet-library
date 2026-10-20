using CockroachDB.DotNet.Demo.Models;
using CockroachDB.DotNet.EntityFramework.ModelConfiguration;
using Microsoft.EntityFrameworkCore;

namespace CockroachDB.DotNet.Demo.Data;

public class DemoDbContext(DbContextOptions<DemoDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyCockroachDBAnnotations();
        base.OnModelCreating(modelBuilder);
    }
}
