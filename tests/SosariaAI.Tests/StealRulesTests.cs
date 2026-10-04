using System;
using Server;
using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class StealRulesTests
{
    private const int BritainGateX = 1336;
    private const int BritainGateY = 1997;
    private const int BritainGateZ = 5;
    private const double ClassicHidingRequirement = 80;

    [Fact]
    public void InReach_OneTile()
    {
        var at = new Point3D(1425, 1695, 0);
        Assert.True(StealRules.InReach(at, new Point3D(1426, 1695, 0)));
        Assert.False(StealRules.InReach(at, new Point3D(1428, 1695, 0)));
    }

    [Fact]
    public void PickLoot_NullPack_IsNull() =>
        Assert.Null(StealRules.PickLoot(null, maxWeight: 10));

    [Fact]
    public void KindOf_GearIsPawnGoods_JunkStaysOther()
    {
        Assert.Equal(LootKind.Gear, StealRules.KindOf(new Katana((Serial)0x7E01)));
        Assert.Equal(LootKind.Gear, StealRules.KindOf(new LeatherChest((Serial)0x7E02)));
        Assert.Equal(LootKind.Gear, StealRules.KindOf(new Shirt((Serial)0x7E03)));
        Assert.Equal(LootKind.Gear, StealRules.KindOf(new Hides((Serial)0x7E04)));
        Assert.Equal(LootKind.Magic,
            StealRules.KindOf(new Katana((Serial)0x7E05) { DamageLevel = WeaponDamageLevel.Ruin }));
        Assert.Equal(LootKind.Gear,
            StealRules.KindOf(new Katana((Serial)0x7E06) { Quality = WeaponQuality.Exceptional }));
        Assert.Equal(LootKind.Gem, StealRules.KindOf(new Amber((Serial)0x7E07)));
        Assert.Equal(LootKind.Gold, StealRules.KindOf(new Gold((Serial)0x7E08)));
        Assert.Equal(LootKind.Other, StealRules.KindOf(new Bandage((Serial)0x7E09)));
        Assert.Equal(LootKind.Other, StealRules.KindOf(new Arrow((Serial)0x7E0A)));
        Assert.Equal(LootKind.Reagent, StealRules.KindOf(new BlackPearl((Serial)0x7E0B)));
    }

    [Fact]
    public void MayBegin_Null_IsFalse() =>
        Assert.False(StealRules.MayBegin(null));

    [Fact]
    public void Worth_RanksKindsAndCountsCoins()
    {
        Assert.True(StealRules.Worth(LootKind.Jewel, 1) > StealRules.Worth(LootKind.Magic, 1));
        Assert.True(StealRules.Worth(LootKind.Magic, 1) > StealRules.Worth(LootKind.Gem, 1));
        Assert.True(StealRules.Worth(LootKind.Gold, 500) > StealRules.Worth(LootKind.Gem, 1));
        Assert.Equal(StealRules.Worth(LootKind.Other, 1), StealRules.Worth(LootKind.Other, 0));
    }

    [Fact]
    public void MarkScore_FallsWithDistance()
    {
        Assert.True(StealRules.MarkScore(100, 1, human: false) > StealRules.MarkScore(100, 5, human: false));
        Assert.True(StealRules.MarkScore(400, 5, human: false) > StealRules.MarkScore(100, 1, human: false));
    }

    [Fact]
    public void MarkScore_AHumanMarkComesFirst()
    {
        Assert.True(StealRules.MarkScore(100, 3, human: true) > StealRules.MarkScore(100, 1, human: false));
        Assert.Equal(
            StealRules.MarkScore(100, 1, human: false) * StealRules.HumanMarkFactor,
            StealRules.MarkScore(100, 1, human: true)
        );
    }

    [Fact]
    public void ShouldCreep_NeedsTheEngineHidingStealthAndLightArmor()
    {
        const double requirement = ClassicHidingRequirement;
        const int lightArmor = StealthRules.ArmorLimit - 1;

        Assert.True(StealRules.ShouldCreep(requirement, StealRules.CreepStealth, requirement, lightArmor));
        Assert.False(StealRules.ShouldCreep(requirement - 1, StealRules.CreepStealth, requirement, lightArmor));
        Assert.False(StealRules.ShouldCreep(requirement, StealRules.CreepStealth - 1, requirement, lightArmor));
        Assert.False(StealRules.ShouldCreep(requirement, StealRules.CreepStealth, requirement, StealthRules.ArmorLimit));
    }

    [Fact]
    public void VictimReaches_OnlyAThiefStillInTheGuardsReach()
    {
        Assert.True(StealRules.VictimReaches(0));
        Assert.True(StealRules.VictimReaches(StealRules.VictimCallReach));
        Assert.False(StealRules.VictimReaches(StealRules.VictimCallReach + 1));
        Assert.False(StealRules.VictimReaches(-1));
    }

    [Fact]
    public void VictimCall_TakesTheMomentAPersonNeedsToShout()
    {
        Assert.True(StealRules.VictimCallMin > TimeSpan.Zero);
        Assert.True(StealRules.VictimCallMax > StealRules.VictimCallMin);
    }

    [Fact]
    public void LookGap_AThiefInACrowdLooksAgainWithinSeconds()
    {
        Assert.True(StealRules.LookGapMin > TimeSpan.Zero);
        Assert.True(StealRules.LookGapMax > StealRules.LookGapMin);
        Assert.True(StealRules.LookGapMax < StealRules.Rest);
    }

    [Theory]
    [InlineData("Black Pearl%s%", 1, "Black Pearl")]
    [InlineData("Black Pearl%s%", 26, "Black Pearls")]
    [InlineData("loa%ves/f%", 1, "loaf")]
    [InlineData("loa%ves/f%", 3, "loaves")]
    [InlineData("arrow", 52, "arrow")]
    [InlineData("", 2, "")]
    [InlineData("broken%s", 2, "broken%s")]
    public void ItemWord_ReadsTheTilePluralMarks(string tileName, int amount, string expected) =>
        Assert.Equal(expected, StealRules.ItemWord(tileName, amount));

    [Fact]
    public void BritainGate_IsMoongateSouthOfGuardedTown()
    {
        Assert.Equal(BritainGateX, WorkSites.BritainGate.X);
        Assert.Equal(BritainGateY, WorkSites.BritainGate.Y);
        Assert.Equal(BritainGateZ, WorkSites.BritainGate.Z);
        Assert.NotEqual(CharactersFile.DefaultBankSpot, WorkSites.BritainGate);
    }

    [Fact]
    public void ThiefLift_GoesToUnguardedSpotBeforeSteal()
    {
        var file = CharactersFile.CreateDefault();
        var nyle = file.Facets[FacetNames.Felucca].FindRoster(PersonasFile.NyleId);
        Assert.NotNull(nyle);

        var lift = nyle.Routines["lift"];
        var goIndex = lift.FindIndex(s =>
            string.Equals(s.Skill, SkillKinds.GoTo, StringComparison.OrdinalIgnoreCase));
        var stealIndex = lift.FindIndex(s =>
            string.Equals(s.Skill, SkillKinds.Steal, StringComparison.OrdinalIgnoreCase));

        Assert.True(goIndex >= 0);
        Assert.True(stealIndex > goIndex);
        Assert.Equal(WorkSites.BritainGate, lift[goIndex].Target);
    }

    [Fact]
    public void MayLiftFromPeople_GuildMembersWithCleanHands()
    {
        Assert.True(StealRules.MayLiftFromPeople(inThievesGuild: true, kills: 0, suspendOnMurder: true));
        Assert.False(StealRules.MayLiftFromPeople(inThievesGuild: false, kills: 0, suspendOnMurder: true));
        Assert.False(StealRules.MayLiftFromPeople(inThievesGuild: true, kills: 1, suspendOnMurder: true));
        Assert.True(StealRules.MayLiftFromPeople(inThievesGuild: true, kills: 1, suspendOnMurder: false));
    }

    [Fact]
    public void TakesHotMark_OnlyTheBoldAndOnlySometimes()
    {
        Assert.False(StealRules.TakesHotMark(StealRules.BoldStealing - 1, 0));
        Assert.True(StealRules.TakesHotMark(StealRules.BoldStealing, 0));
        Assert.False(StealRules.TakesHotMark(StealRules.BoldStealing, StealRules.HotMarkPercent));
    }

    [Fact]
    public void Peek_SkillDecides_MasterAlwaysLooks()
    {
        Assert.True(StealRules.GotALook(StealRules.SureSnoop, PercentRoll.Scale - 1));
        Assert.True(StealRules.GotALook(60, 59));
        Assert.False(StealRules.GotALook(60, 60));
        Assert.True(StealRules.BlindGrab(0));
        Assert.False(StealRules.BlindGrab(StealRules.BlindGrabPercent));
    }

    [Fact]
    public void HidesAfter_AlwaysWhenSneakingIn()
    {
        Assert.True(StealRules.HidesAfter(sneaked: true, PercentRoll.Scale - 1));
        Assert.True(StealRules.HidesAfter(sneaked: false, 0));
        Assert.False(StealRules.HidesAfter(sneaked: false, StealRules.HideAfterPercent));
        Assert.True(StealRules.VictimCalls(0));
        Assert.False(StealRules.VictimCalls(StealRules.VictimCallsPercent));
    }

    [Fact]
    public void MarkRested_AfterItsRest()
    {
        var now = new DateTime(2026, 9, 24, 12, 0, 0);

        Assert.True(StealRules.MarkRested(default, now));
        Assert.False(StealRules.MarkRested(now, now + StealRules.MarkRest - TimeSpan.FromSeconds(1)));
        Assert.True(StealRules.MarkRested(now, now + StealRules.MarkRest));
    }

    [Fact]
    public void PeeksAgain_UntilThePeeksAreSpent()
    {
        // A failed peek was a walk-off at once: 219 thieves in a night gave up "could not get a look in the pack".
        Assert.True(StealRules.PeeksAgain(1));
        Assert.True(StealRules.PeeksAgain(StealRules.MaxPeeks - 1));
        Assert.False(StealRules.PeeksAgain(StealRules.MaxPeeks));
    }

    [Fact]
    public void HasRoomBeside_OnlyAMarkWithAFreeTileNextToIt()
    {
        // Marks boxed in by the bank crowd: 294 thieves gave up "could not get beside the mark in time".
        Assert.False(StealRules.HasRoomBeside(0));
        Assert.True(StealRules.HasRoomBeside(1));
    }

    [Fact]
    public void MayWorkCrowd_NeverUnderTheGuards()
    {
        Assert.False(StealRules.MayWorkCrowd(underGuards: true));
        Assert.True(StealRules.MayWorkCrowd(underGuards: false));
    }
}
