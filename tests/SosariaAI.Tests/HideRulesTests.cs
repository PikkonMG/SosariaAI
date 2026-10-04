using System;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class HideRulesTests
{
    [Fact]
    public void HiddenFor_StaysInsideTheHiddenWindow()
    {
        Assert.Equal(TimeSpan.FromSeconds(HideRules.MinHiddenSeconds), HideRules.HiddenFor(0));
        Assert.Equal(
            TimeSpan.FromSeconds(HideRules.MaxHiddenSeconds),
            HideRules.HiddenFor(HideRules.MaxHiddenSeconds - HideRules.MinHiddenSeconds)
        );

        for (var roll = 0; roll < HideRules.MaxHiddenSeconds * 2; roll++)
        {
            var hidden = HideRules.HiddenFor(roll).TotalSeconds;
            Assert.InRange(hidden, HideRules.MinHiddenSeconds, HideRules.MaxHiddenSeconds);
        }
    }

    [Fact]
    public void HideSkill_Begin_NoThief_IsNotInTheWorld()
    {
        var hide = new HideSkill();

        Assert.False(hide.Begin(null));
        Assert.Equal(Skill.NotInWorldReason, hide.FailReason);
    }

    [Fact]
    public void ThiefLift_AddsHideBeforeSteal()
    {
        var file = CharactersFile.CreateDefault();
        var nyle = file.Facets[FacetNames.Felucca].FindRoster(PersonasFile.NyleId);
        Assert.NotNull(nyle);

        var lift = nyle.Routines["lift"];
        var hideIndex = lift.FindIndex(s =>
            string.Equals(s.Skill, SkillKinds.Hide, StringComparison.OrdinalIgnoreCase));
        var stealthIndex = lift.FindIndex(s =>
            string.Equals(s.Skill, SkillKinds.Stealth, StringComparison.OrdinalIgnoreCase));
        var stealIndex = lift.FindIndex(s =>
            string.Equals(s.Skill, SkillKinds.Steal, StringComparison.OrdinalIgnoreCase));

        Assert.True(hideIndex >= 0);
        Assert.Equal(hideIndex + 1, stealthIndex);
        Assert.Equal(stealthIndex + 1, stealIndex);
    }
}
