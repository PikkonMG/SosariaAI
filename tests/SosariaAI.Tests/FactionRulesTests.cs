using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class FactionRulesTests
{
    private const string Rowan = "Rowan the Scribe";
    private const string Kerr = "Kerr";
    private const int FarLeash = 4000;
    private const int ShortLeash = 10;

    private static readonly DateTime Now = new(2026, 9, 24, 10, 0, 0);

    [Fact]
    public void MayFightAt_OrderAndChaosAnywhere()
    {
        Assert.True(FactionRules.MayFightAt(alignmentFoes: true, selfHasRoom: false, foeHasRoom: false));
        Assert.True(FactionRules.MayFightAt(alignmentFoes: true, selfHasRoom: true, foeHasRoom: true));
    }

    [Fact]
    public void MayFightAt_AGuildWarOnlyWithRoomOnBothSides()
    {
        Assert.True(FactionRules.MayFightAt(alignmentFoes: false, selfHasRoom: true, foeHasRoom: true));
        Assert.False(FactionRules.MayFightAt(alignmentFoes: false, selfHasRoom: false, foeHasRoom: true));
        Assert.False(FactionRules.MayFightAt(alignmentFoes: false, selfHasRoom: true, foeHasRoom: false));
    }

    [Fact]
    public void SightRange_IsOneScreen_PastAPartyInvite_InsideAPatrolsCourse()
    {
        Assert.True(FactionRules.SightRange > CareerSettings.DefaultPartyInviteRange);
        Assert.True(FactionRules.SightRange <= FactionRules.InterceptRange);
    }

    [Fact]
    public void FightRoomRadius_IsThePeaceRingAndTheLineMargin()
    {
        Assert.Equal(FactionRules.SafeRadius + GuardLineRules.LineMarginTiles, FactionRules.FightRoomRadius);
        Assert.True(FactionRules.FightRoomRadius > FactionRules.SafeRadius);
    }

    [Fact]
    public void Side_HoldsAtFourAndDraftsOnlyTheMissing()
    {
        Assert.True(FactionRules.MayJoinSide(FactionRules.MaxSide - 1));
        Assert.False(FactionRules.MayJoinSide(FactionRules.MaxSide));
        Assert.Equal(FactionRules.MaxSide, FactionRules.DraftSlots(0));
        Assert.Equal(1, FactionRules.DraftSlots(FactionRules.MaxSide - 1));
        Assert.Equal(0, FactionRules.DraftSlots(FactionRules.MaxSide + 2));
    }

    [Fact]
    public void TauntDue_OncePerPersonLongerPerPairAndOneVoiceAtAFoe()
    {
        Assert.True(FactionRules.TauntDue(default, default, default, default, Now));
        Assert.False(FactionRules.TauntDue(Now, default, default, default, Now + FactionRules.TauntRest - TimeSpan.FromSeconds(1)));
        Assert.True(FactionRules.TauntDue(Now, default, default, default, Now + FactionRules.TauntRest));
        Assert.False(FactionRules.TauntDue(default, Now, default, default, Now + FactionRules.TauntRest));
        Assert.True(FactionRules.TauntDue(default, Now, default, default, Now + FactionRules.PairTauntRest));
        Assert.False(FactionRules.TauntDue(default, default, Now, default, Now + FactionRules.FoeTauntRest - TimeSpan.FromSeconds(1)));
        Assert.True(FactionRules.TauntDue(default, default, Now, default, Now + FactionRules.FoeTauntRest));
    }

    [Fact]
    public void TauntDue_OnePlaceHearsWordsNowAndThen()
    {
        Assert.False(FactionRules.TauntDue(default, default, default, Now, Now + FactionRules.PlaceTauntRest - TimeSpan.FromSeconds(1)));
        Assert.True(FactionRules.TauntDue(default, default, default, Now, Now + FactionRules.PlaceTauntRest));
    }

    [Fact]
    public void Feuding_KillerAndVictimLeaveEachOtherAloneForTheRest()
    {
        var died = Now;
        var inside = Now + FactionRules.RevengeRest - TimeSpan.FromMinutes(1);
        var after = Now + FactionRules.RevengeRest;

        // Kerr killed Rowan: Rowan does not go back at Kerr, and Kerr does not go back at Rowan.
        Assert.True(FactionRules.Feuding(Kerr, Kerr, null, Rowan, died, default, inside));
        Assert.True(FactionRules.Feuding(null, Rowan, Kerr, Kerr, default, died, inside));
        Assert.False(FactionRules.Feuding(Kerr, Kerr, null, Rowan, died, default, after));
        Assert.False(FactionRules.Feuding("someone else", Kerr, null, Rowan, died, default, inside));
        Assert.False(FactionRules.Feuding(null, Kerr, null, Rowan, default, default, inside));
    }

    [Fact]
    public void ShouldDisengage_WhenBadlyHurtOrTheSkirmishRanLong()
    {
        Assert.True(FactionRules.ShouldDisengage(FactionRules.DisengageHits - 0.01, 1.0, Now, Now));
        Assert.False(FactionRules.ShouldDisengage(1.0, 1.0, Now, Now + FactionRules.SkirmishLimit - TimeSpan.FromSeconds(1)));
        Assert.True(FactionRules.ShouldDisengage(1.0, 1.0, Now, Now + FactionRules.SkirmishLimit));
        Assert.False(FactionRules.ShouldDisengage(1.0, 1.0, default, Now));
    }

    [Fact]
    public void ShouldDisengage_AWinnerFinishesPastTheLimit()
    {
        const double NearlyBeaten = FocusRules.FinishHitsFraction - 0.01;

        Assert.False(FactionRules.ShouldDisengage(1.0, NearlyBeaten, Now, Now + FactionRules.SkirmishLimit));
        Assert.True(FactionRules.ShouldDisengage(FactionRules.DisengageHits - 0.01, NearlyBeaten, Now, Now));
    }

    [Fact]
    public void StandsFoeDown_OnlyWhenTheFightRanLongNotWhenTheLoserBrokeOff()
    {
        Assert.True(FactionRules.StandsFoeDown(1.0));
        Assert.False(FactionRules.StandsFoeDown(FactionRules.DisengageHits - 0.01));
    }

    [Theory]
    [InlineData(0L, 9)]
    [InlineData(5L, 9)]
    [InlineData(123456L, 9)]
    [InlineData(3L, 1)]
    public void ActiveIndexes_AreDistinctAndInRange(long slot, int count)
    {
        var open = FactionRules.ActiveIndexes(slot, count);

        Assert.Equal(Math.Min(FactionRules.ActiveSpots, count), open.Length);
        Assert.Equal(open.Length, new HashSet<int>(open).Count);

        foreach (var index in open)
        {
            Assert.InRange(index, 0, count - 1);
        }
    }

    [Fact]
    public void ActiveIndexes_NoSpotsMeansNone() => Assert.Empty(FactionRules.ActiveIndexes(4, 0));

    [Fact]
    public void SlotOf_ChangesOncePerSlot()
    {
        var start = new DateTime(FactionRules.SlotOf(Now) * FactionRules.SlotLength.Ticks);
        var last = start + FactionRules.SlotLength - TimeSpan.FromTicks(1);

        Assert.Equal(FactionRules.SlotOf(start), FactionRules.SlotOf(last));
        Assert.Equal(FactionRules.SlotOf(start) + 1, FactionRules.SlotOf(start + FactionRules.SlotLength));
    }

    [Fact]
    public void Spots_FeluccaOnly()
    {
        Assert.NotEmpty(FactionSpots.For(FacetNames.Felucca));
        Assert.Empty(FactionSpots.For(FacetNames.Trammel));
        Assert.Empty(FactionSpots.For(null));
    }

    [Fact]
    public void Spots_OutsideEveryBucsDenAndNamedOnce()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var spot in FactionSpots.For(FacetNames.Felucca))
        {
            Assert.True(names.Add(spot.Name), spot.Name);
            Assert.False(PkRules.InBuccaneersDen(spot.Location.X, spot.Location.Y), spot.Name);
        }
    }

    [Fact]
    public void Pick_BothSidesChooseAmongTheSameOpenSpots()
    {
        var spots = FactionSpots.For(FacetNames.Felucca);
        var open = FactionRules.ActiveIndexes(FactionRules.SlotOf(Now), spots.Count);
        var britain = new Point3D(1450, 1650, 0);
        var picked = FactionSpots.Pick(spots, Now, britain, britain, FarLeash);

        Assert.NotNull(picked);
        Assert.Contains(picked.Value, Array.ConvertAll(open, index => spots[index]));
        Assert.Equal(picked, FactionSpots.Pick(spots, Now, britain, britain, FarLeash));
    }

    [Fact]
    public void Pick_NoOpenSpotInsideTheLeash_IsNull()
    {
        var spots = FactionSpots.For(FacetNames.Felucca);
        var farAway = new Point3D(5000, 3000, 0);

        Assert.Null(FactionSpots.Pick(spots, Now, farAway, farAway, ShortLeash));
        Assert.Null(FactionSpots.Pick([], Now, farAway, farAway, FarLeash));
    }

    [Fact]
    public void MayFight_OnlyBothArmed()
    {
        Assert.True(FactionRules.MayFight(selfArmed: true, foeArmed: true));
        Assert.False(FactionRules.MayFight(selfArmed: false, foeArmed: true));
        Assert.False(FactionRules.MayFight(selfArmed: true, foeArmed: false));
    }
}
