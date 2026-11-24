using System.Collections.Generic;

namespace CockroachDB.DotNet.EntityFramework.MultiRegion;

/// <summary>
/// What the multi-region check reads from CockroachDB.
/// </summary>
/// <param name="Database">The current database.</param>
/// <param name="CurrentSchema">The schema that unqualified table names resolve to.</param>
/// <param name="PrimaryRegion">The database's primary region, or <c>null</c> when it is not multi-region.</param>
/// <param name="ClusterRegions">The regions of the cluster's nodes, empty when no node has a region locality.</param>
/// <param name="Tables">The tables the check looks at that exist, by schema and name.</param>
/// <param name="Keywords">Keywords that must be quoted when used as an identifier.</param>
internal sealed record MultiRegionState(
    string Database,
    string CurrentSchema,
    string? PrimaryRegion,
    IReadOnlyList<string> ClusterRegions,
    IReadOnlyDictionary<TableName, TableInfo> Tables,
    IReadOnlySet<string> Keywords);

internal sealed record TableName(string Schema, string Table);

/// <param name="Locality">As <c>SHOW TABLES</c> reports it, <c>null</c> outside a multi-region database.</param>
/// <param name="ColumnTypes">
/// The table's column types (<c>information_schema.columns.crdb_sql_type</c>), read only for the
/// columns the check needs.
/// </param>
internal sealed record TableInfo(string? Locality, IReadOnlyDictionary<string, string> ColumnTypes);
