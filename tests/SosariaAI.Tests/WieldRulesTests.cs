using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class WieldRulesTests
{
    private const double TrainedSkill = 70;

    [Fact]
    public void Mage_HoldsItsSpellbook_AndNoWeapon()
    {
        var mage = BuildPresets.MageNovice();

        Assert.True(WieldRules.HoldsSpellbook(mage));
        Assert.True(WieldRules.MayHoldSpellbook(mage));
        Assert.False(WieldRules.MayWield(mage, SkillName.Macing));
        Assert.False(WieldRules.MayWield(mage, SkillName.Swords));
        // A mage trains wrestling, and wrestles with empty hands.
        Assert.False(WieldRules.MayWield(mage, SkillName.Wrestling));
    }

    [Fact]
    public void Swordsman_WieldsOnlyTheSkillItTrains_AndPacksItsBook()
    {
        var swordsman = BuildPresets.SwordsmanNovice();

        Assert.True(WieldRules.MayWield(swordsman, SkillName.Swords));
        Assert.False(WieldRules.MayWield(swordsman, SkillName.Macing));
        Assert.False(WieldRules.MayWield(swordsman, SkillName.Archery));
        Assert.False(WieldRules.HoldsSpellbook(swordsman));
        Assert.False(WieldRules.MayHoldSpellbook(swordsman));
    }

    [Fact]
    public void Tamer_IsACaster()
    {
        Assert.True(WieldRules.HoldsSpellbook(BuildPresets.TamerNovice()));
        Assert.False(WieldRules.MayWield(BuildPresets.TamerNovice(), SkillName.Macing));
    }

    [Fact]
    public void NoBuild_HoldsWhatItFinds()
    {
        Assert.True(WieldRules.MayWield(null, SkillName.Macing));
        Assert.True(WieldRules.MayHoldSpellbook(null));
        Assert.False(WieldRules.HoldsSpellbook(null));
    }

    [Fact]
    public void Trains_ReadsTheBuildSkills_InAnyCase()
    {
        var build = new ResolvedBuild
        {
            Skills = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["tasteid"] = TrainedSkill }
        };

        Assert.True(build.Trains(SkillName.TasteID));
        Assert.False(build.Trains(SkillName.Swords));
        Assert.False(new ResolvedBuild().Trains(SkillName.Swords));
    }
}
