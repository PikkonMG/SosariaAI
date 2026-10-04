using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class SkillReadinessTests
{
    [Fact]
    public void GatedKinds_CoverTheJobsThatFailedAtTheirStart()
    {
        // The Britain bank run: Cook, Tame, Discord, Peace, Provoke and Beg failed within
        // twenty seconds of starting, over and over; a woodcutter with no hatchet picked
        // "cut wood" three times in a row and failed at once each time.
        string[] failedAtStart =
        [
            SkillKinds.Tame, SkillKinds.Discord, SkillKinds.Peace, SkillKinds.Provoke, SkillKinds.Beg, SkillKinds.Cook,
            SkillKinds.Mine, SkillKinds.Lumberjack, SkillKinds.Fish
        ];

        foreach (var kind in failedAtStart)
        {
            Assert.Contains(kind, SkillReadiness.GatedKinds);
        }
    }

    [Fact]
    public void GatedKinds_EveryStationTradeNeedsItsTool()
    {
        // A Trinsic tinker whose tools broke found no tinker tools for sale in reach and
        // failed "could not get a tool" every half minute.
        foreach (var career in CraftCareerRules.Careers)
        {
            Assert.Contains(career.Kind, SkillReadiness.GatedKinds);
        }
    }

    [Fact]
    public void GatedKinds_CoverTheBeastWork()
    {
        // Tamers failed "tend a beast" 267 times and "study a beast" 104 times at banks with no beast near.
        Assert.Contains(SkillKinds.Vet, SkillReadiness.GatedKinds);
        Assert.Contains(SkillKinds.Lore, SkillReadiness.GatedKinds);
    }

    [Fact]
    public void GatedKinds_CoverCartography()
    {
        // Mapmakers out of blank maps failed "could not get materials" 142 times in one evening.
        Assert.Contains(SkillKinds.Cartography, SkillReadiness.GatedKinds);
    }

    [Fact]
    public void GatedKinds_CoverTheThiefWorkWithNothingToWorkOn()
    {
        // One night: DetectHidden 298 and RemoveTrap 256 "the skill had nothing to work on",
        // Lockpick 228 "no lockpicks", Poison 198 "the skill had nothing to work on".
        Assert.Contains(SkillKinds.DetectHidden, SkillReadiness.GatedKinds);
        Assert.Contains(SkillKinds.RemoveTrap, SkillReadiness.GatedKinds);
        Assert.Contains(SkillKinds.Lockpick, SkillReadiness.GatedKinds);
        Assert.Contains(SkillKinds.Poison, SkillReadiness.GatedKinds);
    }

    [Fact]
    public void HasWork_NoneOutOfTheWorld()
    {
        Assert.False(DetectHiddenSkill.HasWork(null));
        Assert.False(RemoveTrapSkill.HasWork(null));
        Assert.False(LockpickSkill.HasWork(null));
    }

    [Fact]
    public void Unmet_WithoutACharacter_IsEmpty() =>
        Assert.Empty(SkillReadiness.Unmet(null));
}
