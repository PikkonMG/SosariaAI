using System.Collections.Generic;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class JevDecisionRulesTests
{
    private const double Floor = 0.5;

    private static readonly IReadOnlyList<JobOption> Options =
    [
        new("mine ore (Minoc Mine)", "miner:Mine:Minoc Mine", "gather", "full pack"),
        new("rest (Inn)", "miner:Rest:Inn", "recover", "unhurt")
    ];

    [Fact]
    public void Read_UsesAConfidentPick()
    {
        var verdict = JevDecisionRules.Read(Answers("rest (Inn)", 0.82), Options, Floor, error: null);

        Assert.Equal("miner:Rest:Inn", verdict.ActionId);
        Assert.Equal("jev 0.82", verdict.Source);
    }

    [Fact]
    public void Read_FallsBackBelowTheFloor()
    {
        var verdict = JevDecisionRules.Read(Answers("rest (Inn)", 0.31), Options, Floor, error: null);

        Assert.Null(verdict.ActionId);
        Assert.Equal("fallback: jev unsure 0.31", verdict.Source);
    }

    [Fact]
    public void Read_IgnoresAPickThatWasNotOffered()
    {
        var verdict = JevDecisionRules.Read(Answers("steal a horse", 0.99), Options, Floor, error: null);

        Assert.Null(verdict.ActionId);
        Assert.Equal(JevDecisionRules.NoPickSource, verdict.Source);
    }

    [Fact]
    public void Read_FallsBackOnAnErrorOrNoAnswers()
    {
        Assert.Equal("fallback: jev HTTP 429", JevDecisionRules.Read(null, Options, Floor, "HTTP 429").Source);
        Assert.Equal(JevDecisionRules.NoPickSource, JevDecisionRules.Read(null, Options, Floor, error: null).Source);
    }

    [Fact]
    public void Read_GreetsOnlyAboveTheGreetThreshold()
    {
        var answers = Answers("rest (Inn)", 0.9);
        answers[JevPrompt.GreetQuestion] = new SystemOneAnswer(null, 0, 0.7);

        Assert.True(JevDecisionRules.Read(answers, Options, Floor, error: null).Greet);

        answers[JevPrompt.GreetQuestion] = new SystemOneAnswer(null, 0, 0.4);

        Assert.False(JevDecisionRules.Read(answers, Options, Floor, error: null).Greet);
        Assert.False(JevDecisionRules.Read(Answers("rest (Inn)", 0.9), Options, Floor, error: null).Greet);
    }

    [Theory]
    [InlineData(0.5, null, 0.5)]
    [InlineData(0.5, 0.7, 0.7)]
    [InlineData(0.5, 0.2, 0.5)]
    public void Floor_TakesTheHigherOfTheTwo(double decision, double? provider, double expected) =>
        Assert.Equal(expected, JevDecisionRules.Floor(decision, provider));

    [Fact]
    public void SkipReason_AsksOnlyWithARealChoiceAndRoomToAsk()
    {
        Assert.Null(JevDecisionRules.SkipReason(3, forced: false, coolingDown: false, budgetAllows: true, rateAllows: true));
        Assert.Equal(JevDecisionRules.RuleSource, JevDecisionRules.SkipReason(3, true, false, true, true));
        Assert.Equal(JevDecisionRules.OneChoiceSource, JevDecisionRules.SkipReason(1, false, false, true, true));
        Assert.Equal(JevDecisionRules.CooldownSource, JevDecisionRules.SkipReason(3, false, true, true, true));
        Assert.Equal(JevDecisionRules.CapSource, JevDecisionRules.SkipReason(3, false, false, false, true));
        Assert.Equal(JevDecisionRules.RateSource, JevDecisionRules.SkipReason(3, false, false, true, false));
    }

    [Fact]
    public void Moment_ARoutinePickHasNone()
    {
        Assert.Null(JevDecisionRules.Moment(Routine));
        Assert.Null(JevDecisionRules.JobKind(scopeAll: false, moment: null));
        Assert.Equal(JevKind.Decision, JevDecisionRules.JobKind(scopeAll: true, moment: null));
    }

    [Fact]
    public void Moment_EachBigMomentIsNamed()
    {
        Assert.Equal("back from death", JevDecisionRules.Moment(Routine with { DiedRecently = true }));
        Assert.Equal("a player stands near", JevDecisionRules.Moment(Routine with { PlayerNear = true }));
        Assert.Equal(
            JevKind.BigMoment,
            JevDecisionRules.JobKind(scopeAll: false, JevDecisionRules.Moment(Routine with { DiedRecently = true }))
        );
    }

    [Fact]
    public void HasRoom_AShortQueueWaitsBehindTheSends()
    {
        const int Concurrency = 32;

        Assert.True(JevDecisionRules.HasRoom(Concurrency, Concurrency));
        Assert.True(JevDecisionRules.HasRoom(JevDecisionRules.QueueLimit(Concurrency) - 1, Concurrency));
        Assert.False(JevDecisionRules.HasRoom(JevDecisionRules.QueueLimit(Concurrency), Concurrency));
    }

    [Fact]
    public void ScopeSource_IsNotAFallback() =>
        Assert.False(JevDecisionRules.ScopeSource.StartsWith(GoalLoop.FallbackSource, System.StringComparison.Ordinal));

    private static readonly MomentFacts Routine = new(DiedRecently: false, PlayerNear: false);

    [Fact]
    public void IsForced_PartyFollowIsNotPutToJev()
    {
        var follow = new ScoredAction(new ActionId("party:Follow:leader"), "Follow", "party", ActionScorer.PartyFollowScore, "follow");
        var mine = new ScoredAction(new ActionId("miner:Mine:Minoc Mine"), "Mine", "miner", 9, "plan");

        Assert.True(JevDecisionRules.IsForced(new ScoreResult { Ranked = [follow], Winner = follow }));
        Assert.False(JevDecisionRules.IsForced(new ScoreResult { Ranked = [mine], Winner = mine }));
    }

    [Fact]
    public void RulesHold_ARedThatGoesToTheDenFirstIsNotPutToJev()
    {
        var run = new ScoredAction(new ActionId("red:Conflict:Yew gate"), "Conflict", "red", 9, "plan");
        var result = new ScoreResult { Ranked = [run], Winner = run };

        Assert.True(JevDecisionRules.RulesHold(result, denTripFirst: true));
        Assert.False(JevDecisionRules.RulesHold(result, denTripFirst: false));
    }

    [Fact]
    public void Sources_CountAsFallbackOrJev()
    {
        foreach (var source in new[]
                 {
                     JevDecisionRules.RuleSource, JevDecisionRules.OneChoiceSource, JevDecisionRules.CooldownSource,
                     JevDecisionRules.CapSource, JevDecisionRules.RateSource, JevDecisionRules.BusySource,
                     JevDecisionRules.SlowSource, JevDecisionRules.NoPickSource, JevDecisionRules.StaleSource
                 })
        {
            Assert.StartsWith(GoalLoop.FallbackSource, source);
        }
    }

    private static Dictionary<string, SystemOneAnswer> Answers(string choice, double confidence) =>
        new() { [JevPrompt.NextJobQuestion] = new SystemOneAnswer(choice, confidence, null) };
}
