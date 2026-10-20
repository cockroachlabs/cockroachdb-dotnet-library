using System.Text;

namespace CockroachDB.DotNet.AI.VectorStore;

public sealed class FilterExpression
{
    public string Key { get; init; } = "";
    public FilterOperator Operator { get; init; } = FilterOperator.Eq;
    public object? Value { get; init; }
}

public enum FilterOperator
{
    Eq, Ne, Gt, Gte, Lt, Lte, In, NotIn
}

public enum FilterLogic
{
    And, Or
}

public sealed class FilterGroup
{
    public FilterLogic Logic { get; init; } = FilterLogic.And;
    public List<FilterExpression> Expressions { get; init; } = [];
    public List<FilterGroup> Groups { get; init; } = [];
}

public static class FilterExpressionConverter
{
    private const string MetadataColumn = "metadata";

    public static string Convert(FilterGroup? group)
    {
        if (group is null || (group.Expressions.Count == 0 && group.Groups.Count == 0))
            return "";

        return ConvertGroup(group);
    }

    public static string Convert(FilterExpression expression)
    {
        return ConvertExpression(expression);
    }

    private static string ConvertGroup(FilterGroup group)
    {
        var parts = new List<string>();

        foreach (var expr in group.Expressions)
            parts.Add(ConvertExpression(expr));

        foreach (var sub in group.Groups)
            parts.Add($"({ConvertGroup(sub)})");

        var joiner = group.Logic == FilterLogic.And ? " AND " : " OR ";
        return string.Join(joiner, parts);
    }

    private static string ConvertExpression(FilterExpression expr)
    {
        var jsonPath = $"{MetadataColumn}->>'{expr.Key}'";

        return expr.Operator switch
        {
            FilterOperator.Eq => $"{jsonPath} = {FormatValue(expr.Value)}",
            FilterOperator.Ne => $"{jsonPath} != {FormatValue(expr.Value)}",
            FilterOperator.Gt => $"({jsonPath})::numeric > {expr.Value}",
            FilterOperator.Gte => $"({jsonPath})::numeric >= {expr.Value}",
            FilterOperator.Lt => $"({jsonPath})::numeric < {expr.Value}",
            FilterOperator.Lte => $"({jsonPath})::numeric <= {expr.Value}",
            FilterOperator.In => $"{jsonPath} IN ({FormatList(expr.Value)})",
            FilterOperator.NotIn => $"{jsonPath} NOT IN ({FormatList(expr.Value)})",
            _ => throw new ArgumentOutOfRangeException(nameof(expr))
        };
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "NULL",
        int or long or float or double or decimal => value.ToString()!,
        bool b => b.ToString().ToLowerInvariant(),
        _ => $"'{value.ToString()!.Replace("'", "''")}'",
    };

    private static string FormatList(object? value)
    {
        if (value is not System.Collections.IEnumerable enumerable)
            return FormatValue(value);

        var sb = new StringBuilder();
        var first = true;
        foreach (var item in enumerable)
        {
            if (!first) sb.Append(", ");
            sb.Append(FormatValue(item));
            first = false;
        }
        return sb.ToString();
    }
}
