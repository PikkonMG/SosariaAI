using System;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class GossipLinesTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0);

    private static ShardEvent Event(string type, string actor, string other, string place = "Despise, Felucca") =>
        new() { At = Now - TimeSpan.FromMinutes(20), Type = type, Actor = actor, Other = other, Place = place };

    [Theory]
    [InlineData("Despise, Felucca", "despise")]
    [InlineData("Britain", "britain")]
    [InlineData(null, "the wild")]
    public void PlaceWord_NamesTheSpotNotTheFacet(string place, string expected)
    {
        Assert.Equal(expected, GossipLines.PlaceWord(place));
    }

    [Fact]
    public void Tell_RepeatKiller_IsNamedWithTheCount()
    {
        for (var seed = 0; seed < 4; seed++)
        {
            var line = GossipLines.Tell(Event(ShardEventType.Pk, "Aldreth", "Grim"), "Sela", GossipRules.VividDetail, 4, Now, seed);
            Assert.Contains("Grim", line);
        }

        Assert.Contains("4", GossipLines.Tell(Event(ShardEventType.Pk, "Aldreth", "Grim"), "Sela", GossipRules.VividDetail, 4, Now, 0));
    }

    [Fact]
    public void Tell_FadedNews_IsNotTold()
    {
        Assert.Null(GossipLines.Tell(Event(ShardEventType.Pk, "Aldreth", "Grim"), "Sela", GossipRules.FadedDetail, 0, Now, 0));
    }

    [Fact]
    public void Tell_VagueNews_DropsTheTime()
    {
        for (var seed = 0; seed < 4; seed++)
        {
            var line = GossipLines.Tell(Event(ShardEventType.Death, "Aldreth", "a lich"), "Sela", GossipRules.VagueDetail, 0, Now, seed);
            Assert.DoesNotContain("ago", line);
        }
    }

    [Fact]
    public void Tell_ThiefAndRedHaveNoLineAboutThemselves()
    {
        Assert.Null(GossipLines.Tell(Event(ShardEventType.Theft, "Slick", "Mira"), "Slick", GossipRules.VividDetail, 0, Now, 0));
        Assert.Null(GossipLines.Tell(Event(ShardEventType.Red, "Grim", "Mira"), "Grim", GossipRules.VividDetail, 0, Now, 0));
    }

    [Fact]
    public void Tell_PartyMember_TellsItFirstPerson()
    {
        var run = Event(ShardEventType.Party, "Bran", "Sela" + ShardEvent.NameSeparator + "Tam");
        var line = GossipLines.Tell(run, "Tam", GossipRules.VividDetail, 0, Now, 0);

        Assert.Equal("went to despise with Bran a bit ago, fun", line);
    }

    [Fact]
    public void Tell_UnknownKind_IsNotTold()
    {
        Assert.Null(GossipLines.Tell(Event("rumor", "Bran", null), "Sela", GossipRules.VividDetail, 0, Now, 0));
        Assert.Null(GossipLines.Reply(Event("rumor", "Bran", null), 0));
    }

    [Fact]
    public void Fact_FirstPersonForTheVictim()
    {
        Assert.Equal(
            "You were murdered by Grim at despise a bit ago.",
            GossipLines.Fact(Event(ShardEventType.Pk, "Aldreth", "Grim"), "Aldreth", Now)
        );
        Assert.Equal(
            "Aldreth died at despise a bit ago, killed by a monster.",
            GossipLines.Fact(Event(ShardEventType.Death, "Aldreth", null), "Sela", Now)
        );
    }

    [Fact]
    public void Involves_FindsEveryNamedPerson()
    {
        var run = Event(ShardEventType.Party, "Bran", "Sela" + ShardEvent.NameSeparator + "Tam");

        Assert.True(run.Involves("bran"));
        Assert.True(run.Involves("tam"));
        Assert.False(run.Involves("Ta"));
        Assert.False(run.Involves(null));
        Assert.True(run.IsActor("Bran"));
        Assert.False(run.IsActor("Sela"));
    }

    [Fact]
    public void Tell_Duel_WinnerAndLoserTellItTheirWay()
    {
        var duel = Event(ShardEventType.Duel, "Aldous", "Edmund", "Britain");

        Assert.Contains("Edmund", GossipLines.Tell(duel, "Aldous", GossipRules.VividDetail, 0, Now, 0));
        Assert.Contains("Aldous", GossipLines.Tell(duel, "Edmund", GossipRules.VividDetail, 0, Now, 0));
        Assert.Contains("britain", GossipLines.Tell(duel, "Sela", GossipRules.VividDetail, 0, Now, 0));
        Assert.NotNull(GossipLines.Reply(duel, 0));
        Assert.Contains("friendly duel", GossipLines.Fact(duel, "Sela", Now));
    }
}
