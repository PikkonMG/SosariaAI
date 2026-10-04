using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class LoiterPaceRulesTests
{
    private const int RollSweep = 500;

    /// <summary>The lightest stay weight: a stroller who stands only a few seconds.</summary>
    private const int LightStayWeight = 1;
    private static readonly Point3D Home = new(1449, 1723, 6);

    [Fact]
    public void Dwell_StandsForSecondsNotOneThink()
    {
        // The old idle walk stepped on most thinks: a quarter second between steps.
        for (var roll = -RollSweep; roll < RollSweep; roll++)
        {
            Assert.True(LoiterPaceRules.Dwell(IdleWanderSkill.WanderChanceToNotMove, roll) >= TimeSpan.FromSeconds(LoiterPaceRules.MinDwellSeconds));
            Assert.True(LoiterPaceRules.Dwell(LightStayWeight, roll) >= TimeSpan.FromSeconds(LoiterPaceRules.MinDwellSeconds));
        }
    }

    [Fact]
    public void Dwell_LongerForAHeavierStayWeight()
    {
        var lingerShortest = LoiterPaceRules.Dwell(ArrivalRules.StandStayChance, 0);
        var wanderLongest = LoiterPaceRules.Dwell(
            LightStayWeight,
            LoiterPaceRules.MinDwellSeconds * LoiterPaceRules.DwellSpread
        );

        Assert.True(lingerShortest > wanderLongest);
        Assert.Equal(
            TimeSpan.FromSeconds(LoiterPaceRules.StandStillCheckSeconds),
            LoiterPaceRules.Dwell(LoiterPaceRules.StandStill, 0)
        );
    }

    [Fact]
    public void MustReturn_OnlyOutsideTheRing()
    {
        var edge = new Point3D(Home.X + 3, Home.Y, Home.Z);
        var beyond = new Point3D(Home.X + 4, Home.Y, Home.Z);

        Assert.False(LoiterPaceRules.MustReturn(edge, Home, 3));
        Assert.True(LoiterPaceRules.MustReturn(beyond, Home, 3));
        Assert.False(LoiterPaceRules.MustReturn(beyond, Point3D.Zero, 3));
        Assert.True(LoiterPaceRules.MustReturn(edge, Home, 0));
    }

    [Fact]
    public void PinnedToHome_ZeroRingNeverStrolls()
    {
        Assert.True(LoiterPaceRules.PinnedToHome(Home, 0));
        Assert.False(LoiterPaceRules.PinnedToHome(Home, 1));
        Assert.False(LoiterPaceRules.PinnedToHome(Point3D.Zero, 0));
    }

    [Fact]
    public void StrollGoal_StaysInTheRingAndLeavesTheTile()
    {
        const int ring = 4;

        for (var roll = 0; roll < RollSweep; roll++)
        {
            var goal = LoiterPaceRules.StrollGoal(Home, Home, ring, roll, roll * 7 + 3);

            Assert.False(goal.X == Home.X && goal.Y == Home.Y);
            Assert.True(NavMetric.Chebyshev(goal, Home) <= ring);

            var free = LoiterPaceRules.StrollGoal(Home, Point3D.Zero, ring, roll, roll * 7 + 3);
            Assert.True(NavMetric.Chebyshev(free, Home) <= LoiterPaceRules.FreeStrollRadius);
        }
    }

    [Fact]
    public void StepBudget_GrowsWithTheWalk()
    {
        var near = new Point3D(Home.X + 1, Home.Y, Home.Z);
        var far = new Point3D(Home.X + 8, Home.Y, Home.Z);

        Assert.True(LoiterPaceRules.StepBudget(Home, far) > LoiterPaceRules.StepBudget(Home, near));
        Assert.True(LoiterPaceRules.StepBudget(Home, near) > LoiterPaceRules.StepsPerTile);
    }

    [Fact]
    public void ShouldFace_FollowsThePercent()
    {
        Assert.True(LoiterPaceRules.ShouldFace(0));
        Assert.False(LoiterPaceRules.ShouldFace(LoiterPaceRules.FacePercent));
    }
    [Fact]
    public void StayLength_SpreadsRoundTheAuthoredStay()
    {
        // Every loiter of the last run ended on the same three minutes to the second.
        var authored = TimeSpan.FromMinutes(3);
        var shortest = TimeSpan.MaxValue;
        var longest = TimeSpan.Zero;

        for (var roll = -RollSweep; roll < RollSweep; roll++)
        {
            var stay = LoiterPaceRules.StayLength(authored, roll);
            shortest = stay < shortest ? stay : shortest;
            longest = stay > longest ? stay : longest;
        }

        Assert.Equal(authored * LoiterPaceRules.StayMinPercent / LoiterPaceRules.PercentScale, shortest);
        Assert.Equal(authored * LoiterPaceRules.StayMaxPercent / LoiterPaceRules.PercentScale, longest);
        Assert.True(shortest < authored && authored < longest);
    }

    [Fact]
    public void StayLength_NoAuthoredStayStaysNone() =>
        Assert.Equal(TimeSpan.Zero, LoiterPaceRules.StayLength(TimeSpan.Zero, RollSweep));
}
