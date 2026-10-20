using CockroachDB.DotNet.AI.Template;

namespace CockroachDB.DotNet.Tests.AI;

public class VectorSearchTemplateTests
{
    [Fact]
    public void ToVectorLiteral_Formats_Correctly()
    {
        var vector = new float[] { 1.0f, 2.5f, 3.14f };
        var result = VectorSearchTemplate.ToVectorLiteral(vector);

        Assert.Equal("[1,2.5,3.14]", result);
    }

    [Fact]
    public void ToVectorLiteral_Single_Element()
    {
        var vector = new float[] { 0.5f };
        var result = VectorSearchTemplate.ToVectorLiteral(vector);

        Assert.Equal("[0.5]", result);
    }

    [Fact]
    public void ToVectorLiteral_Empty_Array_Returns_Empty_Brackets()
    {
        var result = VectorSearchTemplate.ToVectorLiteral([]);
        Assert.Equal("[]", result);
    }

    [Fact]
    public void ToVectorLiteral_Negative_Values()
    {
        var vector = new float[] { -1.0f, 0f, 1.0f };
        var result = VectorSearchTemplate.ToVectorLiteral(vector);

        Assert.Equal("[-1,0,1]", result);
    }

    [Fact]
    public void ToVectorLiteral_High_Dimensional()
    {
        var vector = Enumerable.Range(0, 1536).Select(i => (float)i / 1536).ToArray();
        var result = VectorSearchTemplate.ToVectorLiteral(vector);

        Assert.StartsWith("[", result);
        Assert.EndsWith("]", result);
        Assert.Equal(1535, result.Count(c => c == ','));
    }
}
