using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SosariaAI.Behaviour;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class RedMomentJevTests
{
    private const double Sure = 0.9;
    private const double Floor = 0.5;
    private const string Provider = "jev";
    private const int CloseBody = 5;
    private const int MidBody = 12;
    private const int FarBody = 25;
    private const int ShortTrip = 40;
    private const int LongTrip = 200;
    private const int CrossingTrip = 900;
    private const int Several = 3;
    private const int One = 1;
    private const int ManyReds = 8;
    private const int FullBand = 4;

    private static readonly JevPickVerdict NoPick = new(null, 0, JevPick.NoAnswerSource);

    // ----- A red just raised near its body -----

    [Fact]
    public void AsksWayBack_OnlyARedsCloseWalkOnceWithTheRecallOpen()
    {
        Assert.True(RedMomentJev.AsksWayBack(true, CorpseRunStep.CloseWalk, false, () => true));
        Assert.False(RedMomentJev.AsksWayBack(false, CorpseRunStep.CloseWalk, false, () => true));
        Assert.False(RedMomentJev.AsksWayBack(true, CorpseRunStep.RecallHome, false, () => true));
        Assert.False(RedMomentJev.AsksWayBack(true, CorpseRunStep.Reclaim, false, () => true));
        Assert.False(RedMomentJev.AsksWayBack(true, CorpseRunStep.CloseWalk, true, () => true));
        Assert.False(RedMomentJev.AsksWayBack(true, CorpseRunStep.CloseWalk, false, () => false));
    }

    [Fact]
    public void AsksWayBack_ReadsTheRunebookOnlyWhenAllElseAllows()
    {
        var looks = 0;

        RedMomentJev.AsksWayBack(true, CorpseRunStep.CloseWalk, true, () => ++looks > 0);
        RedMomentJev.AsksWayBack(false, CorpseRunStep.CloseWalk, false, () => ++looks > 0);

        Assert.Equal(0, looks);
    }

    [Fact]
    public void WayBackState_IsWordsNotNumbers()
    {
        var state = RedMomentJev.WayBackState(
            new WayBackFacts(false, FarBody, true, true, Several, One, 0)
        );

        Assert.DoesNotContain(JsonSerializer.Serialize(state), char.IsDigit);
        Assert.Equal("a red just raised, in a death robe, unarmed", state["you"]);
        Assert.Equal("a long walk away, in a dungeon", state["body"]);
        Assert.Equal("your killer, several blue fighters, one monster", state["at_body"]);
        Assert.Equal("no gang mate near", state["gang"]);
    }

    [Fact]
    public void BuildWayBack_AsksWalkOrRecallInOneChoice()
    {
        var decision = RedMomentJev.BuildWayBack(new WayBackFacts(true, CloseBody, false, false, 0, 0, Several));

        var question = Assert.Single(decision.Questions).Value;
        Assert.Equal(SystemOneApi.ChoiceType, question.Type);
        var criteria = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(question.Criteria);
        Assert.Equal([RedMomentJev.RecallHomeKey, RedMomentJev.WalkBackKey], criteria.Keys.Order().ToArray());
    }

    [Fact]
    public void WayBackStep_JevsRecallElseTheRulesWalk()
    {
        Assert.Equal(CorpseRunStep.RecallHome, RedMomentJev.WayBackStep(Picked(RedMomentJev.RecallHomeKey)));
        Assert.Equal(CorpseRunStep.CloseWalk, RedMomentJev.WayBackStep(Picked(RedMomentJev.WalkBackKey)));
        Assert.Equal(CorpseRunStep.CloseWalk, RedMomentJev.WayBackStep(NoPick));
        Assert.Equal(CorpseRunStep.CloseWalk, RedMomentJev.WayBackStep(JevPick.TooSlow));
    }

    [Fact]
    public void WayBack_JevsAnswerMapsBackThroughTheReader()
    {
        var answers = new Dictionary<string, SystemOneAnswer>
        {
            [JevPick.Question] = new(RedMomentJev.RecallHomeKey, Sure, null)
        };

        var verdict = JevPick.Read(answers, RedMomentJev.WayBackOptions, Floor, Provider);

        Assert.Equal(CorpseRunStep.RecallHome, RedMomentJev.WayBackStep(verdict));
    }

    // ----- A blue band about to ride into the Den -----

    [Fact]
    public void DenRaidState_IsWordsNotNumbers()
    {
        var state = RedMomentJev.DenRaidState(new DenRaidFacts(FullBand, One, ManyReds, DenRaidOutcome.LostFighters));

        Assert.DoesNotContain(JsonSerializer.Serialize(state), char.IsDigit);
        Assert.Equal("you and several friends", state["band"]);
        Assert.Equal("one short of supplies", state["supplies"]);
        Assert.Equal("many reds seen at the Den", state["den"]);
        Assert.Equal("the last raid lost fighters", state["last_raid"]);
    }

    [Fact]
    public void RaidGoes_UnlessJevSaysHold()
    {
        Assert.False(RedMomentJev.RaidGoes(Picked(RedMomentJev.HoldKey)));
        Assert.True(RedMomentJev.RaidGoes(Picked(RedMomentJev.RaidKey)));
        Assert.True(RedMomentJev.RaidGoes(NoPick));
        Assert.True(RedMomentJev.RaidGoes(JevPick.TooSlow));
    }

    [Fact]
    public void BuildDenRaid_AsksRaidOrHold()
    {
        var question = Assert.Single(RedMomentJev.BuildDenRaid(new DenRaidFacts(FullBand, 0, 0, DenRaidOutcome.NoneYet)).Questions).Value;
        var criteria = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(question.Criteria);

        Assert.Equal([RedMomentJev.HoldKey, RedMomentJev.RaidKey], criteria.Keys.Order().ToArray());
    }

    // ----- A red ghost with more than one way back -----

    [Fact]
    public void GhostWayOptions_OnlyTheWaysTheRulesAllow_InTheRuleOrder()
    {
        var keys = RedMomentJev.GhostWayOptions(AllWays()).Select(option => option.Key).ToArray();
        var ankhOnly = RedMomentJev.GhostWayOptions(AllWays() with { CallOffered = false, WaitOffered = false });

        Assert.Equal([RedMomentJev.CallKey, RedMomentJev.AnkhKey, RedMomentJev.WaitKey], keys);
        Assert.Equal(RedMomentJev.AnkhKey, Assert.Single(ankhOnly).Key);
    }

    [Fact]
    public void MayAskGhostWay_OnceAndNeverWantedUnderGuards()
    {
        Assert.True(RedMomentJev.MayAskGhostWay(true, false, false));
        Assert.False(RedMomentJev.MayAskGhostWay(false, false, false));
        Assert.False(RedMomentJev.MayAskGhostWay(true, true, false));
        Assert.False(RedMomentJev.MayAskGhostWay(true, false, true));
    }

    [Fact]
    public void GhostWay_JevsWayWhenOffered()
    {
        Assert.Equal(RedGhostWay.WalkToAnkh, RedMomentJev.GhostWay(Picked(RedMomentJev.AnkhKey), AllWays()));
        Assert.Equal(RedGhostWay.WaitForMate, RedMomentJev.GhostWay(Picked(RedMomentJev.WaitKey), AllWays()));
        Assert.Equal(RedGhostWay.CallHelper, RedMomentJev.GhostWay(Picked(RedMomentJev.CallKey), AllWays()));
    }

    [Fact]
    public void GhostWay_ElseTheRuleOrder_CallThenTheAnkh()
    {
        var noCall = AllWays() with { CallOffered = false };

        Assert.Equal(RedGhostWay.CallHelper, RedMomentJev.GhostWay(NoPick, AllWays()));
        Assert.Equal(RedGhostWay.WalkToAnkh, RedMomentJev.GhostWay(JevPick.TooSlow, noCall));
        Assert.Equal(RedGhostWay.WalkToAnkh, RedMomentJev.GhostWay(Picked(RedMomentJev.CallKey), noCall));
        Assert.Equal(RedGhostWay.CallHelper, RedMomentJev.GhostWay(Picked(RedMomentJev.WaitKey), AllWays() with { WaitOffered = false }));
    }

    [Fact]
    public void GhostWayState_NamesOnlyTheOfferedWays_InWords()
    {
        var state = RedMomentJev.GhostWayState(AllWays());
        var bare = RedMomentJev.GhostWayState(AllWays() with { CallOffered = false, WaitOffered = false });

        Assert.DoesNotContain(JsonSerializer.Serialize(state), char.IsDigit);
        Assert.Equal("a red ghost, your killer stands near", state["you"]);
        Assert.Equal("a fellow red, free, a short walk", state["helper"]);
        Assert.Equal("across the land, passes a guarded town, one blue fighter there", state["ankh"]);
        Assert.Equal("several gang mates near, busy", state["gang"]);
        Assert.False(bare.ContainsKey("helper"));
        Assert.False(bare.ContainsKey("gang"));
    }

    // ----- Words -----

    [Theory]
    [InlineData(CloseBody, "a few steps away")]
    [InlineData(MidBody, "a short walk away")]
    [InlineData(FarBody, "a long walk away")]
    public void BodyWord_Buckets(int tiles, string word) => Assert.Equal(word, RedMomentJev.BodyWord(tiles));

    [Theory]
    [InlineData(ShortTrip, "a short walk")]
    [InlineData(LongTrip, "a long walk")]
    [InlineData(CrossingTrip, "across the land")]
    public void TripWord_Buckets(int tiles, string word) => Assert.Equal(word, RedMomentJev.TripWord(tiles));

    [Theory]
    [InlineData(0, "no red")]
    [InlineData(RedMomentJev.FewReds, "a few reds")]
    [InlineData(ManyReds, "many reds")]
    public void RedsWord_Buckets(int reds, string word) => Assert.Equal(word, RedMomentJev.RedsWord(reds));

    [Fact]
    public void AtBodyWords_NobodyWhenClear() => Assert.Equal("nobody", RedMomentJev.AtBodyWords(false, 0, 0));

    [Fact]
    public void LastRaidWords_NoRaidYet() => Assert.Equal("no raid yet", RedMomentJev.LastRaidWords(DenRaidOutcome.NoneYet));

    [Fact]
    public void MateWait_EndsBeforeTheStuckGhostStandsUp() =>
        Assert.True(RedMomentJev.MateWait < GhostRules.NoWayFallbackAfter);

    private static GhostWayFacts AllWays() =>
        new(
            CallOffered: true,
            CallIsGangMate: false,
            CallTiles: MidBody,
            AnkhOffered: true,
            AnkhTripTiles: CrossingTrip,
            AnkhPassesGuards: true,
            BluesAtAnkh: One,
            WaitOffered: true,
            BusyMatesNear: Several,
            KillerNear: true
        );

    private static JevPickVerdict Picked(string key) => new(key, Sure, Provider);
}
