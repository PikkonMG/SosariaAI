using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class LockpickRulesTests
{
    private const int CannotPick = 0;
    private const int MagicLock = -255;
    private const int EasyLockLevel = 10;
    private const int EasyRequiredSkill = 5;

    [Fact]
    public void Reach_IsOneTile() =>
        Assert.Equal(1, LockpickRules.ReachTiles);

    [Fact]
    public void IsPickable_RequiresLocked()
    {
        Assert.False(LockpickRules.IsPickable(locked: false, lockLevel: EasyLockLevel));
        Assert.True(LockpickRules.IsPickable(locked: true, lockLevel: EasyLockLevel));
    }

    [Fact]
    public void IsPickable_RejectsCannotPickAndMagicLock()
    {
        Assert.False(LockpickRules.IsPickable(locked: true, lockLevel: CannotPick));
        Assert.False(LockpickRules.IsPickable(locked: true, lockLevel: MagicLock));
        Assert.True(LockpickRules.IsPickable(locked: true, lockLevel: EasyLockLevel));
    }

    [Fact]
    public void IsTarget_NeverAChestInsideSomeonesHouse()
    {
        Assert.True(LockpickRules.IsTarget(locked: true, lockLevel: EasyLockLevel, insideHouse: false));
        Assert.False(LockpickRules.IsTarget(locked: true, lockLevel: EasyLockLevel, insideHouse: true));
        Assert.False(LockpickRules.IsTarget(locked: false, lockLevel: EasyLockLevel, insideHouse: false));
    }

    [Fact]
    public void SkillMeetsRequired_UsesLockpickingValue()
    {
        Assert.False(LockpickRules.SkillMeetsRequired(EasyRequiredSkill - 1, EasyRequiredSkill));
        Assert.True(LockpickRules.SkillMeetsRequired(EasyRequiredSkill, EasyRequiredSkill));
    }

    [Fact]
    public void ThiefLift_AddsLockpickAfterSteal()
    {
        var file = CharactersFile.CreateDefault();
        var nyle = file.Facets[FacetNames.Felucca].FindRoster(PersonasFile.NyleId);
        Assert.NotNull(nyle);

        var lift = nyle.Routines["lift"];
        var stealIndex = lift.FindIndex(s =>
            string.Equals(s.Skill, SkillKinds.Steal, StringComparison.OrdinalIgnoreCase));
        var lockpickIndex = lift.FindIndex(s =>
            string.Equals(s.Skill, SkillKinds.Lockpick, StringComparison.OrdinalIgnoreCase));

        Assert.True(stealIndex >= 0);
        Assert.Equal(stealIndex + 1, lockpickIndex);
    }
}
