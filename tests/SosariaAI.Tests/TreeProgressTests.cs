using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class TreeProgressTests
{
    [Fact]
    public void Observe_WhileHarvesting_DoesNotGiveUp()
    {
        var progress = new TreeProgress();
        progress.Reset();

        const int logCount = 3;
        var observeCount = TreeProgress.TreeGiveUpTicks * 2;
        for (var i = 0; i < observeCount; i++)
        {
            Assert.False(progress.Observe(logCount, harvesting: true));
        }
    }

    [Fact]
    public void Observe_NoProgressTicks_GivesUpAtTen()
    {
        var progress = new TreeProgress();
        progress.Reset();

        const int logCount = 4;
        Assert.False(progress.Observe(logCount, harvesting: false));

        for (var i = 0; i < TreeProgress.TreeGiveUpTicks - 1; i++)
        {
            Assert.False(progress.Observe(logCount, harvesting: false));
        }

        Assert.True(progress.Observe(logCount, harvesting: false));
    }

    [Fact]
    public void Observe_LogCountGrowth_ResetsCounter()
    {
        var progress = new TreeProgress();
        progress.Reset();

        const int startLogCount = 2;
        const int grownLogCount = 5;
        var ticksBeforeGrowth = TreeProgress.TreeGiveUpTicks / 2;

        Assert.False(progress.Observe(startLogCount, harvesting: false));

        for (var i = 0; i < ticksBeforeGrowth; i++)
        {
            Assert.False(progress.Observe(startLogCount, harvesting: false));
        }

        Assert.False(progress.Observe(grownLogCount, harvesting: false));

        for (var i = 0; i < TreeProgress.TreeGiveUpTicks - 1; i++)
        {
            Assert.False(progress.Observe(grownLogCount, harvesting: false));
        }

        Assert.True(progress.Observe(grownLogCount, harvesting: false));
    }

    [Fact]
    public void Reset_AfterGiveUp_StartsFresh()
    {
        var progress = new TreeProgress();
        progress.Reset();

        const int logCount = 1;
        Assert.False(progress.Observe(logCount, harvesting: false));

        for (var i = 0; i < TreeProgress.TreeGiveUpTicks - 1; i++)
        {
            Assert.False(progress.Observe(logCount, harvesting: false));
        }

        Assert.True(progress.Observe(logCount, harvesting: false));

        progress.Reset();

        Assert.False(progress.Observe(logCount, harvesting: false));

        for (var i = 0; i < TreeProgress.TreeGiveUpTicks - 1; i++)
        {
            Assert.False(progress.Observe(logCount, harvesting: false));
        }

        Assert.True(progress.Observe(logCount, harvesting: false));
    }

    [Fact]
    public void Observe_GrowthWhileHarvesting_ResetsCounter()
    {
        var progress = new TreeProgress();
        progress.Reset();

        const int startLogCount = 1;
        const int grownLogCount = 4;
        var ticksBeforeGrowth = TreeProgress.TreeGiveUpTicks / 2;

        Assert.False(progress.Observe(startLogCount, harvesting: false));

        for (var i = 0; i < ticksBeforeGrowth; i++)
        {
            Assert.False(progress.Observe(startLogCount, harvesting: false));
        }

        Assert.False(progress.Observe(grownLogCount, harvesting: true));

        for (var i = 0; i < TreeProgress.TreeGiveUpTicks - 1; i++)
        {
            Assert.False(progress.Observe(grownLogCount, harvesting: false));
        }

        Assert.True(progress.Observe(grownLogCount, harvesting: false));
    }
}
