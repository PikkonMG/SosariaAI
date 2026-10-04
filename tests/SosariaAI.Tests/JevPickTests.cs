using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Deliberation;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class JevPickAskerCollection
{
    public const string Name = "Jev pick asker";
}

public class JevPickTests
{
    private const double Floor = 0.5;
    private const double Sure = 0.8;
    private const double Shaky = 0.3;
    private const string Provider = "jev";
    private const string OtherProvider = "jev-backup";

    private static readonly IReadOnlyList<JevPickOption> Options =
    [
        new("go", "ride out now", "the band is small"),
        new("stay", "wait for a better time", "the band is strong")
    ];

    [Fact]
    public void Build_OneChoiceQuestionWithAContrastRubricPerOption()
    {
        var decision = JevPick.Build(new Dictionary<string, object> { ["band"] = "you and two friends" }, "Ride now?", Options);

        var question = Assert.Single(decision.Questions);
        Assert.Equal(JevPick.Question, question.Key);
        Assert.Equal(SystemOneApi.ChoiceType, question.Value.Type);
        var criteria = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(question.Value.Criteria);
        Assert.Equal("ride out now; wrong when the band is small", criteria["go"]);
        Assert.Equal("wait for a better time; wrong when the band is strong", criteria["stay"]);
    }

    [Fact]
    public void Read_NoAnswerKeepsTheRule()
    {
        var verdict = JevPick.Read(null, Options, Floor, Provider);

        Assert.Null(verdict.Choice);
        Assert.Equal(JevPick.NoAnswerSource, verdict.Source);
    }

    [Fact]
    public void Read_AnOptionNotOfferedKeepsTheRule()
    {
        var verdict = JevPick.Read(Answer("flee", Sure), Options, Floor, Provider);

        Assert.Null(verdict.Choice);
        Assert.Equal(JevPick.NoPickSource, verdict.Source);
    }

    [Fact]
    public void Read_AnUnsureAnswerKeepsTheRuleAndSaysWhy()
    {
        var verdict = JevPick.Read(Answer("go", Shaky), Options, Floor, Provider);

        Assert.Null(verdict.Choice);
        Assert.Equal("rules: jev unsure 0.30", verdict.Source);
    }

    [Fact]
    public void Read_ASureAnswerNamesTheOfferedKeyAndTheProvider()
    {
        var verdict = JevPick.Read(Answer(" STAY ", Sure), Options, Floor, OtherProvider);

        Assert.Equal("stay", verdict.Choice);
        Assert.Equal("jev-backup 0.80", verdict.Source);
    }

    [Fact]
    public void Sources_OfTheRulesAllSayRules()
    {
        foreach (var source in new[] { JevPick.NoAnswerSource, JevPick.NoPickSource, JevPick.SlowSource })
        {
            Assert.StartsWith(JevPick.RulesSource, source);
        }
    }

    [Fact]
    public void Wait_HoldsInsideTheWaitThenTakesTheAnswer()
    {
        var wait = new JevWait();
        var asked = At(0);
        Action<JevPickVerdict> reply = null;

        Assert.True(wait.Ask(asked, answer => (reply = answer) != null));
        Assert.True(wait.Waiting(asked));
        Assert.False(wait.TryTake(asked, out _));

        reply(new JevPickVerdict("go", Sure, Provider));

        Assert.False(wait.Waiting(asked));
        Assert.True(wait.TryTake(asked, out var verdict));
        Assert.Equal("go", verdict.Choice);
        Assert.False(wait.TryTake(asked, out _));
    }

    [Fact]
    public void Wait_NoAnswerInTimeIsTooSlowAndALateAnswerAppliesToNothing()
    {
        var wait = new JevWait();
        var asked = At(0);
        Action<JevPickVerdict> reply = null;
        wait.Ask(asked, answer => (reply = answer) != null);

        var overdue = asked + JevWait.AnswerWait;

        Assert.False(wait.Waiting(overdue));
        Assert.True(wait.TryTake(overdue, out var verdict));
        Assert.Equal(JevPick.TooSlow, verdict);

        reply(new JevPickVerdict("go", Sure, Provider));

        Assert.False(wait.Waiting(overdue));
        Assert.False(wait.TryTake(overdue, out _));
    }

