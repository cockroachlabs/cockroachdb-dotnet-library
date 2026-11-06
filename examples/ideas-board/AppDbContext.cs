using CockroachDB.DotNet.EntityFramework.ModelConfiguration;
using Microsoft.EntityFrameworkCore;

namespace IdeasBoard;

// tag::dbcontext[]
public partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Idea> Ideas => Set<Idea>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyCockroachDBAnnotations();
        base.OnModelCreating(modelBuilder);
    }
}
// end::dbcontext[]

// Idempotent DDL matching EF's default mapping of Idea. EnsureCreated() is not
// used here because it no-ops when the database already exists (it never adds
// missing tables), which is exactly the case against a running cluster.
public partial class AppDbContext
{
    public const string CreateTableSql =
        """
        CREATE TABLE IF NOT EXISTS "Ideas" (
            "Id"    uuid    NOT NULL,
            "Text"  text    NOT NULL,
            "Votes" integer NOT NULL,
            CONSTRAINT "PK_Ideas" PRIMARY KEY ("Id")
        );
        """;
}
