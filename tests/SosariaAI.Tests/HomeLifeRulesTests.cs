using System;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class HomeLifeRulesTests
{
    private const double Healthy = 1.0;
    private const double Wounded = HomeLifeRules.HurtHits - 0.1;
    private const int NearHome = HomeLifeRules.HurtHomeTiles;
    private const int FarFromHome = HomeLifeRules.HurtHomeTiles + 1;

    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0);

    [Fact]
    public void Due_NeverWithoutAHouse() =>
        Assert.False(HomeLifeRules.Due(hasHouse: false, hasStash: true, default, Now, Wounded, NearHome));

    [Fact]
    public void Due_AnOwnerThatNeverStayedGoesHome() =>
        Assert.True(HomeLifeRules.Due(hasHouse: true, hasStash: false, default, Now, Healthy, FarFromHome));

    [Fact]
    public void Due_NotSoonAfterTheLastStay()
    {
        var lastStay = Now - HomeLifeRules.StashEvery + TimeSpan.FromMinutes(1);
        Assert.False(HomeLifeRules.Due(hasHouse: true, hasStash: true, lastStay, Now, Wounded, NearHome));
    }

    [Fact]
    public void Due_PiecesToPutAwayOnceTheStashRestRan()
    {
        var lastStay = Now - HomeLifeRules.StashEvery;
        Assert.True(HomeLifeRules.Due(hasHouse: true, hasStash: true, lastStay, Now, Healthy, FarFromHome));
        Assert.False(HomeLifeRules.Due(hasHouse: true, hasStash: false, lastStay, Now, Healthy, FarFromHome));
    }

    [Fact]
    public void Due_ARestOnceTheRestSpanRan() =>
        Assert.True(HomeLifeRules.Due(hasHouse: true, hasStash: false, Now - HomeLifeRules.RestEvery, Now, Healthy, FarFromHome));

    [Fact]
    public void Due_HurtOnlyWithHomeNear()
    {
        var lastStay = Now - HomeLifeRules.StashEvery;
        Assert.True(HomeLifeRules.Due(hasHouse: true, hasStash: false, lastStay, Now, Wounded, NearHome));
        Assert.False(HomeLifeRules.Due(hasHouse: true, hasStash: false, lastStay, Now, Wounded, FarFromHome));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue - 1)]
    [InlineData(-7)]
    public void StayLength_BetweenTheShortestAndLongestStay(int roll)
    {
        var stay = HomeLifeRules.StayLength(roll);
        Assert.InRange(stay, HomeLifeRules.StayMin, HomeLifeRules.StayMax);
    }

    [Fact]
    public void ReadyToLeave_OnceTheStayRanAndHealed()
    {
        var stay = HomeLifeRules.StayMin;
        Assert.False(HomeLifeRules.ReadyToLeave(stay - TimeSpan.FromSeconds(1), stay, Healthy, poisoned: false, guestInside: false));
        Assert.True(HomeLifeRules.ReadyToLeave(stay, stay, Healthy, poisoned: false, guestInside: false));
    }

    [Theory]
    [InlineData(Wounded, false, false)]
    [InlineData(Healthy, true, false)]
    [InlineData(Healthy, false, true)]
    public void ReadyToLeave_WoundsPoisonOrAGuestKeepItIn(double hits, bool poisoned, bool guestInside)
    {
        var stay = HomeLifeRules.StayMin;
        Assert.False(HomeLifeRules.ReadyToLeave(stay, stay, hits, poisoned, guestInside));
        Assert.True(HomeLifeRules.ReadyToLeave(HomeLifeRules.StayLimit, stay, hits, poisoned, guestInside));
    }
}
