using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class TreasureHuntRulesTests
{
    private const int LevelOne = 1;
    private const int LevelTwo = 2;
    private const int LevelThree = 3;
    private const int LevelFive = 5;
    private const int LevelSix = 6;
    private const int YoungLevel = 0;
    private const int PastTopLevel = 7;

    private const double NoSkill = 0;
    private const double JustBelowLevelTwoDecode = 40.9;
    private const double LevelTwoDecode = 41.0;
    private const double GrandmasterSkill = 100.0;

    private const double MageryOpeningLevelOne = 50.0;
    private const double MageryShortOfLevelOne = 49.0;
    private const double LockpickingForLevelOne = 36.0;
    private const double LockpickingShortOfLevelOne = 35.0;

    private const double DisarmSkill = 50.0;
    private const double BelowDisarmSkill = 49.9;

    private const int TrapLevelTwo = 2;
    private const int WorstLevelTwoBlast = 60;
    private const int HitsToTakeLevelTwo = WorstLevelTwoBlast + TreasureHuntRules.TrapHitsMargin + 1;
    private const int HitsTooLowForLevelTwo = WorstLevelTwoBlast + TreasureHuntRules.TrapHitsMargin;

    private const int RichPurse = 5000;
    private const int PoorPurse = 10;
    private const int ShopRoll = 0;
    private const int CraftRoll = TreasureHuntRules.ShopPercent;

    [Fact]
    public void DecodeMinSkill_MirrorsTheEngineTable()
    {
        Assert.Equal(TreasureHuntRules.LevelOneDecodeSkill, TreasureHuntRules.DecodeMinSkill(LevelOne));
        Assert.Equal(TreasureHuntRules.LevelTwoDecodeSkill, TreasureHuntRules.DecodeMinSkill(LevelTwo));
        Assert.Equal(TreasureHuntRules.LevelThreeDecodeSkill, TreasureHuntRules.DecodeMinSkill(LevelThree));
        Assert.Equal(TreasureHuntRules.TopLevelDecodeSkill, TreasureHuntRules.DecodeMinSkill(LevelFive));
        Assert.Equal(TreasureHuntRules.TopLevelDecodeSkill, TreasureHuntRules.DecodeMinSkill(LevelSix));
        Assert.Equal(TreasureHuntRules.NoDecodeSkill, TreasureHuntRules.DecodeMinSkill(YoungLevel));
    }

    [Fact]
    public void CanDecode_NeedsTheLevelMinimum()
    {
        Assert.True(TreasureHuntRules.CanDecode(LevelOne, NoSkill));
        Assert.False(TreasureHuntRules.CanDecode(LevelTwo, JustBelowLevelTwoDecode));
        Assert.True(TreasureHuntRules.CanDecode(LevelTwo, LevelTwoDecode));
    }

    [Fact]
    public void MayDig_TheDecoderOrAnyoneSkilledEnough()
    {
        Assert.True(TreasureHuntRules.MayDig(LevelFive, decodedBySelf: true, NoSkill));
        Assert.False(TreasureHuntRules.MayDig(LevelFive, decodedBySelf: false, JustBelowLevelTwoDecode));
        Assert.True(TreasureHuntRules.MayDig(LevelFive, decodedBySelf: false, GrandmasterSkill));
    }

    [Fact]
    public void LockSkill_MirrorsTheChestFill()
    {
        Assert.Equal(TreasureHuntRules.LevelOneLockSkill, TreasureHuntRules.LockSkill(LevelOne));
        Assert.Equal(TreasureHuntRules.LevelTwoLockSkill, TreasureHuntRules.LockSkill(LevelTwo));
        Assert.Equal(TreasureHuntRules.LevelThreeLockSkill, TreasureHuntRules.LockSkill(LevelThree));
        Assert.Equal(TreasureHuntRules.TopLevelLockSkill, TreasureHuntRules.LockSkill(LevelFive));
    }

    [Fact]
    public void MagicUnlock_FollowsTheSpellFormulaAndLevelCap()
    {
        Assert.True(TreasureHuntRules.CanMagicUnlock(LevelOne, MageryOpeningLevelOne));
        Assert.False(TreasureHuntRules.CanMagicUnlock(LevelOne, MageryShortOfLevelOne));
        Assert.False(TreasureHuntRules.CanMagicUnlock(LevelThree, GrandmasterSkill));
    }

    [Fact]
    public void OpenBy_PrefersTheSpellThenTheLockpick()
    {
        Assert.Equal(ChestOpening.MagicUnlock, TreasureHuntRules.OpenBy(LevelOne, GrandmasterSkill, MageryOpeningLevelOne));
        Assert.Equal(ChestOpening.Lockpick, TreasureHuntRules.OpenBy(LevelOne, LockpickingForLevelOne, NoSkill));
        Assert.Equal(ChestOpening.None, TreasureHuntRules.OpenBy(LevelOne, LockpickingShortOfLevelOne, MageryShortOfLevelOne));
        Assert.Equal(ChestOpening.Lockpick, TreasureHuntRules.OpenBy(LevelFive, GrandmasterSkill, GrandmasterSkill));
    }

    [Fact]
    public void CanDisarm_NeedsLockpickingAndDetectHidden()
    {
        Assert.True(TreasureHuntRules.CanDisarm(DisarmSkill, DisarmSkill));
        Assert.False(TreasureHuntRules.CanDisarm(BelowDisarmSkill, DisarmSkill));
        Assert.False(TreasureHuntRules.CanDisarm(DisarmSkill, BelowDisarmSkill));
    }

    [Fact]
    public void Trap_BlastScalesWithLevelAndTheMarginIsKept()
    {
        Assert.Equal(WorstLevelTwoBlast, TreasureHuntRules.MaxTrapDamage(TrapLevelTwo));
        Assert.True(TreasureHuntRules.CanTakeTrap(HitsToTakeLevelTwo, TrapLevelTwo));
        Assert.False(TreasureHuntRules.CanTakeTrap(HitsTooLowForLevelTwo, TrapLevelTwo));
        Assert.True(TreasureHuntRules.TelekinesisStandOff > TreasureHuntRules.TrapBlastRange);
    }

    [Fact]
    public void TrapPlan_SpellThenSkillThenBlastThenHeal()
    {
        Assert.Equal(ChestTrapPlan.None, TreasureHuntRules.TrapPlan(false, true, true, HitsToTakeLevelTwo, TrapLevelTwo));
        Assert.Equal(ChestTrapPlan.Telekinesis, TreasureHuntRules.TrapPlan(true, true, true, HitsToTakeLevelTwo, TrapLevelTwo));
        Assert.Equal(ChestTrapPlan.Disarm, TreasureHuntRules.TrapPlan(true, false, true, HitsToTakeLevelTwo, TrapLevelTwo));
        Assert.Equal(ChestTrapPlan.Accept, TreasureHuntRules.TrapPlan(true, false, false, HitsToTakeLevelTwo, TrapLevelTwo));
        Assert.Equal(ChestTrapPlan.Heal, TreasureHuntRules.TrapPlan(true, false, false, HitsTooLowForLevelTwo, TrapLevelTwo));
    }

    [Fact]
    public void MayHunt_NeedsLevelFacetDigAndOpening()
    {
        Assert.True(TreasureHuntRules.MayHunt(LevelOne, true, false, NoSkill, NoSkill, MageryOpeningLevelOne));
        Assert.False(TreasureHuntRules.MayHunt(YoungLevel, true, true, GrandmasterSkill, GrandmasterSkill, GrandmasterSkill));
        Assert.False(TreasureHuntRules.MayHunt(PastTopLevel, true, true, GrandmasterSkill, GrandmasterSkill, GrandmasterSkill));
        Assert.False(TreasureHuntRules.MayHunt(LevelOne, false, true, GrandmasterSkill, GrandmasterSkill, GrandmasterSkill));
        Assert.False(TreasureHuntRules.MayHunt(LevelTwo, true, false, JustBelowLevelTwoDecode, GrandmasterSkill, GrandmasterSkill));
        Assert.False(TreasureHuntRules.MayHunt(LevelOne, true, true, GrandmasterSkill, LockpickingShortOfLevelOne, MageryShortOfLevelOne));
    }

    [Fact]
    public void Choose_HuntSellShopOrCraft()
    {
        Assert.Equal(CartographyTask.Hunt, TreasureHuntRules.Choose(true, true, false, PoorPurse, CraftRoll));
        Assert.Equal(CartographyTask.Sell, TreasureHuntRules.Choose(true, false, true, RichPurse, ShopRoll));
        Assert.Equal(CartographyTask.Shop, TreasureHuntRules.Choose(false, false, true, PoorPurse, CraftRoll));
        Assert.Equal(CartographyTask.Shop, TreasureHuntRules.Choose(false, false, false, RichPurse, ShopRoll));
        Assert.Equal(CartographyTask.Craft, TreasureHuntRules.Choose(false, false, false, RichPurse, CraftRoll));
        Assert.Equal(CartographyTask.Craft, TreasureHuntRules.Choose(false, false, false, PoorPurse, ShopRoll));
    }
}
