using System;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class RoadPauseRulesTests
{
    private const int RollSweep = 500;

    [Fact]
    public void MayPause_OnlyAWalkerWithRoadLeft()
    {
        Assert.True(RoadPauseRules.MayPause(running: false, fighting: false, ghost: false, RoadPauseRules.MinRoadLeftTiles));
        Assert.False(RoadPauseRules.MayPause(running: false, fighting: false, ghost: false, RoadPauseRules.MinRoadLeftTiles - 1));
        Assert.False(RoadPauseRules.MayPause(running: true, fighting: false, ghost: false, RoadPauseRules.MinRoadLeftTiles));
        Assert.False(RoadPauseRules.MayPause(running: false, fighting: true, ghost: false, RoadPauseRules.MinRoadLeftTiles));
        Assert.False(RoadPauseRules.MayPause(running: false, fighting: false, ghost: true, RoadPauseRules.MinRoadLeftTiles));
    }

    [Fact]
    public void WalkBetween_AboutAMinuteOfRoadBetweenStops()
    {
        for (var roll = -RollSweep; roll < RollSweep; roll++)
        {
            var walk = RoadPauseRules.WalkBetween(roll);

            Assert.InRange(walk, TimeSpan.FromSeconds(RoadPauseRules.MinWalkSeconds), TimeSpan.FromSeconds(RoadPauseRules.MaxWalkSeconds));
        }

        Assert.NotEqual(RoadPauseRules.WalkBetween(0), RoadPauseRules.WalkBetween(1));
    }

    [Fact]
    public void PauseLength_AFewSecondsNeverAStall()
    {
        for (var roll = -RollSweep; roll < RollSweep; roll++)
        {
            var pause = RoadPauseRules.PauseLength(roll);

            Assert.InRange(pause, TimeSpan.FromSeconds(RoadPauseRules.MinPauseSeconds), TimeSpan.FromSeconds(RoadPauseRules.MaxPauseSeconds));
        }

        Assert.True(RoadPauseRules.MaxPauseSeconds < RoadPauseRules.MinWalkSeconds);
    }
}
