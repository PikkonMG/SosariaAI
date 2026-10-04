using System.Collections.Generic;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class CombatStanceJevTests
{
    private const double Sure = 0.9;
    private const double Doubtful = 0.3;
    private const double JustUnderParalyze = 0.58;
    private const double CloseYes = 0.62;
    private const double CloserYes = 0.6;
    private const double BadlyHurt = 0.4;
    private const double Lightly = 0.75;
    private const int AllStances = 7;
    private const int LayaEnglishTokenLimit = 512;
    private const int Far = 9;
    private const int Close = 3;
    private const int Several = 3;
    private const int Once = 1;
    private const double Unhurt = 1.0;
    private const double Scratched = 0.95;
    private const double NearDeath = 0.1;
    private const double Empty = 0.0;
    private const int OwnerAskGapMs = 5000;

    [Fact]
    public void Build_OneYesNoPerStanceTheCharacterCanCarryOut()
    {
        var mage = CombatStanceJev.Build(StanceRulesTests.Mage());
        var warrior = CombatStanceJev.Build(StanceRulesTests.Warrior());

        Assert.All(mage.Questions.Values, q => Assert.Equal(SystemOneApi.NoulType, q.Type));
        Assert.All(mage.Questions.Values, q => Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(q.Criteria));
        Assert.Contains("kite", mage.Questions.Keys);
        Assert.Contains("press", warrior.Questions.Keys);
        Assert.Contains("flee", warrior.Questions.Keys);
        Assert.DoesNotContain("kite", warrior.Questions.Keys);
        Assert.DoesNotContain("paralyze_then_kite", warrior.Questions.Keys);
    }

    [Fact]
    public void Build_TheStateIsAFewLinesOfWords()
    {
        var decision = CombatStanceJev.Build(
            StanceRulesTests.Mage() with
            {
                HitsFraction = BadlyHurt,
                RecentInterrupts = Several,
                Outlook = FightOutlook.Winning,
                LightDamage = true,
                Cornered = true,
                HasBandage = true
            }
        );
        var state = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(decision.State);

        Assert.Contains("badly hurt", (string)state["you"]);
        Assert.Contains("next to you", (string)state["foe"]);
        var fight = (string)state["fight"];
        Assert.Contains("winning the trade", fight);
        Assert.Contains("taking light damage", fight);
        Assert.Contains("spells broken again and again", fight);
        Assert.Contains("cornered", fight);
        Assert.Contains("bandages", (string)state["you_have"]);
    }

    [Fact]
    public void Build_TheFullestStanceRequestFitsLayasContext()
    {
        // Every stance feasible, every word in the state: the most a stance call can carry.
        var busiest = StanceRulesTests.Mage() with
        {
            HitsFraction = BadlyHurt,
            Poisoned = true,
            NeedsCare = true,
            FoeCasting = true,
            AdjacentFoes = Several,
            RecentInterrupts = Several,
            AlliesNear = Several,
            Outlook = FightOutlook.Losing,
            LightDamage = true,
            HasHealPotion = true,
            HasBandage = true
        };
        var decision = CombatStanceJev.Build(busiest);
        var tokens = JevPromptTests.Tokens(decision);

        Assert.Equal(AllStances, decision.Questions.Count);
        Assert.True(tokens < LayaEnglishTokenLimit, $"about {tokens} tokens: {JevPromptTests.Body(decision)}");
    }

    [Fact]
    public void Key_SameWordsSameKey_NewWordsNewKey()
    {
        var mage = StanceRulesTests.Mage();

        Assert.Equal(CombatStanceJev.Key(mage), CombatStanceJev.Key(mage with { HitsFraction = Scratched }));
        Assert.NotEqual(CombatStanceJev.Key(mage), CombatStanceJev.Key(mage with { HitsFraction = Lightly }));
        Assert.NotEqual(CombatStanceJev.Key(mage), CombatStanceJev.Key(mage with { FoeDistance = Far }));
        Assert.NotEqual(CombatStanceJev.Key(mage), CombatStanceJev.Key(mage with { Outlook = FightOutlook.Losing }));
    }

    [Fact]
    public void Read_AConfidentFeasibleStance()
    {
        var verdict = CombatStanceJev.Read(Answers(("kite", Sure), ("press", Doubtful)), StanceRulesTests.Mage());

        Assert.Equal(CombatStance.Kite, verdict.Stance);
        Assert.Equal(Sure, verdict.Confidence);
        Assert.Equal(BrainProviders.JevName, verdict.Source);
    }

    [Fact]
    public void Read_NamesTheProviderThatAnswered_EvenWhenUnsure()
    {
        const string Laya = "laya";

        Assert.Equal(Laya, CombatStanceJev.Read(Answers(("kite", Sure)), StanceRulesTests.Mage(), Laya).Source);
        Assert.Equal(Laya, CombatStanceJev.Read(null, StanceRulesTests.Mage(), Laya).Source);
    }

    [Fact]
    public void Read_ARiskyStanceNeedsMoreConfidence()
    {
        var mage = StanceRulesTests.Mage();

        Assert.Null(CombatStanceJev.Read(Answers(("paralyze_then_kite", JustUnderParalyze)), mage).Stance);
        Assert.Equal(CombatStance.ParalyzeThenKite, CombatStanceJev.Read(Answers(("paralyze_then_kite", Sure)), mage).Stance);
    }

    [Fact]
    public void Read_TwoCloseYeses_TheRulesPick()
    {
        var verdict = CombatStanceJev.Read(Answers(("press", CloseYes), ("kite", CloserYes)), StanceRulesTests.Mage());

        Assert.Null(verdict.Stance);
    }

    [Fact]
    public void Read_AStanceTheCharacterCannotCarryOut_IsNoAnswer()
    {
        Assert.Null(CombatStanceJev.Read(Answers(("kite", Sure), ("press", Doubtful)), StanceRulesTests.Warrior()).Stance);
        Assert.Null(
            CombatStanceJev.Read(Answers(("kite", Sure), ("press", Doubtful)), StanceRulesTests.Mage() with { Cornered = true }).Stance
        );
    }

    [Fact]
    public void Read_NoAnswers_TheRulesPick()
    {
        Assert.Null(CombatStanceJev.Read(null, StanceRulesTests.Mage()).Stance);
        Assert.Null(CombatStanceJev.Read(Answers(("dance", Sure)), StanceRulesTests.Mage()).Stance);
    }

    [Fact]
    public void Words_BucketTheNumbers()
    {
        Assert.Equal("unhurt", CombatStanceJev.HealthWord(Unhurt));
        Assert.Equal("near death", CombatStanceJev.HealthWord(NearDeath));
        Assert.Equal("empty", CombatStanceJev.ManaWord(Empty));
        Assert.Equal("close", CombatStanceJev.DistanceWord(Close));
        Assert.Equal("far", CombatStanceJev.DistanceWord(Far));
        Assert.Equal("several", CombatStanceJev.CountWord(Several));
        Assert.Equal("one enemy", CombatStanceJev.Counted(Once, "enemy", "enemies"));
        Assert.Equal("several enemies", CombatStanceJev.Counted(Several, "enemy", "enemies"));
        Assert.Equal("once", CombatStanceJev.InterruptWord(Once));
        Assert.Equal("winning the trade", CombatStanceJev.TradeWord(FightOutlook.Winning));
    }

    [Fact]
    public void AskPacing_FiveSecondsAndACapPerFight()
    {
        Assert.True(CombatStanceJev.AskGapMs >= OwnerAskGapMs);
        Assert.True(CombatStanceJev.StanceHoldMs >= CombatStanceJev.AskGapMs);
        Assert.True(CombatStanceJev.MaxAsksPerFight > 0);
    }

    private static Dictionary<string, SystemOneAnswer> Answers(params (string Stance, double Yes)[] yeses)
    {
        var answers = new Dictionary<string, SystemOneAnswer>();

        foreach (var (stance, yes) in yeses)
        {
            answers[stance] = new SystemOneAnswer(null, 0, yes);
        }

        return answers;
    }
}
