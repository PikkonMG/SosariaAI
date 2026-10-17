using System.Linq;
using SosariaAI.Combat;
using SosariaAI.Skills;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class TameStandRulesTests
{
    private const double FullHits = 1.0;
    private const double WornHits = 0.5;
    private const double Grandmaster = 100;
    private const double NoSkill = 0;
    private const int Adjacent = 1;

    [Fact]
    public void Angered_IsAnAngryWildBeastOnTheTamer_NotARefusal()
    {
        // The engine gives the skill clock back on an anger as on a refusal; only the anger keeps the beast.
        Assert.True(TameStandRules.Angered(angersOnTame: true, onTamer: true, controlled: false));
        Assert.False(TameStandRules.Angered(angersOnTame: false, onTamer: true, controlled: false));
        Assert.False(TameStandRules.Angered(angersOnTame: true, onTamer: false, controlled: false));
        Assert.False(TameStandRules.Angered(angersOnTame: true, onTamer: true, controlled: true));
    }

    [Fact]
    public void HoldFor_CalmsFirst_ThenParalyzes_OnlyAnAngryFreeBeast()
    {
        Assert.Equal(QuarryHold.Calm, TameStandRules.HoldFor(true, true, held: false, canCalm: true, canParalyze: true));
        Assert.Equal(QuarryHold.Paralyze, TameStandRules.HoldFor(true, true, held: false, canCalm: false, canParalyze: true));
        Assert.Equal(QuarryHold.None, TameStandRules.HoldFor(true, true, held: false, canCalm: false, canParalyze: false));
        Assert.Equal(QuarryHold.None, TameStandRules.HoldFor(true, true, held: true, canCalm: true, canParalyze: true));
        Assert.Equal(QuarryHold.None, TameStandRules.HoldFor(true, onTamer: false, held: false, canCalm: true, canParalyze: true));
        Assert.Equal(QuarryHold.None, TameStandRules.HoldFor(angersOnTame: false, true, held: false, canCalm: true, canParalyze: true));
    }

    [Fact]
    public void StepsBack_WhenAFreeBeastHasWornTheTamerDown()
    {
        Assert.True(TameStandRules.StepsBack(onTamer: true, held: false, WornHits));
        Assert.False(TameStandRules.StepsBack(onTamer: true, held: false, FullHits));
        Assert.False(TameStandRules.StepsBack(onTamer: true, held: true, WornHits));
        Assert.False(TameStandRules.StepsBack(onTamer: false, held: false, WornHits));
        Assert.False(TameStandRules.StepsBack(onTamer: true, held: false, TameStandRules.StepBackHitsFraction));
    }

    [Fact]
    public void StepsAway_OutToFiveTiles_NoneFromThreeOn()
    {
        Assert.Equal(TameStandRules.StepBackMaxTiles - Adjacent, TameStandRules.StepsAway(Adjacent));
        Assert.Equal(0, TameStandRules.StepsAway(TameStandRules.StepBackMinTiles));
        Assert.Equal(0, TameStandRules.StepsAway(TameStandRules.StepBackMaxTiles));
        Assert.True(TameStandRules.StepBackMinTiles < TameStandRules.StepBackMaxTiles);
    }

    [Fact]
    public void Recovered_AtTheOutOfFightHealLine_AboveTheStepBackLine()
    {
        Assert.True(TameStandRules.Recovered(SelfCareRules.RestHealFraction));
        Assert.False(TameStandRules.Recovered(TameStandRules.StepBackHitsFraction));
        Assert.True(TameStandRules.StepBackHitsFraction > TameSkill.GiveUpHitsFraction);
    }

    [Fact]
    public void HealsSelf_ByBandageOrHealSpell_NotWithNeither()
    {
        Assert.True(TameStandRules.HealsSelf(Grandmaster, hasBandage: true, NoSkill, healInHand: false, greaterHealInHand: false));
        Assert.False(TameStandRules.HealsSelf(NoSkill, hasBandage: true, NoSkill, healInHand: false, greaterHealInHand: false));
        Assert.True(TameStandRules.HealsSelf(NoSkill, hasBandage: false, Grandmaster, healInHand: true, greaterHealInHand: false));
        Assert.True(TameStandRules.HealsSelf(NoSkill, hasBandage: false, Grandmaster, healInHand: false, greaterHealInHand: true));
        Assert.False(TameStandRules.HealsSelf(NoSkill, hasBandage: false, NoSkill, healInHand: true, greaterHealInHand: true));
        Assert.False(TameStandRules.HealsSelf(Grandmaster, hasBandage: false, Grandmaster, healInHand: false, greaterHealInHand: false));
    }

    [Fact]
    public void TameLines_PromiseNoRunAndNoName()
    {
        // The tamer neither runs on a failed try nor names a pet.
        var lines = TalkDefaults.All
            .Where(topic => topic.Name is TalkCategory.TameSuccess or TalkCategory.TameFail)
            .SelectMany(topic => topic.Lines)
            .ToList();

        Assert.NotEmpty(lines);
        Assert.DoesNotContain(lines, line => line.Contains("run") || line.Contains("name it"));
    }
}
