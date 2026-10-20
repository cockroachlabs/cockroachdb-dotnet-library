using CockroachDB.DotNet.Core.FollowerRead;

namespace CockroachDB.DotNet.Tests.FollowerRead;

public sealed class FollowerReadContextTests
{
    [Fact]
    public void Default_IsFalse()
    {
        Assert.False(FollowerReadContext.Current);
    }

    [Fact]
    public void Scoped_SetsCurrentToTrue()
    {
        using (FollowerReadContext.Scoped())
        {
            Assert.True(FollowerReadContext.Current);
        }
    }

    [Fact]
    public void Scoped_RestoresPreviousValue_OnDispose()
    {
        Assert.False(FollowerReadContext.Current);

        using (FollowerReadContext.Scoped())
        {
            Assert.True(FollowerReadContext.Current);
        }

        Assert.False(FollowerReadContext.Current);
    }

    [Fact]
    public void NestedScopes_RestoreCorrectly()
    {
        Assert.False(FollowerReadContext.Current);

        using (FollowerReadContext.Scoped())
        {
            Assert.True(FollowerReadContext.Current);

            using (FollowerReadContext.Scoped())
            {
                Assert.True(FollowerReadContext.Current);
            }

            Assert.True(FollowerReadContext.Current);
        }

        Assert.False(FollowerReadContext.Current);
    }

    [Fact]
    public void ManualSet_WorksDirectly()
    {
        FollowerReadContext.Current = true;
        Assert.True(FollowerReadContext.Current);

        FollowerReadContext.Current = false;
        Assert.False(FollowerReadContext.Current);
    }

    [Fact]
    public async Task AsyncLocal_DoesNotLeakAcrossTasks()
    {
        using (FollowerReadContext.Scoped())
        {
            Assert.True(FollowerReadContext.Current);

            var outerTaskResult = await Task.Run(() => FollowerReadContext.Current);
            Assert.True(outerTaskResult);
        }

        Assert.False(FollowerReadContext.Current);
    }
}
