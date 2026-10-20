namespace CockroachDB.DotNet.Abstractions.Attributes;

/// <summary>
/// Marks an entity property as a CockroachDB computed (generated) column.
/// CockroachDB requires computed columns to be STORED — virtual columns
/// are not supported.
///
/// <example>
/// <code>
/// public class Order
/// {
///     public int Price { get; set; }
///     public int Quantity { get; set; }
///
///     [CockroachDBComputed("price * quantity")]
///     public int Total { get; set; }
/// }
/// </code>
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CockroachDBComputedAttribute : Attribute
{
    /// <summary>SQL expression for the computed column.</summary>
    public string Expression { get; }

    public CockroachDBComputedAttribute(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        Expression = expression;
    }
}
