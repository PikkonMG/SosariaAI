using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class FollowWalkTests
{
    [Fact]
    public void NeedsNav_UsesSoftLegDistance()
    {
        Assert.Equal(NavLimits.SoftLegDistance, FollowWalk.NavThreshold);
        Assert.False(FollowWalk.NeedsNav(NavLimits.SoftLegDistance));
        Assert.False(FollowWalk.NeedsNav(0));
        Assert.True(FollowWalk.NeedsNav(NavLimits.SoftLegDistance + 1));
        Assert.True(FollowWalk.NeedsNav(600));
    }

    [Fact]
    public void ShouldRetryRoute_WaitsThenAllowsAnotherTry()
    {
        Assert.False(FollowWalk.ShouldRetryRoute(0));
        Assert.False(FollowWalk.ShouldRetryRoute(FollowWalk.RetryRouteTicks - 1));
        Assert.True(FollowWalk.ShouldRetryRoute(FollowWalk.RetryRouteTicks));
    }

    [Fact]
    public void ShouldReplan_WaitsOutTheRetryEvenWhenTheLeaderMoves()
    {
        // A follower with no route threw its wait away every tick because the leader
        // had moved one tile, and asked the graph again: one "no route" line a second.
        Assert.False(FollowWalk.ShouldReplan(waitingForRoute: true, destChanged: true, ticksWaited: 1));
        Assert.False(FollowWalk.ShouldReplan(waitingForRoute: true, destChanged: false, ticksWaited: FollowWalk.RetryRouteTicks - 1));
        Assert.True(FollowWalk.ShouldReplan(waitingForRoute: true, destChanged: true, ticksWaited: FollowWalk.RetryRouteTicks));
        Assert.True(FollowWalk.ShouldReplan(waitingForRoute: false, destChanged: true, ticksWaited: 0));
        Assert.False(FollowWalk.ShouldReplan(waitingForRoute: false, destChanged: false, ticksWaited: 0));
    }

    [Fact]
    public void MayTryRecall_LooksAgainOnlyAfterTheRetry()
    {
        var now = new System.DateTime(2026, 1, 1, 12, 0, 0);

        Assert.True(FollowWalk.MayTryRecall(default, now));
        Assert.False(FollowWalk.MayTryRecall(now, now + FollowWalk.RecallRetry - System.TimeSpan.FromSeconds(1)));
        Assert.True(FollowWalk.MayTryRecall(now, now + FollowWalk.RecallRetry));
    }
}
