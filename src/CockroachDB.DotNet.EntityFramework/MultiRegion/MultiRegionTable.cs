namespace CockroachDB.DotNet.EntityFramework.MultiRegion;

/// <summary>
/// The table of an entity, and what its <c>[GlobalTable]</c> or <c>[RegionalByRow]</c>
/// attribute, if any, expects of it. Names are as CockroachDB stores them: EF Core (via Npgsql)
/// quotes identifiers, so the configured table/column names reach the database verbatim.
/// </summary>
/// <param name="Entity">The entity's full CLR type name, for messages.</param>
/// <param name="Schema">The table's schema, or <c>null</c> for the connection's current schema.</param>
/// <param name="Table">The table's name.</param>
/// <param name="Kind">What the attributes expect; <see cref="MultiRegionKind.None"/> for an entity with neither.</param>
/// <param name="RegionColumn">The column of the entity's <c>[CrdbRegion]</c> property, or <c>null</c>.</param>
internal sealed record MultiRegionTable(
    string Entity,
    string? Schema,
    string Table,
    MultiRegionKind Kind,
    string? RegionColumn);

internal enum MultiRegionKind
{
    None,
    Global,
    RegionalByRow,
    Both
}
