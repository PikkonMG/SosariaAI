using Server;
using Server.Mobiles;
using EngineParty = Server.Engines.PartySystem.Party;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// Who may order whom in a real party. The characters stand off the map, so an order is
/// taken and kept but no skill starts.
/// </summary>
public class PartyOrdersTests
{
    static PartyOrdersTests() => Timer.Init(0);

    public PartyOrdersTests() => TestMap.EnsureInternal();

    [Fact]
    public void Hear_TheLeadersLineGivesTheOrder()
    {
        var player = Player(0x7C01, "Dorian");
        var bot = Bot(0x7C02, "Bran");
        LeadWith(player, bot);

        Assert.True(PartyOrders.Hear(bot, player, "follow me"));

        var order = PartyOrders.Current(bot);
        Assert.NotNull(order);
        Assert.Equal(PartyOrderKind.Follow, order.Kind);
        Assert.Equal(player.Serial, order.From);
    }

    [Fact]
    public void Hear_AMembersLineGivesNoOrder()
    {
        var leader = Player(0x7C03, "Dorian");
        var bot = Bot(0x7C04, "Bran");
        var member = Player(0x7C05, "Tam");
        LeadWith(leader, bot);
        Join(leader, member);

        Assert.False(PartyOrders.Hear(bot, member, "follow me"));
        Assert.Null(PartyOrders.Current(bot));
    }

    [Fact]
    public void Hear_AnOutsiderGivesNothing()
    {
        var leader = Player(0x7C06, "Dorian");
        var bot = Bot(0x7C07, "Bran");
        var outsider = Player(0x7C08, "Grim");
        LeadWith(leader, bot);

        Assert.False(PartyOrders.Hear(bot, outsider, "follow me"));
        Assert.False(PartyOrders.Hear(bot, outsider, "heal me"));
        Assert.Null(PartyOrders.Current(bot));
    }

    [Fact]
    public void Hear_AHelpCallFromAPersonReachesTheBotLeader()
    {
        var leader = Bot(0x7C09, "Bran");
        var player = Player(0x7C0A, "Dorian");
        LeadWith(leader, Bot(0x7C0B, "Sela"));
        Join(leader, player);

        Assert.True(PartyOrders.Hear(leader, player, "wait for me"));
        Assert.True(PartyOrders.LeaderWaits(leader));
    }

    [Fact]
    public void Hear_ABotsLineIsNeverAnOrderOrACall()
    {
        var leader = Bot(0x7C0C, "Bran");
        var mate = Bot(0x7C0D, "Sela");
        LeadWith(leader, mate);

        Assert.False(PartyOrders.Hear(mate, leader, "follow me"));
        Assert.False(PartyOrders.Hear(leader, mate, "wait for me"));
        Assert.Null(PartyOrders.Current(mate));
        Assert.False(PartyOrders.LeaderWaits(leader));
    }

    [Fact]
    public void Hear_AnAttackWithNothingInSightIsAnsweredNotTaken()
    {
        var player = Player(0x7C0E, "Dorian");
        var bot = Bot(0x7C0F, "Bran");
        LeadWith(player, bot);

        Assert.True(PartyOrders.Hear(bot, player, "attack"));
        Assert.Null(PartyOrders.Current(bot));
    }

    [Fact]
    public void Hear_AnUnknownPlaceIsAnsweredNotTaken()
    {
        var player = Player(0x7C10, "Dorian");
        var bot = Bot(0x7C11, "Bran");
        LeadWith(player, bot);

        Assert.True(PartyOrders.Hear(bot, player, "go to nowhere at all"));
        Assert.Null(PartyOrders.Current(bot));
    }

    [Fact]
    public void Give_TheSameOrderAgainIsNotNew()
    {
        var player = Player(0x7C12, "Dorian");
        var bot = Bot(0x7C13, "Bran");
        LeadWith(player, bot);
        var now = Core.Now;

        Assert.True(PartyOrders.Give(bot, new PartyOrder(PartyOrderKind.Follow, player.Serial, now)));
        Assert.False(PartyOrders.Give(bot, new PartyOrder(PartyOrderKind.Follow, player.Serial, now)));
        Assert.True(PartyOrders.Give(bot, new PartyOrder(PartyOrderKind.Hold, player.Serial, now)));
        Assert.Equal(PartyOrderKind.Hold, PartyOrders.Current(bot).Kind);
    }

    [Fact]
    public void Settle_Arrived_CannotReach_Drop_MoveTheOrderAlong()
    {
        var player = Player(0x7C14, "Dorian");
        var bot = Bot(0x7C15, "Bran");
        LeadWith(player, bot);
        var place = new Point3D(1400, 1600, 20);

        PartyOrders.Give(bot, new PartyOrder(PartyOrderKind.GoTo, player.Serial, Core.Now, Place: place, PlaceName: "despise"));
        PartyOrders.Arrived(bot);
        var holding = PartyOrders.Current(bot);
        Assert.Equal(PartyOrderKind.Hold, holding.Kind);
        Assert.Equal(place, holding.Place);

        PartyOrders.Settle(bot);
        Assert.Equal(PartyOrderKind.Follow, PartyOrders.Current(bot).Kind);

        PartyOrders.Give(bot, new PartyOrder(PartyOrderKind.GoTo, player.Serial, Core.Now, Place: place, PlaceName: "despise"));
        PartyOrders.CannotReach(bot);
        Assert.Equal(PartyOrderKind.Follow, PartyOrders.Current(bot).Kind);

        PartyOrders.Drop(bot);
        Assert.Null(PartyOrders.Current(bot));
    }

    private static PlayerMobile Player(uint serial, string name) => new((Serial)serial) { Name = name };

    private static SosariaCharacter Bot(uint serial, string name) => new((Serial)serial) { Name = name };

    /// <summary>A party led by <paramref name="leader"/> with one bot in it.</summary>
    private static void LeadWith(Mobile leader, SosariaCharacter bot)
    {
        if (GameParty.Of(leader) == null)
        {
            leader.Party = new EngineParty(leader);
        }

        Assert.True(GameParty.InviteCharacter(leader, bot));
    }

    /// <summary>A person joins the leader's party, as an accepted invite does.</summary>
    private static void Join(Mobile leader, Mobile person)
    {
        var party = GameParty.Of(leader);
        party.Candidates.Add(person);
        Assert.True(GameParty.TryAccept(person, leader));
    }
}
