using System;
using System.IO;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class TalkPromiseTests
{
    private const int RollsTried = 64;
    private const string Place = "despise";
    private const string Listener = "Mira";
    private const string HonestSmallTalk = "roads r quiet today";
    private const string HonestGoingAsk = "is {place} camped?";

    [Fact]
    public void PromiseFreeDefaults_NeverPromise()
    {
        foreach (var topic in TalkDefaults.All)
        {
            if (!TalkDefaults.PromiseFree.Contains(topic.Name))
            {
                continue;
            }

            foreach (var line in TalkFile.Parse(topic.Lines).Lines)
            {
                Assert.False(PromiseLines.IsPromise(line.Text), $"{topic.Name}: \"{line.Text}\"");
            }
        }
    }

    [Fact]
    public void EveryDefaultLine_IsKept()
    {
        foreach (var topic in TalkDefaults.All)
        {
            foreach (var line in TalkFile.Parse(topic.Lines).Lines)
            {
                Assert.True(TalkDefaults.Keeps(topic.Name, line.Text), $"{topic.Name}: \"{line.Text}\"");
            }
        }
    }

    [Fact]
    public void PromiseFree_HoldsTheUnbackedTalk_NotTheTalkWhoseSpeakerActs()
    {
        Assert.Contains(TalkCategory.GoingAsk, TalkDefaults.PromiseFree);
        Assert.Contains(TalkCategory.SmallTalk, TalkDefaults.PromiseFree);
        Assert.Contains(TalkCategory.RecallAdventure, TalkDefaults.PromiseFree);
        Assert.Contains(TalkCategory.GreetOldFriend, TalkDefaults.PromiseFree);
        Assert.Contains(TalkCategory.GuardStandDown, TalkDefaults.PromiseFree);
        Assert.Contains(TalkCategory.HuntHaul, TalkDefaults.PromiseFree);

        Assert.DoesNotContain(TalkCategory.RedOnMyWay, TalkDefaults.PromiseFree);
        Assert.DoesNotContain(TalkCategory.CraftBankStock, TalkDefaults.PromiseFree);
        Assert.DoesNotContain(TalkCategory.TreasureDecoded, TalkDefaults.PromiseFree);
    }

    // Each of these shipped as a default once; a shard that booted then still has it in its file.
    [Theory]
    [InlineData(TalkCategory.GoingAsk, "anyone going to {place}?")]
    [InlineData(TalkCategory.GoingAsk, "Anyone headed to {place}? ")]
    [InlineData(TalkCategory.GoingAsk, "heading to {place} soon, anyone?")]
    [InlineData(TalkCategory.RecallAdventure, "thinking bout {place} again, u in?")]
    [InlineData(TalkCategory.RecallAdventure, "we should hit {place} again {friend}")]
    [InlineData(TalkCategory.GreetOldFriend, "yo {friend}! {place} again sometime?")]
    [InlineData(TalkCategory.SmallTalk, "{name} u still on for later?")]
    [InlineData(TalkCategory.GuardStandDown, "meet me outside {name}")]
    [InlineData(TalkCategory.GuardStandDown, "take it outside {name}")]
    [InlineData(TalkCategory.HuntHaul, "{price} gold today, heading to the bank")]
    [InlineData(TalkCategory.SmallTalk, "anyone wanna hunt?")]
    public void UnbackedPromise_IsNotKept(string category, string line) =>
        Assert.False(TalkDefaults.Keeps(category, line));

    [Theory]
    [InlineData(TalkCategory.CraftBankStock, "heading to the bank for {item}")]
    [InlineData(TalkCategory.TreasureDecoded, "map read, off to dig")]
    [InlineData(TalkCategory.SmallTalk, HonestSmallTalk)]
    [InlineData(TalkCategory.GoingAsk, HonestGoingAsk)]
    public void BackedOrHonestLine_IsKept(string category, string line) =>
        Assert.True(TalkDefaults.Keeps(category, line));

    [Fact]
    public void LoadOrCreate_DropsOldPromiseRows_FromAFileAlreadyThere()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sosaria-talk-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllLines(
                Path.Combine(dir, TalkCategory.SmallTalk + TalkFile.Extension),
                ["{name} u still on for later?", "anyone wanna hunt?", HonestSmallTalk]
            );
            File.WriteAllLines(
                Path.Combine(dir, TalkCategory.GoingAsk + TalkFile.Extension),
                ["anyone going to {place}?", "heading to {place} soon, anyone?", HonestGoingAsk]
            );

            var library = TalkLibrary.LoadOrCreate(dir);

            for (var roll = 0; roll < RollsTried; roll++)
            {
                Assert.Equal(
                    HonestSmallTalk,
                    library.Pick(TalkCategory.SmallTalk, roll, new TalkSlots { Name = Listener }, EraBand.T2A)
                );
                Assert.Equal(
                    "is despise camped?",
                    library.Pick(TalkCategory.GoingAsk, roll, new TalkSlots { Place = Place }, EraBand.T2A)
                );
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void GoingAsk_AsksForNews_NeverPromises()
    {
        for (var roll = 0; roll < RollsTried; roll++)
        {
            var ask = SceneScripts.GoingAsk(Place, 1, roll)[SceneScripts.Lead].Text;

            Assert.Contains(Place, ask);
            Assert.False(PromiseLines.IsPromise(ask), ask);
        }
    }
}
