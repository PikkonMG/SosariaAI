using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class PartyInviteRulesTests
{
    private const int SelfPower = 100;
    private const int ClosePower = 120;
    private const int FarPower = 200;

    [Fact]
    public void PowerFits_WithinSlack()
    {
        Assert.True(PartyInviteRules.PowerFits(SelfPower, ClosePower));
        Assert.False(PartyInviteRules.PowerFits(SelfPower, FarPower));
        Assert.False(PartyInviteRules.PowerFits(SelfPower, 0));
    }

    [Fact]
    public void CountsAsAlly_NeedsSameMapAliveAndClose()
    {
        var range = PartyInviteRules.InviteRange;
        Assert.True(PartyInviteRules.CountsAsAlly(sameMap: true, alive: true, distance: range, range));
        Assert.False(PartyInviteRules.CountsAsAlly(sameMap: true, alive: true, distance: range + 1, range));
        Assert.False(PartyInviteRules.CountsAsAlly(sameMap: false, alive: true, distance: 0, range));
        Assert.False(PartyInviteRules.CountsAsAlly(sameMap: true, alive: false, distance: 0, range));
        Assert.False(PartyInviteRules.CountsAsAlly(sameMap: true, alive: true, distance: -1, range));
    }

    [Fact]
    public void Accepts_HuntOrFriend()
    {
        Assert.True(PartyInviteRules.Accepts(true, true, true, 0, false, false));
        Assert.True(PartyInviteRules.Accepts(false, true, true, PartyInviteRules.FriendScore, false, false));
        Assert.False(PartyInviteRules.Accepts(true, false, true, 0, false, false));
        Assert.False(PartyInviteRules.Accepts(true, true, false, 0, false, false));
        Assert.False(PartyInviteRules.Accepts(true, true, true, 0, true, false));
        Assert.False(PartyInviteRules.Accepts(true, true, true, 0, false, true));
        Assert.False(PartyInviteRules.Accepts(true, true, true, -PartyInviteRules.FriendScore, false, false));
    }

    [Fact]
    public void Lines_AreFixed()
    {
        Assert.Equal("I am with you.", PartyInviteRules.AcceptLine());
        Assert.Equal("Good hunting. I am off.", PartyInviteRules.GoodbyeLine());
    }
}
