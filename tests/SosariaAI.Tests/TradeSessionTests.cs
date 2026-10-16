using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// An order's deposit through a real trade window counts as paid only when the swap ran: the
/// character checks its box after the person, so the coin crosses inside that check, and gold
/// the person moves out of their own pack later proves nothing.
/// </summary>
[Collection(TradeWindowTests.CollectionName)]
public class TradeSessionTests
{
    private const int Deposit = 400;
    private const int Purse = Deposit * 2;
    private const int BackpackSerialOffset = 0x100;
    private const int ArtTileCount = 0x4000;
    private const string Noun = "GM katana";

    static TradeSessionTests() => Timer.Init(0);

    public TradeSessionTests()
    {
        ServerFeatureFlags.PlayerTrading = true;
        TestMap.EnsureInternal();
        TestMap.EnsureRunningWorld();
        EnsureItemBounds();
    }

    [Fact]
    public void Deposit_CoinLeftOutOfTheirPack_ThenAFoldedWindow_IsNotPaid()
    {
        var smith = Character((Serial)0x7801);
        var buyer = Person((Serial)0x7802);
        var purse = InPack(buyer, new Gold((Serial)0x7810) { Amount = Purse });
        var placed = false;
        var session = Deposited(smith, buyer, paid => placed = paid);

        Assert.True(smith.OnDragDrop(buyer, new Gold((Serial)0x7811) { Amount = Deposit }));
        session.Tick(Core.Now);

        var trade = TradeBetween(buyer);
        Assert.False(trade.To.Accepted);

        // The buyer empties their pack, then folds the window: the deposit comes back to them.
        purse.Delete();
        trade.Cancel();
        session.Tick(Core.Now);

        Assert.False(session.Ended);
        session.End(null);
        Assert.False(placed);
        Assert.Equal(0, smith.Backpack.GetAmount(typeof(Gold)));
        Assert.Equal(Deposit, buyer.Backpack.GetAmount(typeof(Gold)));
    }

    [Fact]
    public void Deposit_PaidWhenTheSwapRunsInTheCharactersCheck()
    {
        var smith = Character((Serial)0x7803);
        var buyer = Person((Serial)0x7804);
        var placed = false;
        var session = Deposited(smith, buyer, paid => placed = paid);

        Assert.True(smith.OnDragDrop(buyer, new Gold((Serial)0x7812) { Amount = Deposit }));

        var trade = TradeBetween(buyer);
        trade.From.Accepted = true;
        trade.Update();
        session.Tick(Core.Now);

        Assert.False(trade.Valid);
        Assert.True(session.Ended);
        Assert.True(placed);
        Assert.Equal(Deposit, smith.Backpack.GetAmount(typeof(Gold)));
    }

    private static TradeSession Deposited(SosariaCharacter smith, Mobile buyer, Action<bool> settled)
    {
        var session = TradeSession.Fixed(smith, buyer, null, Deposit, Noun, TradeLineKind.SellerAccept, settled);
        TradeSessions.Open(session);
        Assert.NotNull(TradeBetween(buyer));
        return session;
    }

    // The live window the session built for this person, or null.
    private static SecureTrade TradeBetween(Mobile person) =>
        person.Items.OfType<SecureTradeContainer>().Select(cont => cont.Trade)
            .FirstOrDefault(trade => trade?.Valid == true);

    private static SosariaCharacter Character(Serial serial)
    {
        var character = new SosariaCharacter(serial);
        character.DefaultMobileInit();
        Wear(character, new Backpack((Serial)(serial.Value + BackpackSerialOffset)) { Layer = Layer.Backpack });
        return character;
    }

    private static PlayerMobile Person(Serial serial)
    {
        var person = new PlayerMobile(serial);
        person.DefaultMobileInit();
        Wear(person, new Backpack((Serial)(serial.Value + BackpackSerialOffset)) { Layer = Layer.Backpack });
        return person;
    }

    private static T InPack<T>(Mobile owner, T item) where T : Item
    {
        owner.Backpack.AddItem(item);
        return item;
    }

    // A container places a dropped item inside the art bounds, which the test run never loads.
    private static void EnsureItemBounds()
    {
        if (ItemBounds.Bounds == null)
        {
            typeof(ItemBounds).GetProperty(nameof(ItemBounds.Bounds))!.SetValue(null, new Rectangle2D[ArtTileCount]);
        }
    }

    private static void Wear(Mobile mobile, Item item)
    {
        item.Parent = mobile;
        mobile.Items.Add(item);
    }
}
