using System;
using Server.Items;
using SosariaAI.Economy;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// Bandages from cloth when the healer's 20 are gone: the cheapest cloth per bandage still
/// wanted that the gold buys and the shelf stocks, never dearer than the healer's own price.
/// </summary>
public class ClothBandageRulesTests
{
    private const int ShelfLine = 20;
    private const int WeaverLines = 4;
    private const int TailorClothPrice = 2;
    private const int WeaverUncutPrice = 3;
    private const int BoltPrice = 100;
    private const int Rich = 1000;
    private const int TwoLinesOfCloth = ShelfLine * 2;

    private static readonly ClothOffer[] Tailor =
    [
        new(typeof(Cloth), TailorClothPrice, ClothBandageRules.BandagesPerCloth, ShelfLine),
        new(typeof(UncutCloth), TailorClothPrice, ClothBandageRules.BandagesPerCloth, ShelfLine),
        new(typeof(BoltOfCloth), BoltPrice, ClothBandageRules.BandagesPerBolt, ShelfLine)
    ];

    private static readonly ClothOffer[] Weaver =
    [
        new(typeof(UncutCloth), WeaverUncutPrice, ClothBandageRules.BandagesPerCloth, ShelfLine * WeaverLines),
        new(typeof(BoltOfCloth), BoltPrice, ClothBandageRules.BandagesPerBolt, ShelfLine * WeaverLines)
    ];

    [Fact]
    public void BandagesEach_ABoltMakesFiftyAndClothOne()
    {
        Assert.Equal(ClothBandageRules.ClothPerBolt, ClothBandageRules.BandagesEach(typeof(BoltOfCloth)));
        Assert.Equal(ClothBandageRules.BandagesPerCloth, ClothBandageRules.BandagesEach(typeof(Cloth)));
        Assert.Equal(ClothBandageRules.BandagesPerCloth, ClothBandageRules.BandagesEach(typeof(UncutCloth)));
    }

    [Fact]
    public void BandagesIn_CountsClothAndBolts() =>
        Assert.Equal(TwoLinesOfCloth + ClothBandageRules.BandagesPerBolt, ClothBandageRules.BandagesIn(TwoLinesOfCloth, 1));

    [Fact]
    public void Plan_AtTheTailor_BuysTheClothToTheBandage()
    {
        var lines = ClothBandageRules.Plan(TwoLinesOfCloth, Rich, Tailor);

        Assert.Equal<(Type, int)>([(typeof(Cloth), ShelfLine), (typeof(UncutCloth), ShelfLine)], lines);
    }

    [Fact]
    public void Plan_AtTheWeaver_ABoltWhenItComesCheaper()
    {
        Assert.Equal<(Type, int)>([(typeof(BoltOfCloth), 1)], ClothBandageRules.Plan(TwoLinesOfCloth, Rich, Weaver));

        const int Thirty = 30;
        Assert.Equal<(Type, int)>([(typeof(UncutCloth), Thirty)], ClothBandageRules.Plan(Thirty, Rich, Weaver));
    }

    [Fact]
    public void Plan_TwoBoltsJoinOneLine()
    {
        const int Ninety = 90;

        Assert.Equal<(Type, int)>([(typeof(BoltOfCloth), 2)], ClothBandageRules.Plan(Ninety, Rich, Weaver));
    }

    [Fact]
    public void Plan_NeverPaysMoreThanTheHealerPerBandage()
    {
        var lines = ClothBandageRules.Plan(SupplyRules.BandageTarget, Rich, Tailor);

        Assert.Equal<(Type, int)>([(typeof(Cloth), ShelfLine), (typeof(UncutCloth), ShelfLine)], lines);
    }

    [Fact]
    public void Plan_APoorBuyer_BuysWhatItCanPayFor()
    {
        const int Purse = 15;

        Assert.Equal<(Type, int)>([(typeof(Cloth), Purse / TailorClothPrice)], ClothBandageRules.Plan(TwoLinesOfCloth, Purse, Tailor));
    }

    [Fact]
    public void Plan_NothingShortNoShelfOrNoGold_BuysNothing()
    {
        Assert.Empty(ClothBandageRules.Plan(0, Rich, Tailor));
        Assert.Empty(ClothBandageRules.Plan(TwoLinesOfCloth, Rich, []));
        Assert.Empty(ClothBandageRules.Plan(TwoLinesOfCloth, TailorClothPrice - 1, Tailor));
        Assert.Empty(ClothBandageRules.Plan(TwoLinesOfCloth, Rich, null));
    }

    [Fact]
    public void Plan_AnEmptyLine_IsPassedOver()
    {
        ClothOffer[] bare =
        [
            new(typeof(Cloth), TailorClothPrice, ClothBandageRules.BandagesPerCloth, 0),
            new(typeof(UncutCloth), TailorClothPrice, ClothBandageRules.BandagesPerCloth, ShelfLine)
        ];

        Assert.Equal<(Type, int)>([(typeof(UncutCloth), ShelfLine)], ClothBandageRules.Plan(TwoLinesOfCloth, Rich, bare));
    }

    [Fact]
    public void IsMaking_TheScissorsAndTheCloth_NotTheBandage()
    {
        Assert.True(ClothBandages.IsMaking(typeof(Scissors)));
        Assert.True(ClothBandages.IsMaking(typeof(Cloth)));
        Assert.True(ClothBandages.IsMaking(typeof(UncutCloth)));
        Assert.True(ClothBandages.IsMaking(typeof(BoltOfCloth)));
        Assert.False(ClothBandages.IsMaking(typeof(Bandage)));
    }

    [Fact]
    public void MadeFromCloth_OnlyBandages()
    {
        Assert.True(SupplyRules.MadeFromCloth(SupplyKind.Bandages));
        Assert.False(SupplyRules.MadeFromCloth(SupplyKind.Arrows));
        Assert.False(SupplyRules.MadeFromCloth(SupplyKind.Reagents));
    }
}
