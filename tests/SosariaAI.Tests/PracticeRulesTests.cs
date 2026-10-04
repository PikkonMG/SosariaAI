using System;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class PracticeRulesTests
{
    private const int RollSweep = 1000;

    [Theory]
    [InlineData(SkillKinds.Tactics)]
    [InlineData(SkillKinds.Anatomy)]
    [InlineData(SkillKinds.Mage)]
    [InlineData(SkillKinds.Meditate)]
    [InlineData(SkillKinds.Hide)]
    [InlineData(SkillKinds.Wrestle)]
    [InlineData(SkillKinds.Music)]
    public void IsPractice_OneCheckSkills_RunAsSessions(string kind) =>
        Assert.True(PracticeRules.IsPractice(kind));

    [Theory]
    [InlineData(SkillKinds.Mine)]
    [InlineData(SkillKinds.Hunt)]
    [InlineData(SkillKinds.Recall)]
    [InlineData(SkillKinds.Gate)]
    [InlineData(SkillKinds.Tavern)]
    public void IsPractice_WalksAndTravel_AreNotPractice(string kind) =>
        Assert.False(PracticeRules.IsPractice(kind));

    [Fact]
    public void StandsStill_OnlySkillsAStepWouldBreak()
    {
        Assert.True(PracticeRules.StandsStill(SkillKinds.Meditate));
        Assert.True(PracticeRules.StandsStill(SkillKinds.Hide));
        Assert.False(PracticeRules.StandsStill(SkillKinds.Tactics));
    }

    [Fact]
    public void SessionLength_LastsMinutesNotOneTick()
    {
        for (var roll = -RollSweep; roll < RollSweep; roll++)
        {
            Assert.InRange(
                PracticeRules.SessionLength(roll),
                TimeSpan.FromSeconds(PracticeRules.MinSessionSeconds),
                TimeSpan.FromSeconds(PracticeRules.MaxSessionSeconds)
            );
        }
    }

    [Fact]
    public void Outcome_RunsUntilTimeOrTheSkillHasNothingLeft()
    {
        var length = TimeSpan.FromSeconds(PracticeRules.MinSessionSeconds);

        Assert.Equal(SkillStatus.Running, PracticeRules.Outcome(1, 0, TimeSpan.Zero, length));
        Assert.Equal(SkillStatus.Done, PracticeRules.Outcome(1, 0, length, length));
        Assert.Equal(SkillStatus.Done, PracticeRules.Outcome(2, PracticeRules.FailuresBeforeStop, TimeSpan.Zero, length));
        Assert.Equal(SkillStatus.Failed, PracticeRules.Outcome(0, PracticeRules.FailuresBeforeStop, TimeSpan.Zero, length));
        Assert.Equal(SkillStatus.Failed, PracticeRules.Outcome(0, 0, length, length));
    }
}
