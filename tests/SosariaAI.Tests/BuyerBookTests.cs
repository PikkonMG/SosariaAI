using System;
using Server;
using SosariaAI.Economy;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>Who buys what and where, from the buyers' own lists.</summary>
public class BuyerBookTests
{
    private const int Reach = 400;
    private const string MaginciaProvisioner = "Magincia provisioner";
    private const string MoonglowJeweler = "Moonglow jeweler";
    private const string BritainJeweler = "Britain jeweler";

    private static readonly Point3D Magincia = new(3714, 2220, 20);
    private static readonly Point3D Moonglow = new(4442, 1172, 0);
    private static readonly Point3D Britain = new(1434, 1690, 0);

    private static readonly Type Gem = typeof(Server.Items.Amber);
    private static readonly Type Candle = typeof(Server.Items.Candle);

    private static BuyerBook<string> Towns()
    {
        var book = new BuyerBook<string>();
        book.Add(MaginciaProvisioner, Magincia, [Candle]);
        book.Add(MoonglowJeweler, Moonglow, [Gem]);
        book.Add(BritainJeweler, Britain, [Gem, Gem]);
        return book;
    }

    [Fact]
    public void AnyBuys_OnlyABuyerOfThatTypeInReach()
    {
        // Magincia's provisioner does not buy the gem, and Moonglow's jeweler stands too
        // far off: nobody in reach takes it, so the sale is never offered.
        var book = Towns();

        Assert.True(book.AnyBuys(Candle, Magincia, Reach, _ => true));
        Assert.False(book.AnyBuys(Gem, Magincia, Reach, _ => true));
        Assert.True(book.AnyBuys(Gem, Moonglow, Reach, _ => true));
        Assert.False(book.AnyBuys(null, Magincia, Reach, _ => true));
    }

    [Fact]
    public void AnyBuys_SkipsABuyerThatNoLongerTrades() =>
        Assert.False(Towns().AnyBuys(Candle, Magincia, Reach, buyer => buyer != MaginciaProvisioner));

    [Fact]
    public void NearestBuying_NearestFirst_EachOnce_OnlyThoseThatTrade()
    {
        var book = Towns();
        var near = new Point3D(Britain.X + 10, Britain.Y, Britain.Z);
        var wide = int.MaxValue;

        var found = book.NearestBuying([Gem, Candle, Gem], near, wide, _ => true);

        Assert.Equal(new[] { BritainJeweler, MaginciaProvisioner, MoonglowJeweler }, found);
        Assert.Equal(new[] { BritainJeweler }, book.NearestBuying([Gem], near, Reach, _ => true));
        Assert.Equal(new[] { MoonglowJeweler }, book.NearestBuying([Gem], near, wide, buyer => buyer != BritainJeweler));
        Assert.Empty(BuyerBook<string>.Empty.NearestBuying([Gem], near, wide, _ => true));
    }
}
