using CockroachDB.DotNet.Abstractions.Attributes;

namespace CockroachDB.DotNet.Tests.FollowerRead;

public sealed class FollowerReadStalenessTests
{
    [Fact]
    public void Default_UsesFollowerReadTimestamp()
    {
        var staleness = default(FollowerReadStaleness);

        Assert.True(staleness.IsDefault);
        Assert.Equal("follower_read_timestamp()", staleness.ToAsOfSystemTime());
    }

    [Fact]
    public void AllZeroExplicit_UsesFollowerReadTimestamp()
    {
        var staleness = new FollowerReadStaleness(0, 0, 0);

        Assert.True(staleness.IsDefault);
        Assert.Equal("follower_read_timestamp()", staleness.ToAsOfSystemTime());
    }

    [Theory]
    [InlineData(0, 0, 30, 30, "'-30s'")]
    [InlineData(0, 5, 0, 300, "'-300s'")]
    [InlineData(2, 0, 0, 7200, "'-7200s'")]
    [InlineData(1, 2, 3, 3723, "'-3723s'")] // additive: 3600 + 120 + 3
    public void Additive_ComposesIntoSeconds(long h, long m, long s, long expectedSeconds, string expectedClause)
    {
        var staleness = new FollowerReadStaleness(h, m, s);

        Assert.False(staleness.IsDefault);
        Assert.Equal(expectedSeconds, staleness.TotalSeconds);
        Assert.Equal(expectedClause, staleness.ToAsOfSystemTime());
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(1, -2, 3)]
    public void NegativePart_Throws(long h, long m, long s)
    {
        var staleness = new FollowerReadStaleness(h, m, s);

        Assert.Throws<ArgumentException>(() => staleness.TotalSeconds);
        Assert.Throws<ArgumentException>(() => staleness.ToAsOfSystemTime());
    }

    [Fact]
    public void FromAttribute_ComposesAttributeParts()
    {
        var attribute = new FollowerReadAttribute { HoursAgo = 1, MinutesAgo = 1, SecondsAgo = 1 };

        var staleness = FollowerReadStaleness.FromAttribute(attribute);

        Assert.Equal("'-3661s'", staleness.ToAsOfSystemTime());
        Assert.Equal(staleness, attribute.ToStaleness());
    }
}
