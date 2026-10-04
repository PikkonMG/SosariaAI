using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// When the trade window cannot open — a person who refuses trades, or trading switched off —
/// goods and gold still change hands by a drop on the character, and nothing dropped outside a
/// deal is kept.
/// </summary>
[Collection(TradeHandOffTests.CollectionName)]
public class TradeHandOffTests
{
    public const string CollectionName = "Trade hand-off";

    private const int Asking = 400;
    private const int BackpackSerialOffset = 0x100;
    private const int ArtTileCount = 0x4000;

    static TradeHandOffTests() => Timer.Init(0);

    public TradeHandOffTests()
    {
        TestMap.EnsureInternal();
        TestMap.EnsureRunningWorld();
        EnsureItemBounds();
    }

    [Fact]
    public void Seller_ExactGoldBuysTheGoods()
    {
        var hawker = Character((Serial)0x7E01);
        var buyer = Person((Serial)0x7E02);
        var halberd = InPack(hawker, new Halberd((Serial)0x7E90) { Amount = 1 });
        var session = Agreed(TradeSession.Selling(hawker, buyer, halberd, Asking));
        var gold = new Gold((Serial)0x7E91) { Amount = session.Agreed };

        Assert.True(hawker.OnDragDrop(buyer, gold));
        Assert.True(halberd.IsChildOf(buyer.Backpack));
        Assert.True(gold.IsChildOf(hawker.Backpack));
        Assert.True(session.Ended);
    }

    [Fact]
    public void Seller_WrongGoldIsHandedBack()
    {
        var hawker = Character((Serial)0x7E03);
        var buyer = Person((Serial)0x7E04);
        var halberd = InPack(hawker, new Halberd((Serial)0x7E92) { Amount = 1 });
        var session = Agreed(TradeSession.Selling(hawker, buyer, halberd, Asking));
        var gold = new Gold((Serial)0x7E93) { Amount = session.Agreed - 1 };

        Assert.False(hawker.OnDragDrop(buyer, gold));
        Assert.True(halberd.IsChildOf(hawker.Backpack));
        Assert.False(session.Ended);
        session.End(null);
    }

    [Fact]
    public void Buyer_PaysForTheDescribedGoods()
    {
        var buyer = Character((Serial)0x7E05);
        var seller = Person((Serial)0x7E06);
        InPack(buyer, new Gold((Serial)0x7E94) { Amount = Asking * 10 });
        var claim = new GoodsClaim(Appraisal.RowByKey("polearm"), 1, true, Appraisal.NoMagic);
        var session = Agreed(TradeSession.Buying(buyer, seller, claim, 0), walkedOver: true);
        var halberd = new Halberd((Serial)0x7E95) { Quality = WeaponQuality.Exceptional, Amount = 1 };

        Assert.True(buyer.OnDragDrop(seller, halberd));
        Assert.True(halberd.IsChildOf(buyer.Backpack));
        Assert.Equal(session.Agreed, seller.Backpack.GetAmount(typeof(Gold)));
    }

    [Fact]
    public void Buyer_RefusesGoodsThatAreNotWhatWasSaid()
    {
        var buyer = Character((Serial)0x7E07);
        var seller = Person((Serial)0x7E08);
        InPack(buyer, new Gold((Serial)0x7E96) { Amount = Asking * 10 });
        var claim = new GoodsClaim(Appraisal.RowByKey("polearm"), 1, true, Appraisal.NoMagic);
        var session = Agreed(TradeSession.Buying(buyer, seller, claim, 0), walkedOver: true);
        var plain = new Halberd((Serial)0x7E97) { Amount = 1 };

        Assert.False(buyer.OnDragDrop(seller, plain));
        Assert.Equal(0, seller.Backpack.GetAmount(typeof(Gold)));
        session.End(null);
    }

    [Fact]
    public void Drop_OutsideADealIsHandedBack()
    {
        var character = Character((Serial)0x7E09);
        var person = Person((Serial)0x7E0A);
        var gift = new Gold((Serial)0x7E98) { Amount = Asking };

        Assert.False(character.OnDragDrop(person, gift));
        Assert.False(gift.IsChildOf(character.Backpack));
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

    private static SosariaCharacter Character(Serial serial)
    {
        var character = new SosariaCharacter(serial);
        character.DefaultMobileInit();
        Wear(character, new Backpack((Serial)(serial.Value + BackpackSerialOffset)) { Layer = Layer.Backpack });
        return character;
    }

    // Refusing trades keeps the window away, so the bare hand-off is what a drop runs here.
    private static PlayerMobile Person(Serial serial)
    {
        var person = new PlayerMobile(serial) { RefuseTrades = true };
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