    [Fact]
    public void Wait_ARefusedAskLeavesNothingOpen()
    {
        var wait = new JevWait();

        Assert.False(wait.Ask(At(0), _ => false));
        Assert.False(wait.Waiting(At(0)));
        Assert.False(wait.TryTake(At(0), out _));
    }

    [Fact]
    public void Wait_AnAnswerLeftUnreadGoesStale()
    {
        var wait = new JevWait();
        var asked = At(0);
        wait.Ask(asked, answer => { answer(new JevPickVerdict("go", Sure, Provider)); return true; });

        Assert.False(wait.TryTake(asked + JevWait.AnswerKeep, out _));
        Assert.False(wait.TryTake(asked, out _));
    }

    [Fact]
    public void Wait_AnAnswerToAnOlderQuestionAppliesToNothing()
    {
        var wait = new JevWait();
        Action<JevPickVerdict> first = null;
        wait.Ask(At(0), answer => (first = answer) != null);
        wait.Ask(At(1), _ => true);

        first(new JevPickVerdict("go", Sure, Provider));

        Assert.True(wait.Waiting(At(1)));
    }

    private static DateTime At(int seconds) => new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);

    private static Dictionary<string, SystemOneAnswer> Answer(string choice, double confidence) =>
        new() { [JevPick.Question] = new SystemOneAnswer(choice, confidence, null) };
}

[Collection(JevPickAskerCollection.Name)]
public class JevPickAskTests
{
    private const double Sure = 0.9;
    private const double RaisedFloor = 0.95;
    private const string Provider = "jev";

    private static readonly IReadOnlyList<JevPickOption> Options =
    [
        new("go", "ride out now", "the band is small"),
        new("stay", "wait for a better time", "the band is strong")
    ];

    public JevPickAskTests() => TestMap.EnsureInternal();

    [Fact]
    public void TryAsk_NoProviderKeepsTheRule() =>
        WithAsker(null, null, () =>
        {
            Assert.False(JevPick.IsAvailable);
            Assert.False(JevPick.TryAsk(Red(), null, BrainEventKind.Decide, Decision(), Options, _ => { }));
        });

    [Fact]
    public void TryAsk_FewerThanTwoOptionsIsNotAsked()
    {
        var calls = new List<JevCall>();

        WithAsker(call => { calls.Add(call); return true; }, null, () =>
            Assert.False(JevPick.TryAsk(Red(), null, BrainEventKind.Decide, Decision(), [Options[0]], _ => { })));

        Assert.Empty(calls);
    }

    [Fact]
    public void TryAsk_ARefusedCallKeepsTheRule() =>
        WithAsker(_ => false, null, () =>
            Assert.False(JevPick.TryAsk(Red(), null, BrainEventKind.Decide, Decision(), Options, _ => { })));

    [Fact]
    public void TryAsk_IsBookedAsABigMomentAndReadAgainstTheAnsweringProvidersFloor()
    {
        var calls = new List<JevCall>();
        var verdicts = new List<JevPickVerdict>();

        WithAsker(call => { calls.Add(call); return true; }, _ => RaisedFloor, () =>
        {
            Assert.True(JevPick.TryAsk(Red(), null, BrainEventKind.Died, Decision(), Options, verdicts.Add));

            var call = Assert.Single(calls);
            Assert.Equal(JevKind.BigMoment, call.Use);
            Assert.Equal(BrainEventKind.Died, call.Kind);

            call.Answer(new Dictionary<string, SystemOneAnswer> { [JevPick.Question] = new("go", Sure, null) }, Provider);
        });

        Assert.Null(Assert.Single(verdicts).Choice);
    }

    private static void WithAsker(Func<JevCall, bool> asker, Func<string, double> floorOf, Action test)
    {
        var savedAsker = JevPick.Asker;
        var savedFloor = JevPick.FloorOf;

        try
        {
            JevPick.Asker = asker;
            JevPick.FloorOf = floorOf;
            test();
        }
        finally
        {
            JevPick.Asker = savedAsker;
            JevPick.FloorOf = savedFloor;
        }
    }

    // One character for the class: made on first use, after the constructor readied the internal map.
    private static readonly Lazy<SosariaCharacter> Vex = new(() => new SosariaCharacter((Serial)0x7D91) { Name = "Vex" });

    private static SosariaCharacter Red() => Vex.Value;

    private static JevDecision Decision() => JevPick.Build(new Dictionary<string, object>(), "Ride now?", Options);
}
