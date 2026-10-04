using Server;
using Server.Items;
using SosariaAI.Economy;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>What a seller puts on a counter, and what a counter takes at all.</summary>
public class SaleGoodsTests
{
    private const int Reach = 100;
    private const string Carpenter = "Britain carpenter";
    private const string Smith = "Britain smith";

    private static readonly Point3D Britain = new(1434, 1690, 0);
    private static readonly Point3D Far = new(Britain.X + Reach + 1, Britain.Y, 0);

    static SaleGoodsTests() => Timer.Init(0);

    public SaleGoodsTests() => TestMap.EnsureInternal();

    [Fact]
    public void SoldAs_OreSellsAsTheIngotsAForgeMakes()
    {
        Assert.Equal(typeof(IronIngot), SaleGoods.SoldAs(new IronOre((Serial)0x7F21)));
        Assert.Equal(typeof(Log), SaleGoods.SoldAs(new Log((Serial)0x7F22)));
        Assert.Null(SaleGoods.SoldAs(null));
    }

    [Fact]
    public void TypesOf_EachTypeOnce()
    {
        // A serial-built item starts fixed in place, as before load; loose goods move.
        var goods = new Item[]
        {
            new Log((Serial)0x7F31) { Movable = true },
            new Log((Serial)0x7F32) { Movable = true },
            new IronOre((Serial)0x7F33) { Movable = true },
            new Amber((Serial)0x7F34)
        };

        Assert.Equal(new[] { typeof(Log), typeof(IronIngot) }, SaleGoods.TypesOf(goods));
    }

    [Fact]
    public void CounterTakes_NotBlessedFixedOrAFullContainer()
    {
        // The engine's own sale skips these, so a counter that "buys" them takes nothing.
        // A serial-built item starts fixed in place, as before load; loose goods move.
        var bag = new Bag((Serial)0x7F41) { Movable = true };
        bag.AddItem(new Amber((Serial)0x7F42) { Movable = true });

        Assert.True(SaleGoods.CounterTakes(new Amber((Serial)0x7F43) { Movable = true }));
        Assert.True(SaleGoods.CounterTakes(new Bag((Serial)0x7F44) { Movable = true }));
        Assert.False(SaleGoods.CounterTakes(bag));
        Assert.False(SaleGoods.CounterTakes(new Amber((Serial)0x7F45) { Movable = true, LootType = LootType.Blessed }));
        Assert.False(SaleGoods.CounterTakes(new Amber((Serial)0x7F46)));
        Assert.False(SaleGoods.CounterTakes(null));
    }

    [Fact]
    public void AnyTakes_ABuyerOfTheSoldTypeInReachThatStillDeals()
    {
        // The smith buys ingots, so it takes the ore a forge makes them of. Nobody buys the
        // logs past the reach, and a buyer that no longer deals takes nothing.
        var book = new BuyerBook<string>();
        book.Add(Carpenter, Britain, [typeof(Log)]);
        book.Add(Smith, Britain, [typeof(IronIngot)]);
        var log = new Log((Serial)0x7F51) { Movable = true };
        var ore = new IronOre((Serial)0x7F52) { Movable = true };

        Assert.True(SaleGoods.AnyTakes(book, log, Britain, Reach, _ => true));
        Assert.True(SaleGoods.AnyTakes(book, ore, Britain, Reach, _ => true));
        Assert.False(SaleGoods.AnyTakes(book, log, Far, Reach, _ => true));
        Assert.False(SaleGoods.AnyTakes(book, log, Britain, Reach, buyer => buyer != Carpenter));
        Assert.False(SaleGoods.AnyTakes(BuyerBook<string>.Empty, log, Britain, Reach, _ => true));
        Assert.False(SaleGoods.AnyTakes(book, new Log((Serial)0x7F53), Britain, Reach, _ => true));
    }
}
