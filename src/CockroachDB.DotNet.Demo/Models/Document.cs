using CockroachDB.DotNet.AI.Attributes;

namespace CockroachDB.DotNet.Demo.Models;

public class Document
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = "";
    public string Category { get; set; } = "";

    /// <summary>
    /// Tenant identifier — used as a prefix column in v26.2 vector indexes
    /// for multi-tenant workloads.
    /// </summary>
    public string TenantId { get; set; } = "default";

    [VectorColumn(Dimensions = 1536)]
    public float[]? Embedding { get; set; }
}
