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
/// A settled haggle with a person opens a real SecureTrade window: the character's side shows its
/// goods or its coin, what the person drops on the character lands on the person's side, and the
/// swap runs only once both boxes check.
/// </summary>
[Collection(TradeWindowTests.CollectionName)]
public class TradeWindowTests
{
    public const string CollectionName = "Trade window";

    private const int Asking = 400;
    private const int BackpackSerialOffset = 0x100;
    private const int ArtTileCount = 0x4000;

    static TradeWindowTests() => Timer.Init(0);

    public TradeWindowTests()
    {
        ServerFeatureFlags.PlayerTrading = true;
        TestMap.EnsureInternal();
        TestMap.EnsureRunningWorld();
        EnsureItemBounds();
    }

    [Fact]
    public void Seller_WindowOpensWithTheGoodsOnItsSide()
    {
        var hawker = Character((Serial)0x7F01);
        var buyer = Person((Serial)0x7F02);
        var halberd = InPack(hawker, new Halberd((Serial)0x7F90) { Amount = 1 });
        var session = Agreed(TradeSession.Selling(hawker, buyer, halberd, Asking));

        var trade = TradeBetween(buyer);

        Assert.NotNull(trade);
        Assert.True(trade.Valid);
        Assert.True(halberd.IsChildOf(trade.To.Container));
        Assert.False(session.Ended);
        session.End(null);
    }

    [Fact]
    public void Seller_SwapRunsWhenBothBoxesCheck()
    {
        var hawker = Character((Serial)0x7F03);
        var buyer = Person((Serial)0x7F04);
        var halberd = InPack(hawker, new Halberd((Serial)0x7F91) { Amount = 1 });
        var session = Agreed(TradeSession.Selling(hawker, buyer, halberd, Asking));
        var gold = new Gold((Serial)0x7F92) { Amount = session.Agreed };

        Assert.True(hawker.OnDragDrop(buyer, gold));

        var trade = TradeBetween(buyer);
        Assert.True(gold.IsChildOf(trade.From.Container));

        PressAccept(buyer);
        session.Tick(Core.Now);

        Assert.False(trade.Valid);
        Assert.True(halberd.IsChildOf(buyer.Backpack));
        Assert.True(gold.IsChildOf(hawker.Backpack));
        Assert.True(session.Ended);
    }

    [Fact]
    public void Seller_WrongGoldKeepsTheWindowOpen()
    {
        var hawker = Character((Serial)0x7F05);
        var buyer = Person((Serial)0x7F06);
        var halberd = InPack(hawker, new Halberd((Serial)0x7F93) { Amount = 1 });
        var session = Agreed(TradeSession.Selling(hawker, buyer, halberd, Asking));
        var gold = new Gold((Serial)0x7F94) { Amount = session.Agreed - 1 };

        Assert.True(hawker.OnDragDrop(buyer, gold));

        var trade = PressAccept(buyer);
        session.Tick(Core.Now);

        Assert.True(trade.Valid);
        Assert.False(trade.To.Accepted);
        Assert.False(session.Ended);
        Assert.True(halberd.IsChildOf(trade.To.Container));
        session.End(null);
    }

    [Fact]
    public void Seller_ACheckOnTheLedgerDoesNotPay()
    {
        var hawker = Character((Serial)0x7F07);
        var buyer = Person((Serial)0x7F08);
        var halberd = InPack(hawker, new Halberd((Serial)0x7F95) { Amount = 1 });
        var session = Agreed(TradeSession.Selling(hawker, buyer, halberd, Asking));
        var trade = TradeBetween(buyer);

        // A currency-field offer settles only between two accounts; the character has none.
        trade.From.Gold = session.Agreed;
        trade.From.Accepted = true;
        trade.Update();
        session.Tick(Core.Now);

        Assert.True(trade.Valid);
        Assert.False(trade.To.Accepted);
        Assert.False(session.Ended);
        Assert.True(halberd.IsChildOf(trade.To.Container));
        session.End(null);
    }

    [Fact]
    public void Seller_AFoldedWindowHandsEverythingBack()
    {
        var hawker = Character((Serial)0x7F09);
        var buyer = Person((Serial)0x7F0A);
        var halberd = InPack(hawker, new Halberd((Serial)0x7F96) { Amount = 1 });
        var session = Agreed(TradeSession.Selling(hawker, buyer, halberd, Asking));
        var gold = new Gold((Serial)0x7F97) { Amount = session.Agreed };

        Assert.True(hawker.OnDragDrop(buyer, gold));

        var trade = TradeBetween(buyer);
        trade.Cancel();
        session.Tick(Core.Now);

        Assert.False(trade.Valid);
        Assert.True(halberd.IsChildOf(hawker.Backpack));
        Assert.True(gold.IsChildOf(buyer.Backpack));
        Assert.False(session.Ended);
        session.End(null);
    }

