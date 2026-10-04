using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class StableRulesTests
{
    private const uint Runa = 0x7D0301;
    private const int DragonHitsMax = 400;
    private const int FewHits = 150;
    private const int MostHits = 380;
    private const int NoFollowers = 0;
    private const int DragonAndHorse = 4;
    private const int FollowersMax = 5;
    private const int TwoPets = 2;

    private static readonly DateTime Start = new(2026, 9, 28, 19, 6, 34, DateTimeKind.Utc);
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);
    private static readonly Point3D SkaraStable = new(570, 2121, 0);

    public StableRulesTests() => TestMap.EnsureInternal();

    [Fact]
    public void ReasonToStable_SoonAfterAClaim_StablesNothing()
    {
        // In and out on one errand pays the trainer twice for nothing.
        var justClaimed = StableRules.RestableGap - OneMinute;

        Assert.Equal(StableReason.None, StableRules.ReasonToStable(justClaimed, true, Always));
    }

    [Fact]
    public void ReasonToStable_AHurtPetBeforeFullSlots_AndTheSlotLookOnlyWhenNeeded()
    {
        var looked = false;
        bool Look()
        {
            looked = true;
            return true;
        }

        Assert.Equal(StableReason.Rest, StableRules.ReasonToStable(StableRules.RestableGap, true, Look));
        Assert.False(looked);
        Assert.Equal(StableReason.Taming, StableRules.ReasonToStable(StableRules.RestableGap, false, Look));
        Assert.True(looked);
        Assert.Equal(StableReason.None, StableRules.ReasonToStable(TimeSpan.MaxValue, false, Never));
    }

    [Fact]
    public void SinceClaim_WithNoClaimYet_IsForever()
    {
        Assert.Equal(TimeSpan.MaxValue, StableRules.SinceClaim(default, Start));
        Assert.Equal(OneMinute, StableRules.SinceClaim(Start, Start + OneMinute));
    }

    [Fact]
    public void Hold_AHurtPetRestsItsLeastTime_ThenUntilWell_AtMostTheLongest()
    {
        var restAt = Start;

        Assert.Equal(StableHold.Rest, StableRules.Hold(restAt + OneMinute, restAt, false, default, default));
        Assert.Equal(StableHold.None, StableRules.Hold(restAt + StableRules.PetRestMin, restAt, false, default, default));
        Assert.Equal(StableHold.Rest, StableRules.Hold(restAt + StableRules.PetRestMin, restAt, true, default, default));
        Assert.Equal(StableHold.None, StableRules.Hold(restAt + StableRules.PetRestMax, restAt, true, default, default));
    }

    [Fact]
    public void Hold_PetsWaitForTheTamingTrip_UntilItsSessionCloses()
    {
        var stabledAt = Start;
        var during = stabledAt + OneMinute;

        Assert.Equal(StableHold.Taming, StableRules.Hold(during, default, false, stabledAt, default));
        Assert.Equal(StableHold.Taming, StableRules.Hold(during, default, false, stabledAt, stabledAt - OneMinute));
        Assert.Equal(StableHold.None, StableRules.Hold(during, default, false, stabledAt, during));
        Assert.Equal(StableHold.None, StableRules.Hold(stabledAt + StableRules.TamingHold, default, false, stabledAt, default));
    }

    [Fact]
    public void NeedsRest_OnlyABadlyHurtPetWithNoCareAtHand()
    {
        Assert.True(StableRules.NeedsRest(FewHits, DragonHitsMax, false, false, false));
        Assert.False(StableRules.NeedsRest(FewHits, DragonHitsMax, false, true, false));
        Assert.False(StableRules.NeedsRest(FewHits, DragonHitsMax, false, false, true));
        Assert.False(StableRules.NeedsRest(FewHits, DragonHitsMax, true, false, false));
        Assert.False(StableRules.NeedsRest(MostHits, DragonHitsMax, false, false, false));
    }

    [Fact]
    public void StillHurt_UntilNearlyFull()
    {
        Assert.True(StableRules.StillHurt(FewHits, DragonHitsMax));
        Assert.False(StableRules.StillHurt(MostHits, DragonHitsMax));
    }

    [Fact]
    public void SlotsMayBlock_OnlyWithFewerSlotsFreeThanADragonTakes()
    {
        Assert.True(StableRules.SlotsMayBlock(DragonAndHorse, FollowersMax));
        Assert.False(StableRules.SlotsMayBlock(NoFollowers, FollowersMax));
    }

    [Fact]
    public void StableRoomAndFee_FollowTheTrainersRules()
    {
        Assert.Equal(1, StableRules.StableRoom(TwoPets, 1));
        Assert.Equal(0, StableRules.StableRoom(TwoPets, TwoPets + 1));
        Assert.True(StableRules.CanPay(StableRules.StableFee * TwoPets, TwoPets));
        Assert.False(StableRules.CanPay(StableRules.StableFee * TwoPets - 1, TwoPets));
        Assert.False(StableRules.CanPay(StableRules.StableFee, 0));
    }

    [Fact]
    public void Lines_NameThePetsAndWhy_EasyToCount()
    {
        Assert.Equal(
            "Runa stabled 1 pet at (570, 2121, 0) to rest a hurt pet: Dragon",
            StableRules.StabledLine("Runa", ["Dragon"], SkaraStable, StableReason.Rest)
        );
        Assert.Equal(
            "Runa stabled 2 pets at (570, 2121, 0) to free its slots for taming: Dragon, Horse",
            StableRules.StabledLine("Runa", ["Dragon", "Horse"], SkaraStable, StableReason.Taming)
        );
        Assert.Equal("Runa claimed 1 pet at (570, 2121, 0): Dragon", StableRules.ClaimedLine("Runa", ["Dragon"], SkaraStable));
    }

    [Fact]
    public void NoteStabledAndNoteClaim_KeepTheHoldsOnTheSavedClocks()
    {
        var runa = new SosariaCharacter((Serial)Runa);

        PetKeeper.NoteStabled(runa, StableReason.Rest, Start);
        PetKeeper.NoteStabled(runa, StableReason.Taming, Start);

        Assert.Equal(Start, runa.ClockAt(RuleClock.StabledToRest));
        Assert.Equal(Start, runa.ClockAt(RuleClock.StabledForTaming));
        Assert.Equal(2, runa.RuleClocks.Count);

        var claimedAt = Start + StableRules.PetRestMin;
        PetKeeper.NoteClaim(runa, claimedAt);

        Assert.Equal(default(DateTime), runa.ClockAt(RuleClock.StabledToRest));
        Assert.Equal(default(DateTime), runa.ClockAt(RuleClock.StabledForTaming));
        Assert.Equal(claimedAt, runa.ClockAt(RuleClock.PetsClaimed));
    }

    private static bool Always() => true;

    private static bool Never() => false;
}
