namespace CockroachDB.DotNet.Continuum.Plenum;

/// <summary>
/// Read-only Cockroach Plenum storage insights: usage-based storage signals per
/// table using only customer-safe surfaces.
/// </summary>
public interface IStorageInsights
{
    /// <summary>Summarize storage for every table in the current database.</summary>
    Task<IReadOnlyList<TableStorage>> SummaryAsync(CancellationToken cancellationToken = default);
}
