using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class SceneTests
{
    private const int Roll = 12345;
    private static readonly DateTime Now = new(2026, 9, 23, 20, 0, 0, DateTimeKind.Utc);

    private static void AssertFilled(List<SceneBeat> beats, int castSize)
    {
        Assert.NotEmpty(beats);

        foreach (var beat in beats)
        {
            Assert.InRange(beat.Actor, SceneScripts.Lead, castSize - 1);
            Assert.False(string.IsNullOrWhiteSpace(beat.Text));
            Assert.DoesNotContain("{", beat.Text);
            Assert.True(beat.Delay >= TimeSpan.Zero);
        }
    }

    [Fact]
    public void MayStart_Heard_NeedsRoomAndARestedSpot()
    {
        Assert.True(SceneRules.MayStart(true, true, 0, default, default, Now));
        Assert.False(SceneRules.MayStart(true, true, SceneRules.MaxActive, default, default, Now));
        Assert.False(SceneRules.MayStart(true, true, 0, Now, default, Now));
        Assert.True(SceneRules.MayStart(true, true, 0, Now - SceneRules.PlaceRest, default, Now));
        Assert.True(SceneRules.MayStart(true, false, 0, Now, default, Now));
    }

    [Fact]
    public void MayStart_Unheard_OneInAWhileShardWide()
    {
        Assert.True(SceneRules.MayStart(false, true, 0, default, default, Now));
        Assert.False(SceneRules.MayStart(false, false, 0, default, Now, Now));
        Assert.True(SceneRules.MayStart(false, false, 0, default, Now - SceneRules.UnheardGap, Now));
    }

    [Fact]
    public void Gap_StaysInsideTheTypingWindow()
    {
        foreach (var roll in new[] { 0, 1, -1, int.MaxValue, int.MinValue + 1 })
        {
            Assert.InRange(
                SceneRules.Gap(roll).TotalMilliseconds,
                SceneRules.MinGapMs,
                SceneRules.MinGapMs + SceneRules.GapSpreadMs
            );
        }
    }

    [Fact]
    public void Night_WrapsMidnight()
    {
        Assert.True(SceneRules.IsNight(SceneRules.NightStartsHour));
        Assert.True(SceneRules.IsNight(0));
        Assert.False(SceneRules.IsNight(SceneRules.NightEndsHour));
        Assert.False(SceneRules.IsNight(12));
    }

    [Fact]
    public void FriendChain_EveryoneSpeaks_LeadFirstAtOnce()
    {
        var beats = SceneScripts.FriendChain("Kel", "Lora", "Mira", Roll);
        AssertFilled(beats, 3);

        Assert.Equal(SceneScripts.Lead, beats[0].Actor);
        Assert.Equal(TimeSpan.Zero, beats[0].Delay);
        Assert.Contains("Lora", beats[0].Text);
        Assert.Contains(beats, beat => beat.Actor == SceneScripts.First);
        Assert.Contains(beats, beat => beat.Actor == SceneScripts.Second);
    }

    [Fact]
    public void ShoutAnswer_ABystanderComments()
    {
        var beats = SceneScripts.ShoutAnswer(true, "Kel", "gm katana", "5k", 2, Roll);
        AssertFilled(beats, 3);
        Assert.Equal(SceneScripts.First, beats[0].Actor);
        Assert.True(beats[0].Delay > TimeSpan.Zero);

        AssertFilled(SceneScripts.ShoutAnswer(false, "Kel", "bandages", null, 1, Roll), 2);
    }

    [Fact]
    public void GoingAsk_NamesThePlace()
    {
        var beats = SceneScripts.GoingAsk("despise", 1, Roll);
        AssertFilled(beats, 2);
        Assert.Contains("despise", beats[0].Text);
    }

    [Fact]
    public void DeathAndLooted_BystandersOnly()
    {
        var death = SceneScripts.DeathJoke("Kel", 2, Roll);
        AssertFilled(death, 3);
        Assert.DoesNotContain(death, beat => beat.Actor == SceneScripts.Lead);

        var looted = SceneScripts.LootedReply(Roll);
        AssertFilled(looted, 2);
    }

    [Fact]
    public void Duel_OnlookersWaitForTheWalk()
    {
        var start = SceneScripts.DuelStart("Kel", "Lora", 2, Roll);
        AssertFilled(start, 3);
        Assert.Equal(TimeSpan.FromMilliseconds(SceneRules.DuelWatchMs), start[0].Delay);

        AssertFilled(SceneScripts.DuelEnd("Kel", "Lora", Roll), 2);
    }

    [Fact]
    public void RedAlert_EveryoneAnswers_NobodyPromisesToCome()
    {
        var beats = SceneScripts.RedAlert("britain", [false, true], Roll);
        AssertFilled(beats, 3);
        Assert.Equal(2, beats.Count);
        Assert.DoesNotContain(beats, beat => PromiseLines.IsPromise(beat.Text));
    }

    [Fact]
    public void CraftCustomer_PriceOrSoldOut()
    {
        var priced = SceneScripts.CraftCustomer("Kel", "arms", "150", Roll);
        AssertFilled(priced, 2);
        Assert.Contains(priced, beat => beat.Actor == SceneScripts.Lead);

        AssertFilled(SceneScripts.CraftCustomer("Kel", "arms", null, Roll), 2);
    }

    [Fact]
    public void Party_DepartInPartyChat_ReturnAloud()
    {
        var depart = SceneScripts.PartyDepart("Kel", "despise", 2, Roll);
        AssertFilled(depart, 3);
        Assert.All(depart, beat => Assert.Equal(SceneChannel.Party, beat.Channel));

        var back = SceneScripts.PartyReturn("despise", 1, Roll);
        AssertFilled(back, 2);
        Assert.All(back, beat => Assert.Equal(SceneChannel.Say, beat.Channel));
    }

    [Fact]
    public void TavernNight_TellsAStoryWhenThereIsOne()
    {
        const string story = "heard someone got pked at despise";
        const string reply = "wow";
        var beats = SceneScripts.TavernNight("britain", story, reply, 2, Roll);
        AssertFilled(beats, 3);
        Assert.Contains(beats, beat => beat.Actor == SceneScripts.Second && beat.Text == story);
        Assert.Equal(reply, beats[^1].Text);

        AssertFilled(SceneScripts.TavernNight(null, null, null, 1, Roll), 2);
    }
}
