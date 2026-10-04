using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class AssistRulesTests
{
    [Fact]
    public void ShouldHelp_ArmedNeighbour_HelpsAnAllyInReach()
    {
        Assert.True(
            AssistRules.ShouldHelp(
                selfAlive: true,
                allyAlive: true,
                sameMap: true,
                distance: AssistRules.HelpRange,
                selfHasWeapon: true,
                aggressorIsEnemy: true,
                selfIsGhost: false,
                busyWithOtherFoe: false,
                underGuards: false
            )
        );
    }

    [Fact]
    public void ShouldHelp_UnderGuards_DoesNotHelp()
    {
        Assert.False(
            AssistRules.ShouldHelp(
                selfAlive: true,
                allyAlive: true,
                sameMap: true,
                distance: AssistRules.HelpRange,
                selfHasWeapon: true,
                aggressorIsEnemy: true,
                selfIsGhost: false,
                busyWithOtherFoe: false,
                underGuards: true
            )
        );
    }

    [Fact]
    public void ShouldHelp_UnarmedOrTooFarOrBusy_DoesNotHelp()
    {
        Assert.False(
            AssistRules.ShouldHelp(
                selfAlive: true,
                allyAlive: true,
                sameMap: true,
                distance: 0,
                selfHasWeapon: false,
                aggressorIsEnemy: true,
                selfIsGhost: false,
                busyWithOtherFoe: false,
                underGuards: false
            )
        );
        Assert.False(
            AssistRules.ShouldHelp(
                selfAlive: true,
                allyAlive: true,
                sameMap: true,
                distance: AssistRules.HelpRange + 1,
                selfHasWeapon: true,
                aggressorIsEnemy: true,
                selfIsGhost: false,
                busyWithOtherFoe: false,
                underGuards: false
            )
        );
        Assert.False(
            AssistRules.ShouldHelp(
                selfAlive: true,
                allyAlive: true,
                sameMap: true,
                distance: 1,
                selfHasWeapon: true,
                aggressorIsEnemy: true,
                selfIsGhost: false,
                busyWithOtherFoe: true,
                underGuards: false
            )
        );
    }

    [Fact]
    public void ShouldStandAndFight_OnlyWithAWeaponAndHelp()
    {
        Assert.True(AssistRules.ShouldStandAndFight(hasWeapon: true, helpersInRange: 1));
        Assert.False(AssistRules.ShouldStandAndFight(hasWeapon: true, helpersInRange: 0));
        Assert.False(AssistRules.ShouldStandAndFight(hasWeapon: false, helpersInRange: 3));
    }

    [Fact]
    public void HelpersToCall_CapsTheFoeAtMaxHelpers()
    {
        const int many = AssistRules.MaxHelpers * 3;

        Assert.Equal(AssistRules.MaxHelpers, AssistRules.HelpersToCall(alreadyOnFoe: 0, willing: many));
        Assert.Equal(AssistRules.MaxHelpers - 1, AssistRules.HelpersToCall(alreadyOnFoe: 1, willing: many));
        Assert.Equal(0, AssistRules.HelpersToCall(alreadyOnFoe: AssistRules.MaxHelpers, willing: many));
        Assert.Equal(0, AssistRules.HelpersToCall(alreadyOnFoe: many, willing: many));
    }

    [Fact]
    public void HelpersToCall_NeverMoreThanAreWilling()
    {
        Assert.Equal(1, AssistRules.HelpersToCall(alreadyOnFoe: 0, willing: 1));
        Assert.Equal(0, AssistRules.HelpersToCall(alreadyOnFoe: 0, willing: 0));
    }

    [Fact]
    public void HelpRange_ReachesPastAHuntersSight() =>
        Assert.True(AssistRules.HelpRange > SosariaCombat.HuntRangePerception);
}
