using System;
using System.Collections.Generic;
using Server.Items;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A character that lost its kit searched the shop list once per lost piece on every score.
/// The pieces are grouped by the shops that sell them, so one search answers each kind.
/// </summary>
public class VendorBuyKitGroupTests
{
    private static readonly string[] Smiths = ["blacksmith", "vendor:Armorer"];
    private static readonly string[] Bowyers = ["vendor:Bowyer"];

    [Fact]
    public void GroupByShops_OneGroupPerShopKind_InTheKitsOrder()
    {
        var groups = VendorBuySkill.GroupByShops(
            [typeof(Bow), typeof(PlateChest), typeof(Crossbow), typeof(PlateLegs)],
            ShopsFor
        );

        Assert.Equal(2, groups.Count);
        Assert.Same(Bowyers, groups[0].Shops);
        Assert.Equal([typeof(Bow), typeof(Crossbow)], groups[0].Pieces);
        Assert.Same(Smiths, groups[1].Shops);
        Assert.Equal([typeof(PlateChest), typeof(PlateLegs)], groups[1].Pieces);
    }

    [Fact]
    public void GroupByShops_APieceNoShopSellsIsLeftOut()
    {
        var groups = VendorBuySkill.GroupByShops([typeof(Lantern), typeof(Candle), typeof(Bow)], ShopsFor);

        var only = Assert.Single(groups);
        Assert.Equal([typeof(Bow)], only.Pieces);
    }

    private static IReadOnlyList<string> ShopsFor(Type type)
    {
        if (type == typeof(Lantern))
        {
            return null;
        }

        if (type == typeof(Candle))
        {
            return [];
        }

        return type == typeof(Bow) || type == typeof(Crossbow) ? Bowyers : Smiths;
    }
}
