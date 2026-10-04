using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// Ursel Walsh and Kenric the Just set out from the Magincia bank for another town, met the
/// red camp on the road to the moongate, ran home, and set out on the same road again every
/// two minutes: the trip check read only the goal and the midpoint, both far from the camp.
/// </summary>
public class TownTripDangerTests
{
    private const uint Ursel = 0x7E0101;
    private const uint Kenric = 0x7E0102;
    private const uint Loiterer = 0x7E0103;
    private const int FarTiles = 200;

    private static readonly DateTime Start = new(2026, 9, 28, 12, 45, 19, DateTimeKind.Utc);
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private static readonly Point3D MaginciaBank = new(3690, 2153, 20);
    private static readonly Point3D CampRoad = new(3651, 2097, 25);
    private static readonly Point3D MaginciaGate = new(3563, 2139, 20);
    private static readonly Point3D BritainGate = new(1336, 1997, 5);
    private static readonly Point3D BritainBank = new(1434, 1699, 0);
    private static readonly Point3D RedCamp = new(3645, 2085, 25);

    public TownTripDangerTests() => TestMap.EnsureInternal();

    [Fact]
    public void IsLegNearAny_FindsASpotBesideTheMiddleOfALongLeg()
    {
        var from = new Point3D(0, 0, 0);
        var to = new Point3D(FarTiles, 0, 0);
        var beside = new Point3D(FarTiles / 2, NavSearch.AvoidRadiusTiles / 2, 0);

        Assert.False(NavSearch.IsNearAny(from, [beside]));
        Assert.False(NavSearch.IsNearAny(to, [beside]));
        Assert.True(NavSearch.IsLegNearAny(from, to, [beside], from));
    }

    [Fact]
    public void IsLegNearAny_FarSpotOrNoSpots_IsFalse()
    {
        var from = new Point3D(0, 0, 0);
        var to = new Point3D(FarTiles, 0, 0);
        var far = new Point3D(FarTiles / 2, NavSearch.AvoidRadiusTiles * 2, 0);

        Assert.False(NavSearch.IsLegNearAny(from, to, [far], from));
        Assert.False(NavSearch.IsLegNearAny(from, to, [], from));
        Assert.False(NavSearch.IsLegNearAny(from, to, null, from));
    }

    [Fact]
    public void WalksNear_TheMaginciaRoadPassesTheCampThatGoalAndMidpointMiss()
    {
        var mid = new Point3D((MaginciaBank.X + BritainBank.X) / 2, (MaginciaBank.Y + BritainBank.Y) / 2, MaginciaBank.Z);
        Assert.False(NavSearch.IsNearAny(BritainBank, [RedCamp]));
        Assert.False(NavSearch.IsNearAny(mid, [RedCamp]));

        Assert.True(TravelPlan.WalksNear(MaginciaBank, MaginciaToBritain(), [RedCamp]));
    }

    [Fact]
    public void WalksNear_AMoongateHopIsNotWalked()
    {
        // The camp lies on the straight line between the two moongates, not on either road.
        var onTheHop = new Point3D((MaginciaGate.X + BritainGate.X) / 2, (MaginciaGate.Y + BritainGate.Y) / 2, 0);
        TravelStep[] byGate =
        [
            new("MaginciaGate", MaginciaGate, NavGateKind.None),
            new("BritainGate", BritainGate, NavGateKind.Moongate),
            new("BritainBank", BritainBank, NavGateKind.None)
        ];

        Assert.False(TravelPlan.WalksNear(MaginciaGate, byGate, [onTheHop]));
        Assert.True(TravelPlan.WalksNear(MaginciaGate, byGate, [BritainGate]));
    }

    /// <summary>
    /// A run ends about fourteen tiles from the nearest foe, inside the radius of the place run
    /// from. Every road from there read as a road past it, and the walk home or to the dungeon
    /// door ended at the first step after the run, whichever way it led.
    /// </summary>
    [Fact]
    public void WalksNear_AWalkerClearOfAFight_ReadsOnlyTheRoadBackTowardIt()
    {
        var fight = new Point3D(1260, 890, 0);
        var clear = new Point3D(fight.X, fight.Y + RetreatRules.ClearTiles, 0);
        TravelStep[] away = [new("South", new Point3D(clear.X, clear.Y + FarTiles, 0), NavGateKind.None)];
        TravelStep[] back = [new("North", new Point3D(clear.X, fight.Y - FarTiles, 0), NavGateKind.None)];

        Assert.True(NavSearch.IsNearAny(clear, [fight]));
        Assert.False(TravelPlan.WalksNear(clear, away, [fight]));
        Assert.True(TravelPlan.WalksNear(clear, back, [fight]));
    }

