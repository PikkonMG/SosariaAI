using System;
using Server;
using Server.Mobiles;
using SosariaAI.Common;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class CommonRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Rest = TimeSpan.FromMinutes(10);

    [Fact]
    public void Rested_IsTrueWhenNothingHappenedYet() => Assert.True(TimeRules.Rested(default, Now, Rest));

    [Fact]
    public void Rested_WaitsTheWholeRest()
    {
        Assert.False(TimeRules.Rested(Now - Rest + TimeSpan.FromSeconds(1), Now, Rest));
        Assert.True(TimeRules.Rested(Now - Rest, Now, Rest));
    }

    [Fact]
    public void Passed_IsFalseWhenNothingStarted() => Assert.False(TimeRules.Passed(default, Now, Rest));

    [Fact]
    public void Passed_TurnsTrueOnceTheSpanIsOver()
    {
        Assert.False(TimeRules.Passed(Now - Rest + TimeSpan.FromSeconds(1), Now, Rest));
        Assert.True(TimeRules.Passed(Now - Rest, Now, Rest));
    }

    [Fact]
    public void HitsFraction_IsHitsOverMaximum()
    {
        // A new mobile has its maximum but no hits yet; setting hits starts the engine's regen timer.
        TestMap.EnsureInternal();
        var mobile = new PlayerMobile((Serial)0x7E01);

        Assert.True(mobile.HitsMax > 0);
        Assert.Equal((double)mobile.Hits / mobile.HitsMax, Vitals.HitsFraction(mobile), 3);
        Assert.True(Vitals.HitsFraction(mobile) < Vitals.FullHits);
    }

    [Fact]
    public void HitsFraction_CountsANullMobileAsUnhurt() => Assert.Equal(Vitals.FullHits, Vitals.HitsFraction(null));

    [Fact]
    public void InWorld_NeedsARealMap()
    {
        TestMap.EnsureInternal();

        Assert.False(People.InWorld(null));
        Assert.False(People.InWorld(new PlayerMobile((Serial)0x7E02)));

        var internalMobile = new SosariaCharacter((Serial)0x7E03);
        internalMobile.DefaultMobileInit();
        internalMobile.Map = Map.Internal;
        Assert.False(People.InWorld(internalMobile));
    }
}
