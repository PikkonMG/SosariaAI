using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class DefendRulesTests
{
    [Fact]
    public void ShouldFightBack_AliveFoeOnTheSameMap()
    {
        Assert.True(DefendRules.ShouldFightBack(selfAlive: true, ghost: false, foeAlive: true, sameMap: true));
        Assert.False(DefendRules.ShouldFightBack(selfAlive: true, ghost: false, foeAlive: false, sameMap: true));
        Assert.False(DefendRules.ShouldFightBack(selfAlive: true, ghost: true, foeAlive: true, sameMap: true));
        Assert.False(DefendRules.ShouldFightBack(selfAlive: true, ghost: false, foeAlive: true, sameMap: false));
        Assert.False(DefendRules.ShouldFightBack(
            selfAlive: true,
            ghost: false,
            foeAlive: true,
            sameMap: true,
            fleeing: true
        ));
    }

    [Fact]
    public void ShouldFightBack_ARunnerDoesNotTurnBack_OnAPersonEither() =>
        Assert.False(DefendRules.ShouldFightBack(selfAlive: true, ghost: false, foeAlive: true, sameMap: true, fleeing: true));

    [Fact]
    public void ShouldAnswerHit_PersonOrEnemy_NotIdle()
    {
        Assert.True(DefendRules.ShouldAnswerHit(
            selfAlive: true,
            ghost: false,
            fromAlive: true,
            fromIsPerson: true,
            fromIsEnemy: false
        ));
        Assert.True(DefendRules.ShouldAnswerHit(
            selfAlive: true,
            ghost: false,
            fromAlive: true,
            fromIsPerson: false,
            fromIsEnemy: true
        ));
        Assert.False(DefendRules.ShouldAnswerHit(
            selfAlive: true,
            ghost: false,
            fromAlive: true,
            fromIsPerson: false,
            fromIsEnemy: false
        ));
        Assert.False(DefendRules.ShouldAnswerHit(
            selfAlive: true,
            ghost: true,
            fromAlive: true,
            fromIsPerson: true,
            fromIsEnemy: true
        ));
    }

    [Fact]
    public void MayRestoreTownStance_NotWhileAFoeIsOnThem()
    {
        Assert.False(DefendRules.MayRestoreTownStance(hasAliveFoe: true));
        Assert.True(DefendRules.MayRestoreTownStance(hasAliveFoe: false));
    }

    [Fact]
    public void MayTakeGate_NotWhileAFoeIsOnThem()
    {
        Assert.False(DefendRules.MayTakeGate(hasAliveFoe: true));
        Assert.True(DefendRules.MayTakeGate(hasAliveFoe: false));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void RunsBare_AnUnarmedPersonRunsFromAPerson(bool foeIsPerson, bool armed, bool runs) =>
        Assert.Equal(runs, DefendRules.RunsBare(foeIsPerson, armed));
}
