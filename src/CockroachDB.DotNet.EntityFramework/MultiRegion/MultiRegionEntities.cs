using System.Collections.Generic;
using System.Reflection;
using CockroachDB.DotNet.Abstractions.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CockroachDB.DotNet.EntityFramework.MultiRegion;

/// <summary>
/// Lists every entity in an EF Core model with its table and multi-region attribute, if any.
/// Attributes are read from the CLR type; table and column names come from the model, so they
/// reflect the application's configured names and EF Core's quoting.
/// </summary>
internal static class MultiRegionEntities
{
    internal static List<MultiRegionTable> Read(IModel model)
    {
        var tables = new List<MultiRegionTable>();
        foreach (var entityType in model.GetEntityTypes())
        {
            var table = entityType.GetTableName();
            if (table is null)
            {
                continue;
            }

            var clrType = entityType.ClrType;
            var global = clrType.GetCustomAttribute<GlobalTableAttribute>() is not null;
            var regionalByRow = clrType.GetCustomAttribute<RegionalByRowAttribute>() is not null;
            var kind = global && regionalByRow
                ? MultiRegionKind.Both
                : global
                    ? MultiRegionKind.Global
                    : regionalByRow
                        ? MultiRegionKind.RegionalByRow
                        : MultiRegionKind.None;

            var schema = entityType.GetSchema();
            var regionColumn = kind == MultiRegionKind.RegionalByRow ? RegionColumn(entityType) : null;

            tables.Add(new MultiRegionTable(clrType.FullName ?? clrType.Name, schema, table, kind, regionColumn));
        }

        return tables;
    }

    private static string? RegionColumn(IEntityType entityType)
    {
        foreach (var property in entityType.ClrType.GetProperties())
        {
            if (property.GetCustomAttribute<CrdbRegionAttribute>() is null)
            {
                continue;
            }

            var mapped = entityType.FindProperty(property.Name);
            if (mapped is null)
            {
                return null;
            }

            var store = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table);
            return store is { } so ? mapped.GetColumnName(so) : mapped.GetColumnName();
        }

        return null;
    }
}
