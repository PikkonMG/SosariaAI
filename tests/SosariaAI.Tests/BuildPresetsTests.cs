using System;
using System.Linq;
using Server;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class BuildPresetsTests
{
    [Fact]
    public void Resolve_SwordsmanNovice_UsesListedSkills()
    {
        var build = BuildPresets.Resolve(new BuildDefinition { Preset = BuildPresets.Swordsman });
        Assert.Equal(CombatStyle.Melee, build.Style);
        Assert.Equal(CharacterRole.Fighter, build.Role);
        Assert.Equal(BuildPresets.NoviceFightSkill, build.Skills["Swords"]);
        Assert.Equal(BuildPresets.NoviceStrength, build.Strength);
        Assert.Contains("Katana", build.Kit);
        Assert.True(build.CanHeal);
    }

    [Fact]
    public void Resolve_Veteran_TrainsGrandmasterSkillsInsideTheEraCaps()
    {
        var build = BuildPresets.Resolve(new BuildDefinition { Preset = BuildPresets.Archer, Veteran = true });
        Assert.Equal(EraBuildCaps.SkillCap, build.Skills["Archery"]);
        Assert.Equal(ClassBuilds.ArcherStats.Strength, build.Strength);
        Assert.Equal(ClassBuilds.ArcherStats.Dexterity, build.Dexterity);
        Assert.Equal(ClassBuilds.ArcherStats.Intelligence, build.Intelligence);
        Assert.Equal(CombatStyle.Archer, build.Style);
    }

    [Fact]
    public void Resolve_Mage_CarriesSpellbookAndReagents()
    {
        var build = BuildPresets.Resolve(new BuildDefinition { Preset = BuildPresets.Mage, Veteran = true });
        Assert.Equal(EraBuildCaps.SkillCap, build.Skills["Magery"]);
        Assert.Equal(EraBuildCaps.SkillCap, build.Skills["EvalInt"]);
        Assert.Contains("Spellbook", build.Kit);
        Assert.Contains("BlackPearl", build.Kit);
        Assert.Contains("SulfurousAsh", build.Kit);
    }

    [Theory]
    [InlineData(BuildPresets.Swordsman)]
    [InlineData(BuildPresets.Archer)]
    [InlineData(BuildPresets.Mage)]
    [InlineData(BuildPresets.Thief)]
    [InlineData(BuildPresets.Tamer)]
    [InlineData(null)]
    public void Resolve_EveryVeteranPreset_StaysInsideTheEraCaps(string preset)
    {
        var build = BuildPresets.Resolve(new BuildDefinition { Preset = preset, Veteran = true });

        Assert.True(build.Strength + build.Dexterity + build.Intelligence <= EraBuildCaps.StatTotalCap);
        Assert.All(new[] { build.Strength, build.Dexterity, build.Intelligence }, stat => Assert.InRange(stat, EraBuildCaps.MinStat, EraBuildCaps.StatCap));
        Assert.True(build.Skills.Values.Sum() <= EraBuildCaps.SkillTotalCap);
    }

    [Fact]
    public void Resolve_AuthoredStatsAboveTheCap_AreFitted()
    {
        var build = BuildPresets.Resolve(
            new BuildDefinition
            {
                Preset = BuildPresets.Swordsman,
                Stats = new StatsDefinition { Strength = 120, Dexterity = 100, Intelligence = 100 }
            }
        );

        Assert.True(build.Strength + build.Dexterity + build.Intelligence <= EraBuildCaps.StatTotalCap);
        Assert.True(build.Strength <= EraBuildCaps.StatCap);
    }

    [Fact]
    public void Resolve_OverridesReplacePresetKitAndHeal()
    {
        var build = BuildPresets.Resolve(
            new BuildDefinition
            {
                Preset = BuildPresets.Mage,
                CanHeal = true,
                Kit = ["Spellbook"],
                Skills = new() { ["Magery"] = 55 }
            }
        );

        Assert.Equal(55, build.Skills["Magery"]);
        Assert.Equal(["Spellbook"], build.Kit);
        Assert.True(build.CanHeal);
        Assert.Equal(CombatStyle.Mage, build.Style);
    }

    [Fact]
    public void Resolve_MissingPreset_IsWorker()
    {
        var build = BuildPresets.Resolve(null);
        Assert.Equal(CharacterRole.Worker, build.Role);
        Assert.Equal(CombatStyle.Melee, build.Style);
        Assert.True(build.CanHeal);
        Assert.Equal(BuildPresets.WorkerFightSkill, build.Skills["Swords"]);
        Assert.Equal(BuildPresets.WorkerFightSkill, build.Skills["Tactics"]);
        Assert.Equal(BuildPresets.WorkerFightSkill, build.Skills["Healing"]);
        Assert.Contains("LeatherChest", build.Kit);
        Assert.Contains("Hatchet", build.Kit);
        Assert.Equal(BuildPresets.WorkerStrength, build.Strength);
    }

    [Fact]
    public void Presets_EverySkillKeyParsesAsSkillName()
    {
        ResolvedBuild[] presets =
        [
            BuildPresets.WorkerDefault,
            BuildPresets.SwordsmanNovice(),
            BuildPresets.ArcherNovice(),
            BuildPresets.MageNovice(),
            BuildPresets.ThiefNovice(),
            BuildPresets.TamerNovice()
        ];

        foreach (var preset in presets)
        {
            foreach (var key in preset.Skills.Keys)
            {
                Assert.True(Enum.TryParse(key, ignoreCase: true, out SkillName _), $"{key} is not a SkillName");
            }
        }
    }

    [Fact]
    public void TamerPreset_IsAMageTamerWithABook()
    {
        var build = BuildPresets.TamerNovice();

        Assert.Equal(CombatStyle.Mage, build.Style);
        Assert.Equal(CharacterRole.Fighter, build.Role);
        Assert.True(build.Skills.ContainsKey(nameof(SkillName.Magery)));
        Assert.True(build.Skills.ContainsKey(nameof(SkillName.Veterinary)));
        Assert.True(build.Skills.ContainsKey(nameof(SkillName.AnimalLore)));
        Assert.True(build.Skills[nameof(SkillName.AnimalTaming)] > build.Skills[nameof(SkillName.Magery)]);
        Assert.Contains(ClassKits.Spellbook, build.Kit);
        Assert.All(ClassKits.AllReagents, reagent => Assert.Contains(reagent, build.Kit));
        Assert.DoesNotContain("Club", build.Kit);
    }
}
