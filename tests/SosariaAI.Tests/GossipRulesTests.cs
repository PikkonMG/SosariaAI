using System;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class GossipRulesTests
{
    [Fact]
    public void Constants_MatchDesign()
    {
        Assert.Equal(TimeSpan.FromHours(3), GossipRules.MaxAge);
        Assert.Equal(TimeSpan.FromSeconds(60), GossipRules.MinNewsAge);
        Assert.Equal(TimeSpan.FromMinutes(45), GossipRules.VividAge);
        Assert.Equal(TimeSpan.FromMinutes(15), GossipRules.RetellAfter);
        Assert.Equal(2.0, GossipRules.NewsSecondsPerTile);
        Assert.Equal(40, GossipRules.HereRadius);
        Assert.Equal(400, GossipRules.VividRadius);
        Assert.Equal(1200, GossipRules.VagueRadius);
        Assert.Equal(3, GossipRules.RepeatKillerAt);
        Assert.Equal(300, GossipRules.JournalCapacity);
    }

    [Fact]
    public void TooLocal_InsideHereRadius_IsTrueAtAnyAge()
    {
        Assert.True(GossipRules.TooLocal(0));
        Assert.True(GossipRules.TooLocal(GossipRules.HereRadius));
    }

    [Fact]
    public void TooLocal_OutsideHereRadius_IsFalse()
    {
        Assert.False(GossipRules.TooLocal(GossipRules.HereRadius + 1));
    }

    [Fact]
    public void Retold_FadesWithDetail()
    {
        Assert.True(GossipRules.Retold(GossipRules.VividDetail, 99));
        Assert.True(GossipRules.Retold(GossipRules.VagueDetail, GossipRules.VagueRetellPercent - 1));
        Assert.False(GossipRules.Retold(GossipRules.VagueDetail, GossipRules.VagueRetellPercent));
        Assert.False(GossipRules.Retold(GossipRules.FadedDetail, 0));
    }

    [Fact]
    public void MayTell_RedSightingOnlyOnce()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0);
        Assert.True(GossipRules.MayTell(ShardEventType.Red, 0, default, now));
        Assert.False(GossipRules.MayTell(ShardEventType.Red, 1, now - TimeSpan.FromHours(1), now));
        Assert.True(GossipRules.MayTell(ShardEventType.Pk, 3, now - GossipRules.RetellAfter, now));
    }

    [Fact]
    public void MayTell_WaitsBeforeTheSameStoryAgain()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0);
        Assert.False(GossipRules.MayTell(ShardEventType.Death, 1, now - TimeSpan.FromMinutes(1), now));
    }

    [Fact]
    public void KeepsQuiet_ThiefAndRedDoNotTellOnThemselves()
    {
        Assert.True(GossipRules.KeepsQuiet(ShardEventType.Theft, tellerIsActor: true));
        Assert.True(GossipRules.KeepsQuiet(ShardEventType.Red, tellerIsActor: true));
        Assert.False(GossipRules.KeepsQuiet(ShardEventType.Theft, tellerIsActor: false));
        Assert.False(GossipRules.KeepsQuiet(ShardEventType.Pk, tellerIsActor: true));
    }

    [Fact]
    public void IsRepeatKiller_FromThreeKills()
    {
        Assert.False(GossipRules.IsRepeatKiller(GossipRules.RepeatKillerAt - 1));
        Assert.True(GossipRules.IsRepeatKiller(GossipRules.RepeatKillerAt));
    }

    [Fact]
    public void WhenWord_NeverAClockTime()
    {
        Assert.Equal("just now", GossipRules.WhenWord(TimeSpan.FromMinutes(2)));
        Assert.Equal("a bit ago", GossipRules.WhenWord(TimeSpan.FromMinutes(20)));
        Assert.Equal("like an hour ago", GossipRules.WhenWord(TimeSpan.FromMinutes(60)));
        Assert.Equal("earlier", GossipRules.WhenWord(TimeSpan.FromHours(2)));
    }

    [Fact]
    public void NewsHasArrived_TravelIsTwoSecondsPerTile()
    {
        var almost = TimeSpan.FromSeconds(199);
        var arrived = TimeSpan.FromSeconds(200);
        Assert.False(GossipRules.NewsHasArrived(100, almost, ownEvent: false));
        Assert.True(GossipRules.NewsHasArrived(100, arrived, ownEvent: false));
    }

    [Fact]
    public void NewsHasArrived_MinAgeEvenAtZeroDistance()
    {
        Assert.False(GossipRules.NewsHasArrived(0, TimeSpan.FromSeconds(59), ownEvent: false));
        Assert.True(GossipRules.NewsHasArrived(0, TimeSpan.FromSeconds(60), ownEvent: false));
    }

    [Fact]
    public void NewsHasArrived_OwnEventNeedsNonNegativeAge()
    {
        Assert.False(GossipRules.NewsHasArrived(0, TimeSpan.FromSeconds(-1), ownEvent: true));
        Assert.True(GossipRules.NewsHasArrived(0, TimeSpan.Zero, ownEvent: true));
    }

    [Fact]
    public void DetailLevel_OwnOrVivid_IsTwo()
    {
        Assert.Equal(2, GossipRules.DetailLevel(2000, TimeSpan.FromHours(2), ownEvent: true));
        Assert.Equal(2, GossipRules.DetailLevel(400, TimeSpan.FromMinutes(44), ownEvent: false));
        Assert.Equal(2, GossipRules.DetailLevel(10, TimeSpan.FromMinutes(10), ownEvent: false));
    }

    [Fact]
    public void DetailLevel_Vague_IsOne()
    {
        Assert.Equal(1, GossipRules.DetailLevel(401, TimeSpan.FromMinutes(44), ownEvent: false));
        Assert.Equal(1, GossipRules.DetailLevel(1200, TimeSpan.FromMinutes(50), ownEvent: false));
        Assert.Equal(1, GossipRules.DetailLevel(1500, TimeSpan.FromMinutes(10), ownEvent: false));
    }

    [Fact]
    public void DetailLevel_Faded_IsZero()
    {
        Assert.Equal(0, GossipRules.DetailLevel(1201, TimeSpan.FromMinutes(45), ownEvent: false));
        Assert.Equal(0, GossipRules.DetailLevel(1500, TimeSpan.FromMinutes(50), ownEvent: false));
    }

    [Fact]
    public void Weight_PkBeatsDeath()
    {
        var pk = GossipRules.Weight(ShardEventType.Pk, 0, ownStory: false);
        var death = GossipRules.Weight(ShardEventType.Death, 0, ownStory: false);
        Assert.True(pk > death);
        Assert.Equal(4.0, pk);
        Assert.Equal(3.0, death);
    }

    [Fact]
    public void Weight_TellCountFades()
    {
        var fresh = GossipRules.Weight(ShardEventType.Pk, 0, ownStory: false);
        var told = GossipRules.Weight(ShardEventType.Pk, 1, ownStory: false);
        var thrice = GossipRules.Weight(ShardEventType.Pk, 3, ownStory: false);
        Assert.Equal(4.0, fresh);
        Assert.Equal(2.0, told);
        Assert.Equal(1.0, thrice);
        Assert.True(fresh > told);
        Assert.True(told > thrice);
    }

    [Fact]
    public void Weight_OwnStoryBoosts()
    {
        var other = GossipRules.Weight(ShardEventType.Pk, 0, ownStory: false);
        var own = GossipRules.Weight(ShardEventType.Pk, 0, ownStory: true);
        Assert.Equal(other * 2.5, own);
    }

    [Fact]
    public void Weight_TypeBases()
    {
        Assert.Equal(1.5, GossipRules.Weight(ShardEventType.Party, 0, ownStory: false));
        Assert.Equal(2.0, GossipRules.Weight(ShardEventType.Theft, 0, ownStory: false));
        Assert.Equal(0.8, GossipRules.Weight(ShardEventType.Red, 0, ownStory: false));
        Assert.Equal(1.0, GossipRules.Weight("unknown", 0, ownStory: false));
    }

    [Fact]
    public void MayTell_AStoryWearsOut()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0);

        Assert.True(GossipRules.MayTell(ShardEventType.Death, GossipRules.MaxTells - 1, default, now));
        Assert.False(GossipRules.MayTell(ShardEventType.Death, GossipRules.MaxTells, default, now));
    }

    [Fact]
    public void Weight_DuelIsGoodTalk() =>
        Assert.True(GossipRules.Weight(ShardEventType.Duel, 0, false) > GossipRules.Weight(ShardEventType.Red, 0, false));
}
