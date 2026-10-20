using CockroachDB.DotNet.AI.VectorStore;

namespace CockroachDB.DotNet.Tests.AI;

public class FilterExpressionConverterTests
{
    [Fact]
    public void Null_Group_Returns_Empty_String()
    {
        Assert.Equal("", FilterExpressionConverter.Convert((FilterGroup?)null));
    }

    [Fact]
    public void Empty_Group_Returns_Empty_String()
    {
        var group = new FilterGroup();
        Assert.Equal("", FilterExpressionConverter.Convert(group));
    }

    [Fact]
    public void Single_Eq_String_Expression()
    {
        var expr = new FilterExpression { Key = "category", Operator = FilterOperator.Eq, Value = "science" };
        var result = FilterExpressionConverter.Convert(expr);

        Assert.Equal("metadata->>'category' = 'science'", result);
    }

    [Fact]
    public void Single_Eq_Numeric_Expression()
    {
        var expr = new FilterExpression { Key = "year", Operator = FilterOperator.Gt, Value = 2020 };
        var result = FilterExpressionConverter.Convert(expr);

        Assert.Equal("(metadata->>'year')::numeric > 2020", result);
    }

    [Fact]
    public void And_Group_With_Two_Expressions()
    {
        var group = new FilterGroup
        {
            Logic = FilterLogic.And,
            Expressions =
            [
                new() { Key = "category", Operator = FilterOperator.Eq, Value = "tech" },
                new() { Key = "year", Operator = FilterOperator.Gte, Value = 2023 }
            ]
        };
        var result = FilterExpressionConverter.Convert(group);

        Assert.Equal("metadata->>'category' = 'tech' AND (metadata->>'year')::numeric >= 2023", result);
    }

    [Fact]
    public void Or_Group_With_Two_Expressions()
    {
        var group = new FilterGroup
        {
            Logic = FilterLogic.Or,
            Expressions =
            [
                new() { Key = "status", Operator = FilterOperator.Eq, Value = "active" },
                new() { Key = "status", Operator = FilterOperator.Eq, Value = "pending" }
            ]
        };
        var result = FilterExpressionConverter.Convert(group);

        Assert.Equal("metadata->>'status' = 'active' OR metadata->>'status' = 'pending'", result);
    }

    [Fact]
    public void In_Operator_With_String_List()
    {
        var expr = new FilterExpression
        {
            Key = "tag",
            Operator = FilterOperator.In,
            Value = new List<string> { "ai", "ml", "data" }
        };
        var result = FilterExpressionConverter.Convert(expr);

        Assert.Equal("metadata->>'tag' IN ('ai', 'ml', 'data')", result);
    }

    [Fact]
    public void NotIn_Operator()
    {
        var expr = new FilterExpression
        {
            Key = "region",
            Operator = FilterOperator.NotIn,
            Value = new List<string> { "us-east1", "us-west2" }
        };
        var result = FilterExpressionConverter.Convert(expr);

        Assert.Equal("metadata->>'region' NOT IN ('us-east1', 'us-west2')", result);
    }

    [Fact]
    public void Nested_Groups()
    {
        var group = new FilterGroup
        {
            Logic = FilterLogic.And,
            Expressions =
            [
                new() { Key = "active", Operator = FilterOperator.Eq, Value = true }
            ],
            Groups =
            [
                new()
                {
                    Logic = FilterLogic.Or,
                    Expressions =
                    [
                        new() { Key = "category", Operator = FilterOperator.Eq, Value = "tech" },
                        new() { Key = "category", Operator = FilterOperator.Eq, Value = "science" }
                    ]
                }
            ]
        };
        var result = FilterExpressionConverter.Convert(group);

        Assert.Equal(
            "metadata->>'active' = true AND (metadata->>'category' = 'tech' OR metadata->>'category' = 'science')",
            result);
    }

    [Fact]
    public void String_With_Single_Quotes_Is_Escaped()
    {
        var expr = new FilterExpression { Key = "name", Operator = FilterOperator.Eq, Value = "O'Brien" };
        var result = FilterExpressionConverter.Convert(expr);

        Assert.Equal("metadata->>'name' = 'O''Brien'", result);
    }

    [Fact]
    public void Ne_Operator()
    {
        var expr = new FilterExpression { Key = "status", Operator = FilterOperator.Ne, Value = "deleted" };
        var result = FilterExpressionConverter.Convert(expr);

        Assert.Equal("metadata->>'status' != 'deleted'", result);
    }

    [Fact]
    public void Lt_And_Lte_Operators()
    {
        var lt = new FilterExpression { Key = "score", Operator = FilterOperator.Lt, Value = 100 };
        var lte = new FilterExpression { Key = "score", Operator = FilterOperator.Lte, Value = 100 };

        Assert.Equal("(metadata->>'score')::numeric < 100", FilterExpressionConverter.Convert(lt));
        Assert.Equal("(metadata->>'score')::numeric <= 100", FilterExpressionConverter.Convert(lte));
    }
}
