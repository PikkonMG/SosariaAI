using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class SkillOutcomeTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0);

    [Fact]
    public void SkillClock_Shift_MovesOnlyASetMark()
    {
        Assert.Equal(default, SkillClock.Shift(default, TimeSpan.FromMinutes(1)));
        Assert.Equal(Start.AddMinutes(1), SkillClock.Shift(Start, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void GoToSkill_Resume_ShiftsTheGiveUpClock()
    {
        var skill = new GoToSkill(new Point3D(1, 1, 0), 1);
        Assert.NotNull(skill);
        Assert.False(GoToSkill.TimeUp(Start + GoToSkill.GiveUp, SkillClock.Shift(Start, TimeSpan.FromSeconds(1))));
    }

    [Fact]
    public void DecideSkill_Outcome_DoneWhenResolvedFailedAfterGiveUp()
    {
        Assert.Equal(SkillStatus.Done, DecideSkill.Outcome(resolved: true, Start, Start));
        Assert.Equal(SkillStatus.Running, DecideSkill.Outcome(resolved: false, Start, Start));
        Assert.Equal(SkillStatus.Failed, DecideSkill.Outcome(resolved: false, Start + DecideSkill.GiveUp, Start));
    }

    [Fact]
    public void HarvestSkill_Outcome_FailsADeadPatch()
    {
        Assert.Equal(SkillStatus.Done, HarvestSkill.Outcome(countAtStart: 3, countNow: 4));
        Assert.Equal(SkillStatus.Failed, HarvestSkill.Outcome(countAtStart: 3, countNow: 3));
    }

    [Fact]
    public void DungeonTripSkill_HomeOutcome_FailedHomeWalkIsStillDone()
    {
        Assert.Equal(SkillStatus.Done, DungeonTripSkill.HomeOutcome(SkillStatus.Failed, crawlFailed: false));
        Assert.Equal(SkillStatus.Done, DungeonTripSkill.HomeOutcome(SkillStatus.Done, crawlFailed: false));
        Assert.Equal(SkillStatus.Running, DungeonTripSkill.HomeOutcome(SkillStatus.Running, crawlFailed: false));
    }

    [Fact]
    public void DungeonTripSkill_HomeOutcome_ACrawlThatReachedNoRoomFailsTheTrip()
    {
        Assert.Equal(SkillStatus.Failed, DungeonTripSkill.HomeOutcome(SkillStatus.Failed, crawlFailed: true));
        Assert.Equal(SkillStatus.Failed, DungeonTripSkill.HomeOutcome(SkillStatus.Done, crawlFailed: true));
        Assert.Equal(SkillStatus.Running, DungeonTripSkill.HomeOutcome(SkillStatus.Running, crawlFailed: true));
    }

    [Fact]
    public void BankShopSkill_DwellOver_CountsFromTheShout()
    {
        Assert.False(TreasureMarketRules.DwellOver(Start + BankShopSkill.ShopDwell, default, BankShopSkill.ShopDwell));
        Assert.False(TreasureMarketRules.DwellOver(Start, Start, BankShopSkill.ShopDwell));
        Assert.True(TreasureMarketRules.DwellOver(Start + BankShopSkill.ShopDwell, Start, BankShopSkill.ShopDwell));
        Assert.False(TreasureMarketRules.DwellOver(Start + BankShopSkill.ShopDwell, Start, BankShopSkill.CrafterHawkDwell));
        Assert.True(TreasureMarketRules.DwellOver(Start + BankShopSkill.CrafterHawkDwell, Start, BankShopSkill.CrafterHawkDwell));
    }

    [Fact]
    public void HuntSkill_ShouldRest_HoldsUntilFitOnceRecovering()
    {
        Assert.False(HuntSkill.ShouldRest(recovering: false, hitsFraction: 0.7, inCombat: false));
        Assert.True(HuntSkill.ShouldRest(recovering: true, hitsFraction: 0.7, inCombat: false));
        Assert.False(HuntSkill.ShouldRest(recovering: true, hitsFraction: 1.0, inCombat: false));
    }

    [Fact]
    public void HuntSkill_PlayerFarTooLong_AfterTheFollowLimit()
    {
        Assert.False(HuntSkill.PlayerFarTooLong(Start, default));
        Assert.False(HuntSkill.PlayerFarTooLong(Start, Start));
        Assert.True(HuntSkill.PlayerFarTooLong(Start + SosariaAI.Behaviour.PartyWaitRules.FollowLimit, Start));
    }

    [Fact]
    public void GearEquip_ConflictingLayers_BothHandsForAWeapon()
    {
        Assert.Equal([Layer.OneHanded, Layer.TwoHanded], GearEquip.ConflictingLayers(Layer.OneHanded));
        Assert.Equal([Layer.OneHanded, Layer.TwoHanded], GearEquip.ConflictingLayers(Layer.TwoHanded));
        Assert.Equal([Layer.Helm], GearEquip.ConflictingLayers(Layer.Helm));
    }
}
