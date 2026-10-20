using CockroachDB.DotNet.AI;
using CockroachDB.DotNet.AI.Attributes;

namespace CockroachDB.DotNet.Tests.AI;

public class VectorSearchAttributeTests
{
    [Fact]
    public void Default_Values_Are_Set_Correctly()
    {
        var attr = new VectorSearchAttribute { Table = "documents" };

        Assert.Equal("documents", attr.Table);
        Assert.Equal("embedding", attr.VectorColumn);
        Assert.Equal(DistanceMetric.Cosine, attr.Metric);
        Assert.Equal(10, attr.Limit);
        Assert.Equal("queryVector", attr.QueryParam);
        Assert.Empty(attr.Columns);
        Assert.True(attr.ReadOnly);
        Assert.Equal(0, attr.BeamSize);
    }

    [Fact]
    public void Custom_Values_Override_Defaults()
    {
        var attr = new VectorSearchAttribute
        {
            Table = "products",
            VectorColumn = "vec",
            Metric = DistanceMetric.L2,
            Limit = 5,
            QueryParam = "input",
            Columns = ["id", "name"],
            ReadOnly = false,
            BeamSize = 32
        };

        Assert.Equal("products", attr.Table);
        Assert.Equal("vec", attr.VectorColumn);
        Assert.Equal(DistanceMetric.L2, attr.Metric);
        Assert.Equal(5, attr.Limit);
        Assert.Equal("input", attr.QueryParam);
        Assert.Equal(2, attr.Columns.Length);
        Assert.False(attr.ReadOnly);
        Assert.Equal(32, attr.BeamSize);
    }

    [Fact]
    public void Attribute_Target_Is_Method()
    {
        var usage = typeof(VectorSearchAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }
}

public class SemanticSearchAttributeTests
{
    [Fact]
    public void Default_Values_Include_Filter_And_MaxDistance()
    {
        var attr = new SemanticSearchAttribute { Table = "articles" };

        Assert.Equal("articles", attr.Table);
        Assert.Equal("", attr.Filter);
        Assert.Equal(0, attr.MaxDistance);
        Assert.Equal(DistanceMetric.Cosine, attr.Metric);
    }

    [Fact]
    public void Custom_Values_Override_Defaults()
    {
        var attr = new SemanticSearchAttribute
        {
            Table = "articles",
            Filter = "category = :category AND active = true",
            MaxDistance = 0.5,
            Metric = DistanceMetric.InnerProduct,
            Limit = 20,
            BeamSize = 16
        };

        Assert.Equal("category = :category AND active = true", attr.Filter);
        Assert.Equal(0.5, attr.MaxDistance);
        Assert.Equal(DistanceMetric.InnerProduct, attr.Metric);
        Assert.Equal(20, attr.Limit);
        Assert.Equal(16, attr.BeamSize);
    }
}

public class VectorColumnAttributeTests
{
    [Fact]
    public void Dimensions_Is_Required()
    {
        var attr = new VectorColumnAttribute { Dimensions = 1536 };
        Assert.Equal(1536, attr.Dimensions);
    }

    [Fact]
    public void Attribute_Target_Is_Property()
    {
        var usage = typeof(VectorColumnAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Property, usage.ValidOn);
    }
}
