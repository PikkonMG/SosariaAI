using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class ApproachProgressTests
{
    [Fact]
    public void Observe_GettingCloserNeverGivesUp()
    {
        var progress = new ApproachProgress();
        var distance = 100.0;

        for (var i = 0; i < ApproachProgress.GiveUpTicks * 3; i++)
        {
            distance -= 0.5;
            Assert.False(progress.Observe(distance));
        }
    }

    [Fact]
    public void Observe_PacingAtSameDistanceGivesUpAfterLimit()
    {
        var progress = new ApproachProgress();
        Assert.False(progress.Observe(10));

        for (var i = 0; i < ApproachProgress.GiveUpTicks - 1; i++)
        {
            Assert.False(progress.Observe(10));
        }

        Assert.True(progress.Observe(10));
    }

    [Fact]
    public void Observe_DetourFartherThenCloserResets()
    {
        var progress = new ApproachProgress();
        Assert.False(progress.Observe(10));

        for (var i = 0; i < ApproachProgress.GiveUpTicks / 2; i++)
        {
            Assert.False(progress.Observe(12));
        }

        Assert.False(progress.Observe(9));

        for (var i = 0; i < ApproachProgress.GiveUpTicks - 1; i++)
        {
            Assert.False(progress.Observe(9));
        }

        Assert.True(progress.Observe(9));
    }

    [Fact]
    public void Reset_StartsFresh()
    {
        var progress = new ApproachProgress();

        for (var i = 0; i < ApproachProgress.GiveUpTicks; i++)
        {
            progress.Observe(10);
        }

        progress.Reset();

        Assert.False(progress.Observe(10));
    }
}
