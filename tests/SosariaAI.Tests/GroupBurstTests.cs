using Server;
using Server.Mobiles;
using EngineParty = Server.Engines.PartySystem.Party;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>A side's burst call: party mates and gang mates time their casts to it, others do not see it.</summary>
public class GroupBurstTests
{
    private const long LeadMs = 60000;
    private const long LaterMs = 1000;
    private const int Gang = 7301;
    private const int OtherGang = 7302;

    static GroupBurstTests() => Timer.Init(0);

    public GroupBurstTests() => TestMap.EnsureInternal();

    [Fact]
    public void Join_APartyMateTimesItsCastToTheEarliestCall_AnOutsiderSeesNone()
    {
        var leader = new PlayerMobile((Serial)0x3C10) { Name = "Dorian" };
        var mage = new SosariaCharacter((Serial)0x3C11) { Name = "Bran" };
        var mate = new SosariaCharacter((Serial)0x3C12) { Name = "Sela" };
        var loner = new SosariaCharacter((Serial)0x3C13) { Name = "Tam" };
        var target = new PlayerMobile((Serial)0x3C14) { Name = "Grim" };
        leader.Party = new EngineParty(leader);
        Assert.True(GameParty.InviteCharacter(leader, mage));
        Assert.True(GameParty.InviteCharacter(leader, mate));
        var landAt = Core.TickCount + LeadMs;

        Assert.False(GroupBurst.TryCall(mate, target, out _));

        GroupBurst.Join(mage, target, landAt);
        Assert.True(GroupBurst.TryCall(mate, target, out var called));
        Assert.Equal(landAt, called);

        GroupBurst.Join(mate, target, landAt + LaterMs);
        Assert.True(GroupBurst.TryCall(mage, target, out called));
        Assert.Equal(landAt, called);

        GroupBurst.Join(loner, target, landAt);
        Assert.False(GroupBurst.TryCall(loner, target, out _));
    }

    [Fact]
    public void Join_AGangMateSeesTheCall_AnotherGangDoesNot()
    {
        var red = new SosariaCharacter((Serial)0x3C20) { Name = "Vex", OutlawGang = Gang };
        var mate = new SosariaCharacter((Serial)0x3C21) { Name = "Kerr", OutlawGang = Gang };
        var rival = new SosariaCharacter((Serial)0x3C22) { Name = "Rook", OutlawGang = OtherGang };
        var victim = new PlayerMobile((Serial)0x3C23) { Name = "Rowan" };
        var landAt = Core.TickCount + LeadMs;

        GroupBurst.Join(red, victim, landAt);

        Assert.True(GroupBurst.TryCall(mate, victim, out var called));
        Assert.Equal(landAt, called);
        Assert.False(GroupBurst.TryCall(rival, victim, out _));
    }
}
