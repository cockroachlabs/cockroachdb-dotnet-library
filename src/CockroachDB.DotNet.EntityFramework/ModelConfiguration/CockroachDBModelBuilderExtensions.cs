using System.Reflection;
using CockroachDB.DotNet.Abstractions.Attributes;
using Microsoft.EntityFrameworkCore;

namespace CockroachDB.DotNet.EntityFramework.ModelConfiguration;

public static class CockroachDBModelBuilderExtensions
{
    public static ModelBuilder ApplyCockroachDBAnnotations(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (clrType.GetCustomAttribute<GlobalTableAttribute>() is not null)
            {
                modelBuilder.Entity(clrType).ToTable(t =>
                    t.HasComment("GLOBAL TABLE"));
            }

            if (clrType.GetCustomAttribute<RegionalByRowAttribute>() is { } regionalAttr)
            {
                modelBuilder.Entity(clrType).ToTable(t =>
                    t.HasComment($"REGIONAL BY ROW (default: {regionalAttr.DefaultRegion})"));
            }

            foreach (var property in clrType.GetProperties())
            {
                if (property.GetCustomAttribute<CrdbRegionAttribute>() is not null)
                {
                    modelBuilder.Entity(clrType)
                        .Property(property.Name)
                        .HasColumnName("crdb_region");
                }
            }
        }

        return modelBuilder;
    }
}
