using System;
using SosariaAI.Combat;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>A sitting lasts until the mana is rested, or ends after a while.</summary>
public class MeditateRulesTests
{
    private const string ExpectedKind = "Meditate";
    private const int EmptyMana = 0;
    private const int FullMana = 50;
    private const int RestedMana = 45;
    private const int TiredMana = 44;
    private const int NoPool = 0;
    private static readonly DateTime Start = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Kind_IsMeditate()
    {
        Assert.Equal(ExpectedKind, MeditateRules.Kind);
        Assert.Equal(ExpectedKind, new MeditateSkill().Name);
    }

    [Fact]
    public void MayMeditate_NeedsSomeManaGone()
    {
        Assert.True(MeditateRules.MayMeditate(EmptyMana, FullMana));
        Assert.True(MeditateRules.MayMeditate(FullMana - MeditateRules.MinManaDeficit, FullMana));
        Assert.False(MeditateRules.MayMeditate(FullMana, FullMana));
    }

    [Fact]
    public void MayBegin_NoCharacter_IsFalse() =>
        Assert.False(MeditateRules.MayBegin(null));

    [Fact]
    public void IsRested_AtTheRestedLine()
    {
        Assert.Equal(RestedMana, (int)(FullMana * SelfCareRules.RestedManaFraction));
        Assert.True(MeditateRules.IsRested(RestedMana, FullMana));
        Assert.False(MeditateRules.IsRested(TiredMana, FullMana));
        Assert.True(MeditateRules.IsRested(EmptyMana, NoPool));
    }

    [Fact]
    public void SatTooLong_AfterTheLimit()
    {
        Assert.False(MeditateRules.SatTooLong(Start, Start));
        Assert.False(MeditateRules.SatTooLong(Start + MeditateRules.SitLimit - TimeSpan.FromSeconds(1), Start));
        Assert.True(MeditateRules.SatTooLong(Start + MeditateRules.SitLimit, Start));
        Assert.False(MeditateRules.SatTooLong(Start, default));
    }

    [Fact]
    public void Outcome_WorkedWhenTheManaRose()
    {
        Assert.Equal(SkillStatus.Done, MeditateRules.Outcome(EmptyMana, TiredMana));
        Assert.Equal(SkillStatus.Failed, MeditateRules.Outcome(TiredMana, TiredMana));
    }
}
