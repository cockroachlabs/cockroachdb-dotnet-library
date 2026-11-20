using System.Collections.Concurrent;
using System.Reflection;
using CockroachDB.DotNet.Abstractions.Attributes;
using CockroachDB.DotNet.Core.Region;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CockroachDB.DotNet.EntityFramework.Interceptors;

/// <summary>
/// Homes REGIONAL BY ROW rows by stamping the <c>crdb_region</c> column from the
/// ambient <see cref="RegionContext"/> on INSERT. CockroachDB has no
/// <c>crdb_region</c> session variable, so a row is homed by writing the column —
/// not with <c>SET</c>. The region flows in from <c>InRegion(...)</c> (via
/// <c>RegionMiddleware</c>) or an explicit <see cref="RegionContext.Scoped"/>.
/// No-op when no region is scoped or the entity has no <c>[CrdbRegion]</c>
/// property. Added entities only — updates never silently relocate existing rows.
/// </summary>
public sealed class CrdbRegionSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> RegionProperties = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            StampRegions(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            StampRegions(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // Internal for the region-homing unit test (teeth-check).
    internal static void StampRegions(DbContext context)
    {
        var region = RegionContext.Current;
        if (string.IsNullOrEmpty(region))
            return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added)
                continue;

            var prop = RegionProperties.GetOrAdd(entry.Entity.GetType(), FindCrdbRegionProperty);
            if (prop is not null && entry.Metadata.FindProperty(prop.Name) is not null)
                entry.Property(prop.Name).CurrentValue = region;
        }
    }

    private static PropertyInfo? FindCrdbRegionProperty(Type type)
    {
        foreach (var p in type.GetProperties())
            if (p.GetCustomAttribute<CrdbRegionAttribute>() is not null)
                return p;
        return null;
    }
}
