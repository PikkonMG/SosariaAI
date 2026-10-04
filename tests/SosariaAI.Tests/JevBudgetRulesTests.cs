using System;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class JevBudgetRulesTests
{
    private const double Fresh = 0.2;
    private const double Tight = 0.85;
    private const double Spent = 1.0;
    private const long Cap = 1000;

    [Fact]
    public void Allows_EveryKindWhileTheHourIsFresh()
    {
        foreach (var kind in Enum.GetValues<JevKind>())
        {
            Assert.True(JevBudgetRules.Allows(kind, Fresh));
        }
    }

    [Fact]
    public void Allows_PastEightyPercent_OnlyTheCallsThatMatterMost()
    {
        Assert.True(JevBudgetRules.Allows(JevKind.PersonFight, Tight));
        Assert.True(JevBudgetRules.Allows(JevKind.BigMoment, Tight));
        Assert.True(JevBudgetRules.Allows(JevKind.Trade, Tight));
        Assert.False(JevBudgetRules.Allows(JevKind.MonsterFight, Tight));
        Assert.False(JevBudgetRules.Allows(JevKind.Decision, Tight));
        Assert.False(JevBudgetRules.Allows(JevKind.SpeechGate, Tight));
    }

    [Fact]
    public void Allows_NothingAtTheCap()
    {
        foreach (var kind in Enum.GetValues<JevKind>())
        {
            Assert.False(JevBudgetRules.Allows(kind, Spent));
        }
    }

    [Fact]
    public void UsedShare_AZeroCapReadsAsSpent()
    {
        Assert.Equal(JevBudgetRules.FullShare, JevBudgetRules.UsedShare(0, 0));
        Assert.Equal(0.5, JevBudgetRules.UsedShare(Cap / 2, Cap));
    }

    [Fact]
    public void Names_EveryKindHasItsWords()
    {
        foreach (var kind in Enum.GetValues<JevKind>())
        {
            Assert.False(string.IsNullOrWhiteSpace(JevBudgetRules.Names[kind]));
        }
    }

    [Fact]
    public void FightKind_SplitsPeopleFromMonsters()
    {
        Assert.Equal(JevKind.PersonFight, JevBudgetRules.FightKind(foeIsPerson: true));
        Assert.Equal(JevKind.MonsterFight, JevBudgetRules.FightKind(foeIsPerson: false));
    }
}