    [Fact]
    public void Seller_AFreshDropOpensTheWindowAgain()
    {
        var hawker = Character((Serial)0x7F0B);
        var buyer = Person((Serial)0x7F0C);
        var halberd = InPack(hawker, new Halberd((Serial)0x7F98) { Amount = 1 });
        var session = Agreed(TradeSession.Selling(hawker, buyer, halberd, Asking));

        TradeBetween(buyer).Cancel();
        session.Tick(Core.Now);

        var gold = new Gold((Serial)0x7F99) { Amount = session.Agreed };
        Assert.True(hawker.OnDragDrop(buyer, gold));

        var trade = TradeBetween(buyer);
        Assert.True(trade.Valid);
        Assert.True(gold.IsChildOf(trade.From.Container));
        session.End(null);
    }

    [Fact]
    public void Buyer_WindowShowsTheCoinOnItsSide()
    {
        var buyer = Character((Serial)0x7F0D);
        var seller = Person((Serial)0x7F0E);
        InPack(buyer, new Gold((Serial)0x7F9A) { Amount = Asking * 10 });
        var claim = new GoodsClaim(Appraisal.RowByKey("polearm"), 1, true, Appraisal.NoMagic);
        var session = Agreed(TradeSession.Buying(buyer, seller, claim, 0), walkedOver: true);

        var trade = TradeBetween(seller);

        Assert.NotNull(trade);
        Assert.Equal(session.Agreed, trade.To.Container.Items.OfType<Gold>().Sum(pile => pile.Amount));
        Assert.Equal(Asking * 10 - session.Agreed, buyer.Backpack.GetAmount(typeof(Gold)));
        session.End(null);
    }

    [Fact]
    public void Buyer_SwapRunsWhenBothBoxesCheck()
    {
        var buyer = Character((Serial)0x7F0F);
        var seller = Person((Serial)0x7F10);
        InPack(buyer, new Gold((Serial)0x7F9B) { Amount = Asking * 10 });
        var claim = new GoodsClaim(Appraisal.RowByKey("polearm"), 1, true, Appraisal.NoMagic);
        var session = Agreed(TradeSession.Buying(buyer, seller, claim, 0), walkedOver: true);
        var halberd = new Halberd((Serial)0x7F9C) { Quality = WeaponQuality.Exceptional, Amount = 1 };

        Assert.True(buyer.OnDragDrop(seller, halberd));

        var trade = TradeBetween(seller);
        Assert.True(halberd.IsChildOf(trade.From.Container));

        PressAccept(seller);
        session.Tick(Core.Now);

        Assert.False(trade.Valid);
        Assert.True(halberd.IsChildOf(buyer.Backpack));
        Assert.Equal(session.Agreed, seller.Backpack.GetAmount(typeof(Gold)));
        Assert.True(session.Ended);
    }

    [Fact]
    public void Buyer_WrongGoodsKeepsTheWindowOpen()
    {
        var buyer = Character((Serial)0x7F11);
        var seller = Person((Serial)0x7F12);
        InPack(buyer, new Gold((Serial)0x7F9D) { Amount = Asking * 10 });
        var claim = new GoodsClaim(Appraisal.RowByKey("polearm"), 1, true, Appraisal.NoMagic);
        var session = Agreed(TradeSession.Buying(buyer, seller, claim, 0), walkedOver: true);
        var plain = new Halberd((Serial)0x7F9E) { Amount = 1 };

        Assert.True(buyer.OnDragDrop(seller, plain));

        var trade = PressAccept(seller);
        session.Tick(Core.Now);

        Assert.True(trade.Valid);
        Assert.False(trade.To.Accepted);
        Assert.False(session.Ended);
        session.End(null);
    }

    [Fact]
    public void EndingTheDealFoldsTheWindow()
    {
        var hawker = Character((Serial)0x7F13);
        var buyer = Person((Serial)0x7F14);
        var halberd = InPack(hawker, new Halberd((Serial)0x7F9F) { Amount = 1 });
        var session = Agreed(TradeSession.Selling(hawker, buyer, halberd, Asking));
        var trade = TradeBetween(buyer);

        session.End(null);

        Assert.False(trade.Valid);
        Assert.True(halberd.IsChildOf(hawker.Backpack));
    }

    // Opens the session and shakes on the character's own number, as a person saying "deal" does.
    private static TradeSession Agreed(TradeSession session, bool walkedOver = false)
    {
        TradeSessions.Open(session);

        if (walkedOver)
        {
            session.Tick(Core.Now);
        }

        session.Hear(new TradeIntent(TradeIntentKind.Accept, session.Standing, null, true));
        Assert.Equal(TradePhase.HandOff, session.Phase);
        return session;
    }

    // The live window the session built for this person, or null. A folded one lingers on the
    // mobile until its deferred delete runs, so only a still-valid trade counts.
    private static SecureTrade TradeBetween(Mobile person) =>
        person.Items.OfType<SecureTradeContainer>().Select(cont => cont.Trade)
            .FirstOrDefault(trade => trade?.Valid == true);

    // The person's accept click, minus the packet: the engine handler sets the flag then updates.
    private static SecureTrade PressAccept(Mobile person)
    {
        var trade = TradeBetween(person);
        Assert.NotNull(trade);
        trade.From.Accepted = true;
        trade.Update();
        return trade;
    }

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