    [Fact]
    public void ComesNearer_OnlyASpotInsideTheRadiusAndNearerThanTheWalker()
    {
        var spot = new Point3D(0, 0, 0);
        var walker = new Point3D(NavSearch.AvoidRadiusTiles / 2, 0, 0);

        Assert.False(NavSearch.ComesNearer(walker, walker, [spot]));
        Assert.False(NavSearch.ComesNearer(walker with { X = walker.X + 1 }, walker, [spot]));
        Assert.True(NavSearch.ComesNearer(walker with { X = walker.X - 1 }, walker, [spot]));
        Assert.False(NavSearch.ComesNearer(new Point3D(NavSearch.AvoidRadiusTiles + 1, 0, 0), new Point3D(FarTiles, 0, 0), [spot]));
        Assert.True(NavSearch.ComesNearer(new Point3D(NavSearch.AvoidRadiusTiles, 0, 0), new Point3D(FarTiles, 0, 0), [spot]));
        Assert.False(NavSearch.ComesNearer(spot, walker, null));
    }

    [Fact]
    public void OnFoot_ATileRouteIsReadAsAWalkedRoad()
    {
        Point3D[] route = [CampRoad, MaginciaGate];
        var steps = TravelPlan.OnFoot(route);

        Assert.Equal(route.Length, steps.Count);
        Assert.All(steps, step => Assert.Equal(NavGateKind.None, step.ArrivalGate));
        Assert.True(TravelPlan.WalksNear(MaginciaBank, steps, [RedCamp]));
        Assert.Empty(TravelPlan.OnFoot(null));
    }

    [Fact]
    public void WalksNear_NoDangerOrNoPlan_IsFalse()
    {
        Assert.False(TravelPlan.WalksNear(MaginciaBank, MaginciaToBritain(), []));
        Assert.False(TravelPlan.WalksNear(MaginciaBank, MaginciaToBritain(), null));
        Assert.False(TravelPlan.WalksNear(MaginciaBank, null, [RedCamp]));
    }

    [Fact]
    public void RestsAfterGivingUp_LastsAsLongAsTheDangerIsRemembered()
    {
        Assert.Equal(SpotMemory.Memory, TownTripRules.GivenUpRest);
        Assert.False(TownTripRules.RestsAfterGivingUp(default, Start));
        Assert.True(TownTripRules.RestsAfterGivingUp(Start, Start));
        Assert.True(TownTripRules.RestsAfterGivingUp(Start, Start + TownTripRules.GivenUpRest - OneSecond));
        Assert.False(TownTripRules.RestsAfterGivingUp(Start, Start + TownTripRules.GivenUpRest));
    }

    [Fact]
    public void TownTripGivenUp_EachBankHasItsOwnClock()
    {
        Assert.NotEqual(RuleClock.TownTripGivenUp(BritainBank), RuleClock.TownTripGivenUp(MaginciaBank));
        Assert.Equal(RuleClock.TownTripGivenUp(BritainBank), RuleClock.TownTripGivenUp(BritainBank));
    }

    [Fact]
    public void GiveUpForThreat_OnATownTrip_RestsItsBankAndStopsTheTrip()
    {
        var ursel = Person(Ursel);
        var routine = new Routine([new TownTrip(BritainBank)]);
        TestRoutine.Give(ursel, routine);

        TownTrip.GiveUpForThreat(ursel, Start);

        Assert.True(routine.NeedsNext);
        Assert.Equal(Start, ursel.ClockAt(RuleClock.TownTripGivenUp(BritainBank)));
        Assert.True(TownTripRules.RestsAfterGivingUp(ursel.ClockAt(RuleClock.TownTripGivenUp(BritainBank)), Start + OneSecond));
        Assert.Equal(default(DateTime), ursel.ClockAt(RuleClock.TownTripGivenUp(MaginciaBank)));
    }

    [Fact]
    public void GiveUpForThreat_OnOtherWork_OnlyStopsIt()
    {
        var loiterer = Person(Loiterer);
        var routine = new Routine([new ArrivalStay()]);
        TestRoutine.Give(loiterer, routine);

        TownTrip.GiveUpForThreat(loiterer, Start);

        Assert.True(routine.NeedsNext);
        Assert.Empty(loiterer.RuleClocks);
    }

    [Fact]
    public void GiveUpForThreat_NoRoutineOrNobody_DoesNothing()
    {
        var kenric = Person(Kenric);

        TownTrip.GiveUpForThreat(kenric, Start);
        TownTrip.GiveUpForThreat(null, Start);

        Assert.Empty(kenric.RuleClocks);
    }

    [Fact]
    public void TownTrip_IsATravelStepToItsBank()
    {
        var trip = new TownTrip(BritainBank);

        Assert.Equal(SkillKinds.Travel, trip.Name);
        Assert.Equal(BritainBank, trip.Goal);
    }

    /// <summary>The road the planner found: past the camp to the moongate, through it, and on to the bank.</summary>
    private static TravelStep[] MaginciaToBritain() =>
    [
        new("MaginciaBank", MaginciaBank, NavGateKind.None),
        new("CampRoad", CampRoad, NavGateKind.None),
        new("MaginciaGate", MaginciaGate, NavGateKind.None),
        new("BritainGate", BritainGate, NavGateKind.Moongate),
        new("BritainBank", BritainBank, NavGateKind.None)
    ];

    private static SosariaCharacter Person(uint serial) => new((Serial)serial);
}
