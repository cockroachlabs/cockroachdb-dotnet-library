namespace CockroachDB.DotNet.Continuum.Plenum;

/// <summary>
/// Cockroach Plenum storage signal for a single table. <see cref="Rows"/> and
/// <see cref="Bytes"/> are best-effort: they are <c>null</c> when the running
/// CockroachDB version does not expose that signal, rather than failing.
/// </summary>
/// <param name="Schema">The table's schema name.</param>
/// <param name="Table">The table name.</param>
/// <param name="Rows">Estimated row count, or <c>null</c> if unavailable.</param>
/// <param name="Bytes">Approximate on-disk size in bytes, or <c>null</c> if unavailable.</param>
public sealed record TableStorage(string Schema, string Table, long? Rows, long? Bytes)
{
    /// <summary>The <c>schema.table</c> qualified name.</summary>
    public string QualifiedName => $"{Schema}.{Table}";
}
