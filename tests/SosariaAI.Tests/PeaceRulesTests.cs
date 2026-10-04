using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class PeaceRulesTests
{
    [Fact]
    public void PracticeWindow_MatchesModernUOPeacemakingCheck()
    {
        Assert.Equal(0, PeaceRules.PracticeMin);
        Assert.Equal(120, PeaceRules.PracticeMax);
    }

    [Fact]
    public void IsCombatTarget_RequiresCombatantAndNotSelf()
    {
        Assert.False(PeaceRules.IsCombatTarget(
            hasCombatant: false,
            isSelf: false,
            uncalmable: false,
            areaPeaceImmune: false));
        Assert.False(PeaceRules.IsCombatTarget(
            hasCombatant: true,
            isSelf: true,
            uncalmable: false,
            areaPeaceImmune: false));
        Assert.True(PeaceRules.IsCombatTarget(
            hasCombatant: true,
            isSelf: false,
            uncalmable: false,
            areaPeaceImmune: false));
    }

    [Fact]
    public void IsCombatTarget_RejectsUncalmableAndAreaPeaceImmune()
    {
        Assert.False(PeaceRules.IsCombatTarget(
            hasCombatant: true,
            isSelf: false,
            uncalmable: true,
            areaPeaceImmune: false));
        Assert.False(PeaceRules.IsCombatTarget(
            hasCombatant: true,
            isSelf: false,
            uncalmable: false,
            areaPeaceImmune: true));
    }

    [Fact]
    public void PeaceSkill_Name_IsPeace() =>
        Assert.Equal(SkillKinds.Peace, new PeaceSkill().Name);

    [Fact]
    public void PeaceSkill_Begin_NoBard_IsFalse() =>
        Assert.False(new PeaceSkill().Begin(null));
}
