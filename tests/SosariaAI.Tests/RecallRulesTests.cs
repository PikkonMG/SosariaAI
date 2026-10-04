using System;
using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class RecallRulesTests
{
    private const string ExpectedKind = "Recall";
    private const double ExpectedMinMagery = 30;
    private const int CombatHeatSeconds = 30;
    private const string WhyNoPath = "no graph path from the near nodes";

    /// <summary>The engine's criminal flag lapses after this long.</summary>
    private const int CriminalFlagMinutes = 2;

    [Fact]
    public void NoRoadLine_TellsWhyNoRecallCarriedTheWalker()
    {
        // 38 reds on the Den wrote "no route" and nothing on why the recall failed.
        Assert.Equal(
            $"{WhyNoPath}; no recall: {RecallRules.NoRuneWhy}",
            RecallRules.NoRoadLine(WhyNoPath, RecallRules.NoRuneWhy)
        );
        Assert.Equal(WhyNoPath, RecallRules.NoRoadLine(WhyNoPath, null));
    }

    [Fact]
    public void MayRecast_WhileTriesAreLeftAndTheWaitRuns()
    {
        // Norvel "fizzled recall" at the Den, cast again inside the recovery, and the run was over.
        var now = DateTime.UtcNow;
        var waitEnds = now + RecallRules.RecastWait;

        Assert.True(RecallRules.MayRecast(1, waitEnds, now));
        Assert.False(RecallRules.MayRecast(1, waitEnds, waitEnds));
        Assert.False(RecallRules.MayRecast(RecallRules.MaxCastTries, waitEnds, now));
    }

    [Fact]
    public void NoRoadRecallWait_OutlastsTheHeatOfBattle() =>
        // The engine keeps a caster from recalling for thirty seconds after a blow on a player.
        Assert.True(RecallRules.NoRoadRecallWait > TimeSpan.FromSeconds(CombatHeatSeconds));

    [Fact]
    public void Kind_IsRecall()
    {
        Assert.Equal(ExpectedKind, RecallRules.Kind);
        Assert.Equal(ExpectedKind, new RecallSkill().Name);
    }

    [Fact]
    public void MinMagery_FourthCircle() =>
        Assert.Equal(ExpectedMinMagery, RecallRules.MinMagery);

    [Fact]
    public void Scroll_EasierThanTheBookAndOneRuleForCharges()
    {
        Assert.True(RecallRules.ScrollMinMagery < RecallRules.MinMagery);
        Assert.Equal(RecallRules.ScrollMinMagery, RunebookRules.ChargeMinMagery);
        Assert.Equal(RecallRules.MinMagery, TravelSpells.BookMinMagery(TravelSpellKind.Recall));
        Assert.Equal(RecallRules.ScrollMinMagery, TravelSpells.ScrollMinMagery(TravelSpellKind.Recall));
    }

    [Theory]
    [InlineData(900, 10, true)]
    [InlineData(RecallRules.MinTripTiles - 1, 10, false)]
    [InlineData(900, RecallRules.MaxLandingWalkTiles, true)]
    [InlineData(900, RecallRules.MaxLandingWalkTiles + 1, false)]
    [InlineData(RecallRules.MinTripTiles, RecallRules.MinTripTiles, false)]
    public void PaysForTrip_LongTripToARuneNearTheGoal(int trip, int landingToGoal, bool pays) =>
        Assert.Equal(pays, RecallRules.PaysForTrip(trip, landingToGoal));

    [Fact]
    public void PaysForTrip_TheWalkLeftIsASmallShareOfTheTrip()
    {
        const int trip = 240;
        var third = trip / RecallRules.TripShareOfLandingWalk;

        Assert.True(RecallRules.PaysForTrip(trip, third));
        Assert.False(RecallRules.PaysForTrip(trip, third + 1));
    }

    [Fact]
    public void PaysForTrip_ADungeonDoorPaysSooner()
    {
        const int trip = DungeonEntryRules.RecallMinTripTiles;

        Assert.True(DungeonEntryRules.RecallMinTripTiles < RecallRules.MinTripTiles);
        Assert.False(RecallRules.PaysForTrip(trip, 0));
        Assert.True(RecallRules.PaysForTrip(trip, 0, DungeonEntryRules.RecallMinTripTiles));
        Assert.False(RecallRules.PaysForTrip(trip - 1, 0, DungeonEntryRules.RecallMinTripTiles));
    }

    [Fact]
    public void RuneToward_NoCharacter_FindsNoRune()
    {
        Assert.Null(RecallRules.RuneToward(null, new Point3D(1434, 1699, 0)));
        Assert.False(RecallRules.TryRecallToward(null, new Point3D(1434, 1699, 0)));
    }

    [Fact]
    public void RecallSkill_TowardAGoal_IsARecall() =>
        Assert.Equal(ExpectedKind, new RecallSkill(new Point3D(1434, 1699, 0)).Name);

    [Fact]
    public void Fizzle_IsCastAgainAFewTimesThenWalked()
    {
        Assert.True(RecallRules.ShouldCastAgain(1));
        Assert.True(RecallRules.ShouldCastAgain(RecallRules.MaxCastTries - 1));
        Assert.False(RecallRules.ShouldCastAgain(RecallRules.MaxCastTries));
    }

    [Fact]
    public void TryRecallHome_NoCharacter_CastsNothing() =>
        Assert.False(RecallRules.TryRecallHome(null));

    [Fact]
    public void TakesMagic_MostLongTripsRecallAndSomeWalk()
    {
        // An all-recall world empties the roads: a share of long trips is walked.
        var recalls = 0;

        for (var roll = 0; roll < PercentRoll.Scale; roll++)
        {
            recalls += RecallRules.TakesMagic(RecallRules.MinTripTiles, roll) ? 1 : 0;
        }

        Assert.Equal(RecallRules.LongTripMagicPercent, recalls);
        Assert.True(recalls < PercentRoll.Scale);
    }

    [Fact]
    public void TakesMagic_AFarTripRecallsMoreOftenThanALongOne()
    {
        Assert.True(RecallRules.FarTripMagicPercent > RecallRules.LongTripMagicPercent);
        Assert.Equal(RecallRules.LongTripMagicPercent, RecallRules.MagicPercent(RecallRules.FarTripTiles - 1));
        Assert.Equal(RecallRules.FarTripMagicPercent, RecallRules.MagicPercent(RecallRules.FarTripTiles));
        Assert.True(RecallRules.TakesMagic(RecallRules.FarTripTiles, RecallRules.FarTripMagicPercent - 1));
        Assert.False(RecallRules.TakesMagic(RecallRules.FarTripTiles, RecallRules.FarTripMagicPercent));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue + 1)]
    [InlineData(int.MaxValue)]
    public void TakesMagic_AnyRollIsInRange(int roll) =>
        Assert.Equal(
            System.Math.Abs(roll % PercentRoll.Scale) < RecallRules.LongTripMagicPercent,
            RecallRules.TakesMagic(RecallRules.MinTripTiles, roll)
        );

    [Fact]
    public void PaysForTrip_WithNoRoadAShortTripPaysWhenTheRuneLandsBesideTheGoal()
    {
        // Across a river or to an island the road does not exist, so length is no bar;
        // the walk from the landing must still be a small share of the trip.
        const int trip = 40;

        Assert.False(RecallRules.PaysForTrip(trip, 0));
        Assert.True(RecallRules.PaysForTrip(trip, trip / RecallRules.TripShareOfLandingWalk, RecallRules.NoRoadMinTripTiles));
        Assert.False(RecallRules.PaysForTrip(trip, trip / RecallRules.TripShareOfLandingWalk + 1, RecallRules.NoRoadMinTripTiles));
    }

    [Fact]
    public void RecallSkill_OverNoRoad_IsARecall() =>
        Assert.Equal(ExpectedKind, new RecallSkill(new Point3D(2880, 3472, 15), RecallRules.NoRoadMinTripTiles).Name);
    [Theory]
    [InlineData(TravelSpells.CriminalWhy, true)]
    [InlineData(TravelSpells.HeatWhy, true)]
    [InlineData(TravelSpells.FightingWhy, true)]
    [InlineData(TravelSpells.RecoveringWhy, true)]
    [InlineData(TravelSpells.ManaWhy, true)]
    [InlineData(TravelSpells.LandingTakenWhy, true)]
    [InlineData(TravelSpells.NoMeansWhy, false)]
    [InlineData(TravelSpells.PlaceWhy, false)]
    [InlineData(RecallRules.NoRuneWhy, false)]
    public void HomeRecallWaits_ForARefusalThatPasses_OrTheCriminalFlag(string why, bool waits) =>
        Assert.Equal(waits, RecallRules.HomeRecallWaits(why));

    [Fact]
    public void HomeRecallWait_OutlastsTheCriminalFlag()
    {
        Assert.True(RecallRules.HomeRecallWait > TimeSpan.FromMinutes(CriminalFlagMinutes));
    }

    /// <summary>
    /// Reds flagged for looting at the Shame door had no road home clear of the guards and ended
    /// 78 walks home with "no recall: a criminal": the trip waited only for refusals that pass in
    /// seconds. It waits out the flag now, and a refusal no wait ends is not waited for.
    /// </summary>
    [Fact]
    public void NoRoadWait_ThePassingRefusalsBriefly_TheCriminalFlagOut_TheRestNot()
    {
        Assert.Equal(RecallRules.NoRoadRecallWait, RecallRules.NoRoadWait(TravelSpells.HeatWhy));
        Assert.Equal(RecallRules.NoRoadRecallWait, RecallRules.NoRoadWait(TravelSpells.BookRestWhy));
        Assert.Equal(RecallRules.HomeRecallWait, RecallRules.NoRoadWait(TravelSpells.CriminalWhy));
        Assert.True(RecallRules.NoRoadWait(TravelSpells.CriminalWhy) > TimeSpan.FromMinutes(CriminalFlagMinutes));
        Assert.Null(RecallRules.NoRoadWait(RecallRules.NoRuneWhy));
        Assert.Null(RecallRules.NoRoadWait(TravelSpells.PlaceWhy));
        Assert.Null(RecallRules.NoRoadWait(null));
    }
}
