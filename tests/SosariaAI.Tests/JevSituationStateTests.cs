using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class JevSituationStateTests
{
    [Fact]
    public void Build_SendsWordsNotNumbers()
    {
        var state = JevSituationState.Build(
            new JevSituation
            {
                Identity = "You are a green mage, getting by.",
                Drives = new PersonaDrives(0.2, 0.9, 0.5, isCustom: true),
                Want = "learn magery",
                Mood = "eager",
                DayPart = "Evening",
                Place = "Moonglow",
                DistanceFromHome = 4000,
                Hits = 30,
                HitsMax = 100,
                Mana = 5,
                ManaMax = 100,
                Gold = 12,
                PackFill = 0.8,
                SinceHunt = TimeSpan.FromMinutes(5),
                SinceRest = TimeSpan.FromHours(3),
                RecentRuns = 2
            }
        );

        var json = JsonSerializer.Serialize(state);

        Assert.False(json.Any(char.IsDigit), json);
        Assert.Equal("content, careful", state["temper"]);
        Assert.Equal("learn magery (eager)", state["want"]);
        Assert.Equal("evening", state["time"]);
        Assert.Equal("Moonglow, outside town, far from home", state["place"]);

        Assert.Equal("badly wounded, mana empty", state["body"]);
        Assert.Equal("purse broke, pack heavy, nothing to sell", state["goods"]);
        Assert.Equal("hunt just now, rest hours ago, bank never", state["last"]);

        Assert.Equal("fled danger lately", state["danger"]);
        Assert.Equal("alone", state["party"]);
    }

    [Fact]
    public void Build_LeavesOutAnEmptyPlanAndNoMoment()
    {
        var state = JevSituationState.Build(new JevSituation());

        Assert.False(state.ContainsKey("plan"));
        Assert.False(state.ContainsKey("moment"));
        Assert.False(state.ContainsKey("recent"));
        Assert.Equal("idle", state["doing"]);
        Assert.Equal("none", state["danger"]);
    }

    [Fact]
    public void Build_NamesTheMoment()
    {
        var state = JevSituationState.Build(new JevSituation { Moment = "low on gold" });

        Assert.Equal("low on gold", state["moment"]);
    }

    [Fact]
    public void Build_ARedHearsItsKitItsGangAndTheBlues_InWords()
    {
        const int SeveralMates = 3;
        var state = JevSituationState.Build(
            new JevSituation { Red = true, Armed = false, GangAtDen = SeveralMates, BlueBand = RoadGroupKind.Sweep }
        );

        Assert.DoesNotContain(JsonSerializer.Serialize(state), char.IsDigit);
        Assert.Equal("unarmed, several gang mates at the Den, blues sweep near your camp", state["red"]);
    }

    [Fact]
    public void Build_ABlueHasNoRedWords() =>
        Assert.False(JevSituationState.Build(new JevSituation { GangAtDen = 1, BlueBand = RoadGroupKind.DenRaid }).ContainsKey("red"));

    [Fact]
    public void BlueBandWords_NamesTheRaidOnTheDen()
    {
        Assert.Equal("blues raid the Den", JevSituationState.BlueBandWords(RoadGroupKind.DenRaid));
        Assert.Equal("no blue band out", JevSituationState.BlueBandWords(null));
    }

    [Theory]
    [InlineData(100, 100, "unhurt")]
    [InlineData(80, 100, "scratched")]
    [InlineData(50, 100, "wounded")]
    [InlineData(20, 100, "badly wounded")]
    [InlineData(5, 100, "near death")]
    [InlineData(0, 0, "unhurt")]
    public void HitsWord_Buckets(int hits, int max, string word) => Assert.Equal(word, JevSituationState.HitsWord(hits, max));

    [Theory]
    [InlineData(0.1, "light")]
    [InlineData(0.5, "half full")]
    [InlineData(0.8, "heavy")]
    [InlineData(1.2, "overloaded")]
    public void PackWord_Buckets(double fill, string word) => Assert.Equal(word, JevSituationState.PackWord(fill));

    [Fact]
    public void SinceWord_NeverForAnUnsetTime() =>
        Assert.Equal("never", JevSituationState.SinceWord(TimeSpan.MaxValue));

    [Fact]
    public void TemperWords_MiddlingIsEvenTempered() =>
        Assert.Equal("even-tempered", JevSituationState.TemperWords(PersonaDrives.Neutral));

    [Fact]
    public void PartyWords_FormingBeatsMembers()
    {
        Assert.Equal("a party is forming", JevSituationState.PartyWords("Hope", forming: true));
        Assert.Equal("in a party with Hope", JevSituationState.PartyWords("Hope", forming: false));
        Assert.Equal("alone", JevSituationState.PartyWords("none", forming: false));
    }
}
