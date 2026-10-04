using System.Collections.Generic;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class ResistRulesTests
{
    private const string ExpectedKind = "Resist";
    private const double NoviceMagery = 10;
    private const double JourneymanMagery = 40;
    private const double AdeptMagery = 70;
    private const int FullMana = 100;
    private const int DryMana = 3;
    private const int CurseOnlyShortMana = 10;

    [Fact]
    public void Kind_IsResist()
    {
        Assert.Equal(ExpectedKind, ResistRules.Kind);
        Assert.Equal(ExpectedKind, new ResistSkill().Name);
    }

    [Fact]
    public void Options_AddCurseOnlyForAnAbleCaster()
    {
        Assert.DoesNotContain(ResistSpell.Curse, ResistRules.Options(JourneymanMagery));
        Assert.Contains(ResistSpell.Curse, ResistRules.Options(AdeptMagery));
        Assert.Contains(ResistSpell.Clumsy, ResistRules.Options(JourneymanMagery));
    }

    [Fact]
    public void Pick_RotatesThroughTheCurses()
    {
        var first = ResistRules.Pick(0, JourneymanMagery, FullMana, _ => true);
        var second = ResistRules.Pick(1, JourneymanMagery, FullMana, _ => true);
        var third = ResistRules.Pick(2, JourneymanMagery, FullMana, _ => true);

        Assert.Equal(ResistSpell.Clumsy, first);
        Assert.Equal(ResistSpell.Weaken, second);
        Assert.Equal(ResistSpell.Feeblemind, third);
    }

    [Fact]
    public void Pick_SkipsCursesWithoutReagents()
    {
        var owned = new HashSet<ResistSpell> { ResistSpell.Feeblemind };

        Assert.Equal(ResistSpell.Feeblemind, ResistRules.Pick(0, JourneymanMagery, FullMana, owned.Contains));
    }

    [Fact]
    public void Pick_NothingWhenDryPoorOrUnskilled()
    {
        Assert.Null(ResistRules.Pick(0, JourneymanMagery, DryMana, _ => true));
        Assert.Null(ResistRules.Pick(0, JourneymanMagery, FullMana, _ => false));
        Assert.Null(ResistRules.Pick(0, NoviceMagery, FullMana, _ => true));
    }

    [Fact]
    public void Pick_CurseNeedsItsOwnMana()
    {
        var curseOnly = new HashSet<ResistSpell> { ResistSpell.Curse };

        Assert.Null(ResistRules.Pick(0, AdeptMagery, CurseOnlyShortMana, curseOnly.Contains));
        Assert.Equal(ResistSpell.Curse, ResistRules.Pick(0, AdeptMagery, ResistRules.CurseMana, curseOnly.Contains));
    }
}
