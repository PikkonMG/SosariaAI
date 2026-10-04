using System;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using Xunit;

namespace SosariaAI.Tests;

public class InviteAskRulesTests
{
    [Fact]
    public void IsYes_MatchesSpokenAssent()
    {
        Assert.True(InviteAskRules.IsYes("yes"));
        Assert.True(InviteAskRules.IsYes("Sure, let's go"));
        Assert.True(InviteAskRules.IsYes("ok"));
        Assert.True(InviteAskRules.IsYes("aye"));
        Assert.True(InviteAskRules.IsYes("y"));
        Assert.True(InviteAskRules.IsYes("yea"));
        Assert.True(InviteAskRules.IsYes("ya"));
        Assert.True(InviteAskRules.IsYes("alright then"));
        Assert.True(InviteAskRules.IsYes("fine"));
        Assert.True(InviteAskRules.IsYes("Of course!"));
        Assert.False(InviteAskRules.IsYes("hello"));
        Assert.False(InviteAskRules.IsYes("of"));
        Assert.False(InviteAskRules.IsYes("course"));
        Assert.True(InviteAskRules.IsNo("no thanks"));
        Assert.True(InviteAskRules.IsNo("nope"));
    }

    [Fact]
    public void IsYes_NoWordsWinOverYesWords()
    {
        Assert.False(InviteAskRules.IsYes("no thanks"));
        Assert.False(InviteAskRules.IsYes("yes, no, I am busy"));
        Assert.True(InviteAskRules.IsNo("yes, no, I am busy"));
    }

    [Fact]
    public void GoNotLater_AreNeitherYesNorNo()
    {
        Assert.False(InviteAskRules.IsYes("go"));
        Assert.False(InviteAskRules.IsNo("not now"));
        Assert.False(InviteAskRules.IsNo("later"));
        Assert.False(InviteAskRules.IsYes("later"));
    }

    [Fact]
    public void MayInviteFighter_NeedsWeaponAndPower()
    {
        Assert.False(InviteAskRules.MayInviteFighter(false, 80, 40));
        Assert.False(InviteAskRules.MayInviteFighter(true, 30, 40));
        Assert.True(InviteAskRules.MayInviteFighter(true, 40, 40));
    }

    [Fact]
    public void LogLines_NameBothSides()
    {
        Assert.Equal("Bernadette asked Wystan to party", InviteAskRules.LogAsk("Bernadette", "Wystan"));
        Assert.Equal("Wystan answered yes", InviteAskRules.LogAnswer("Wystan", true));
        Assert.Equal("Bernadette invited Wystan to party", InviteAskRules.LogInvite("Bernadette", "Wystan"));
        Assert.Equal("Wystan gave Bernadette no answer", InviteAskRules.LogNoAnswer("Bernadette", "Wystan"));
    }

    [Fact]
    public void AnswerTimeout_UsesNamedDefault()
    {
        var now = new DateTime(2026, 4, 1, 12, 0, 0);
        Assert.True(
            InviteAskRules.AnswerTimedOut(now, now.AddSeconds(CareerSettings.DefaultInviteAnswerSeconds), CareerSettings.DefaultInviteAnswerSeconds)
        );
        Assert.False(InviteAskRules.AnswerTimedOut(now, now.AddSeconds(10), CareerSettings.DefaultInviteAnswerSeconds));
    }

    [Fact]
    public void PlayerIdle_NeedsNoFightNoWarModeAndStandingStill()
    {
        Assert.True(InviteAskRules.PlayerIdle(fighting: false, warmode: false, InviteAskRules.PlayerIdleFor));
        Assert.False(InviteAskRules.PlayerIdle(fighting: true, warmode: false, InviteAskRules.PlayerIdleFor));
        Assert.False(InviteAskRules.PlayerIdle(fighting: false, warmode: true, InviteAskRules.PlayerIdleFor));
        Assert.False(InviteAskRules.PlayerIdle(fighting: false, warmode: false, InviteAskRules.PlayerIdleFor - TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void PlayerAskRested_OneAskPerGapFromAnyone()
    {
        var now = new DateTime(2026, 4, 1, 12, 0, 0);
        Assert.True(InviteAskRules.PlayerAskRested(default, now));
        Assert.False(InviteAskRules.PlayerAskRested(now, now + InviteAskRules.PlayerAskGap - TimeSpan.FromSeconds(1)));
        Assert.True(InviteAskRules.PlayerAskRested(now, now + InviteAskRules.PlayerAskGap));
    }

    [Fact]
    public void MayAskPlayer_RareAndOnlyWhenItFits()
    {
        var fits = InviteAskRules.AskPlayerPercent - 1;
        Assert.True(InviteAskRules.MayAskPlayer(playerIdle: true, powerFits: true, declinedBefore: false, rested: true, fits));
        Assert.False(InviteAskRules.MayAskPlayer(playerIdle: true, powerFits: true, declinedBefore: false, rested: true, InviteAskRules.AskPlayerPercent));
        Assert.False(InviteAskRules.MayAskPlayer(playerIdle: false, powerFits: true, declinedBefore: false, rested: true, fits));
        Assert.False(InviteAskRules.MayAskPlayer(playerIdle: true, powerFits: false, declinedBefore: false, rested: true, fits));
        Assert.False(InviteAskRules.MayAskPlayer(playerIdle: true, powerFits: true, declinedBefore: true, rested: true, fits));
        Assert.False(InviteAskRules.MayAskPlayer(playerIdle: true, powerFits: true, declinedBefore: false, rested: false, fits));
    }

    [Fact]
    public void RemembersDecline_ReadsTheBondReason()
    {
        var met = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var declined = new Bond("bot:Felucca:mira", "player:0x00007B01", BondRules.NeutralScore, met, "Britain", met, 0, InviteAskRules.DeclineReason);

        Assert.True(InviteAskRules.RemembersDecline(declined));
        Assert.False(InviteAskRules.RemembersDecline(declined with { LastReason = BondRules.HealedReason }));
        Assert.False(InviteAskRules.RemembersDecline(null));
    }
}
