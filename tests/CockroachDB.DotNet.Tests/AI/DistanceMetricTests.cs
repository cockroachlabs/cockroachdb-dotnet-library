using CockroachDB.DotNet.AI;

namespace CockroachDB.DotNet.Tests.AI;

public class DistanceMetricTests
{
    [Theory]
    [InlineData(DistanceMetric.L2, "<->")]
    [InlineData(DistanceMetric.Cosine, "<=>")]
    [InlineData(DistanceMetric.InnerProduct, "<#>")]
    public void ToOperator_Returns_Correct_PgVector_Operator(DistanceMetric metric, string expected)
    {
        Assert.Equal(expected, metric.ToOperator());
    }

    [Fact]
    public void Cosine_ToSimilarity_Subtracts_From_One()
    {
        var similarity = DistanceMetric.Cosine.ToSimilarity(0.3);
        Assert.Equal(0.7, similarity, precision: 10);
    }

    [Fact]
    public void L2_ToSimilarity_Returns_Inverse()
    {
        var similarity = DistanceMetric.L2.ToSimilarity(1.0);
        Assert.Equal(0.5, similarity, precision: 10);
    }

    [Fact]
    public void L2_ToSimilarity_Zero_Distance_Returns_One()
    {
        var similarity = DistanceMetric.L2.ToSimilarity(0.0);
        Assert.Equal(1.0, similarity, precision: 10);
    }

    [Fact]
    public void InnerProduct_ToSimilarity_Negates_Distance()
    {
        var similarity = DistanceMetric.InnerProduct.ToSimilarity(0.5);
        Assert.Equal(-0.5, similarity, precision: 10);
    }

    [Theory]
    [InlineData(DistanceMetric.L2, "vector_l2_ops")]
    [InlineData(DistanceMetric.Cosine, "vector_cosine_ops")]
    [InlineData(DistanceMetric.InnerProduct, "vector_ip_ops")]
    public void ToOpclass_Returns_Correct_V26_2_Opclass(DistanceMetric metric, string expected)
    {
        Assert.Equal(expected, metric.ToOpclass());
    }

    [Fact]
    public void ToOpclass_Throws_For_Invalid_Metric()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((DistanceMetric)99).ToOpclass());
    }
}
