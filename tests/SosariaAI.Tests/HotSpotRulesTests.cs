using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class HotSpotRulesTests
{
    private const int Slots = 200;
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0);

    [Theory]
    [InlineData(true, HotSpotRules.RedReachTiles, false, true)]
    [InlineData(true, HotSpotRules.RedReachTiles + 1, false, false)]
    [InlineData(false, 10, false, false)]
    [InlineData(false, HotSpotRules.RedReachTiles * 2, true, true)]
    public void InReach_AWalkableSpotNearEnough_OrOneARuneLandsBy(bool walkable, int distance, bool recallable, bool inReach) =>
        Assert.Equal(inReach, HotSpotRules.InReach(walkable, distance, recallable));

    [Fact]
    public void GateIsHotSpot_AnUnguardedPadWithOpenGroundRoundIt()
    {
        // MoongateGuards leaves six of the eight Felucca pads without guards, as in 1999, and
        // the gate's traffic, not its guards, makes the spot.
        Assert.True(HotSpotRules.GateIsHotSpot(inBucsDen: false, padGuarded: false, HotSpotRules.MinOpenCamps));
        Assert.True(HotSpotRules.GateIsHotSpot(inBucsDen: false, padGuarded: false, HotSpotRules.CampDirections));
        Assert.False(HotSpotRules.GateIsHotSpot(inBucsDen: false, padGuarded: false, HotSpotRules.MinOpenCamps - 1));
    }

    [Fact]
    public void GateIsHotSpot_NeverAGuardedPad() =>
        // Britain and Moonglow kept their guards: a red that stepped off a run there died.
        Assert.False(HotSpotRules.GateIsHotSpot(inBucsDen: false, padGuarded: true, HotSpotRules.CampDirections));

    [Fact]
    public void GateIsHotSpot_NeverTheDensOwnGate() =>
        // The Den is the reds' home, where no red strikes: its gate is no camp for a run.
        Assert.False(HotSpotRules.GateIsHotSpot(inBucsDen: true, padGuarded: false, HotSpotRules.CampDirections));

    [Fact]
    public void YewGate_OutweighsEveryOtherSpot_AndHoldsTheMostHouses()
    {
        var yew = HotSpotRules.WeightOf(HotSpotKind.Moongate, yewGate: true);

        foreach (var kind in Enum.GetValues<HotSpotKind>())
        {
            Assert.True(yew > HotSpotRules.WeightOf(kind, yewGate: false), kind.ToString());
            Assert.True(
                HotSpotRules.HousesFor(HotSpotKind.Moongate, yewGate: true) > HotSpotRules.HousesFor(kind, yewGate: false),
                kind.ToString()
            );
        }
    }

    [Fact]
    public void HousesFor_NoneInTheDenOrAtAGraveyardByTown()
    {
        Assert.Equal(0, HotSpotRules.HousesFor(HotSpotKind.Den, yewGate: false));
        Assert.Equal(0, HotSpotRules.HousesFor(HotSpotKind.Graveyard, yewGate: false));
        Assert.Equal(HotSpotRules.SpotHouses, HotSpotRules.HousesFor(HotSpotKind.DungeonDoor, yewGate: false));
    }

    [Theory]
    [InlineData("Despise", true)]
    [InlineData("covetous", true)]
    [InlineData(" Shame ", true)]
    [InlineData("Wrong", false)]
    [InlineData(null, false)]
    public void IsBusyDungeon_TheClassicFour(string dungeon, bool busy) =>
        Assert.Equal(busy, HotSpotRules.IsBusyDungeon(dungeon));

    [Fact]
    public void IsHotGraveyard_TheBritainCemetery()
    {
        Assert.True(HotSpotRules.IsHotGraveyard("Britain Cemetery"));
        Assert.False(HotSpotRules.IsHotGraveyard("Cove Cemetery"));
    }

    [Fact]
    public void PickIndex_SameGangAgrees_WeightsDecide_ZeroWeightNever()
    {
        int[] weights = [HotSpotRules.YewGateWeight, 0, HotSpotRules.GateWeight];
        var counts = new int[weights.Length];
        var slot = HotSpotRules.SlotOf(Now);

        for (var gang = 0; gang < Slots; gang++)
        {
            var pick = HotSpotRules.PickIndex(weights, gang, slot);
            Assert.Equal(pick, HotSpotRules.PickIndex(weights, gang, slot));
            counts[pick]++;
        }

        Assert.Equal(0, counts[1]);
        Assert.True(counts[0] > counts[2]);
        Assert.Equal(HotSpotRules.NoSpot, HotSpotRules.PickIndex([], 1, slot));
        Assert.Equal(HotSpotRules.NoSpot, HotSpotRules.PickIndex([0, 0], 1, slot));
        Assert.Equal(HotSpotRules.NoSpot, HotSpotRules.PickIndex(null, 1, slot));
    }

    [Fact]
    public void PickIndex_TheCampMovesOnWithTheSlot()
    {
        int[] weights = [1, 1, 1, 1];
        var seen = new HashSet<int>();

        for (long slot = 0; slot < Slots; slot++)
        {
            seen.Add(HotSpotRules.PickIndex(weights, gang: 3, slot));
        }

        Assert.Equal(weights.Length, seen.Count);
        Assert.Equal(HotSpotRules.SlotOf(Now) + 1, HotSpotRules.SlotOf(Now + HotSpotRules.CampSlot));
    }

    [Theory]
    [InlineData(0, 4, 0)]
    [InlineData(5, 4, 1)]
    [InlineData(-1, 4, 3)]
    [InlineData(2, 0, HotSpotRules.NoSpot)]
    public void CampIndex_GangsSpreadOverTheCamps(int gang, int camps, int index) =>
        Assert.Equal(index, HotSpotRules.CampIndex(gang, camps));

    [Fact]
    public void Camps_StandPastTheGuardBoxAndThePeaceRadius()
    {
        Assert.True(HotSpotRules.GateCampTiles > FactionRules.SafeRadius);
        Assert.True(HotSpotRules.MinOpenCamps <= HotSpotRules.CampDirections);
    }

    [Theory]
    [InlineData(HotSpotKind.Moongate, true)]
    [InlineData(HotSpotKind.DungeonDoor, true)]
    [InlineData(HotSpotKind.Graveyard, true)]
    [InlineData(HotSpotKind.Den, false)]
    public void IsRunCamp_ARunRidesOutOfTheDen_NotIntoIt(HotSpotKind kind, bool camp) =>
        // The live log: 14 to 22 reds at a time "camped" Buccaneer's Den, where no red may strike.
        Assert.Equal(camp, HotSpotRules.IsRunCamp(kind));

    [Fact]
    public void InReach_ADenRedReachesTheYewGate_OneMoongateHopAway()
    {
        // The engine's pads (PublicMoongate.cs): Yew in the woods, and the Den's own gate.
        var yewGate = new Point3D(771, 752, 5);
        var denGate = new Point3D(2711, 2234, 0);
        var yewCamp = new Point3D(yewGate.X, yewGate.Y - HotSpotRules.GateCampTiles, yewGate.Z);
        IReadOnlyList<Point3D> openGates = [yewGate, denGate];

        Assert.False(HotSpotRules.InReach(true, NavMetric.Chebyshev(PkRules.BucsDenHaven, yewCamp), false));
        Assert.True(HotSpotRules.InReach(true, NavMetric.ByMoongate(PkRules.BucsDenHaven, yewCamp, openGates), false));
        Assert.False(HotSpotRules.InReach(false, NavMetric.ByMoongate(PkRules.BucsDenHaven, yewCamp, openGates), false));
    }
}
