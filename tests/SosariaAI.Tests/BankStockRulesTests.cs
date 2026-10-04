using SosariaAI.Economy;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// Stock at the bank goes by the market table's stack rows, and a crafter takes only what it
/// can store and pay for at the top of the band, so the price it agrees to is always covered.
/// </summary>
public class BankStockRulesTests
{
    private const string IngotsKey = "ingots";
    private const string WoodKey = "wood";
    private const string PolearmKey = "polearm";
    private const string FoodKey = "food";
    private const int Load = 150;
    private const int Afforded = 20;

    private static GoodsRow Ingots => Appraisal.RowByKey(IngotsKey);

    [Fact]
    public void IsStockRow_ACraftersRawStockOnly()
    {
        Assert.True(BankStockRules.IsStockRow(Ingots));
        Assert.True(BankStockRules.IsStockRow(Appraisal.RowByKey(WoodKey)));
        Assert.False(BankStockRules.IsStockRow(Appraisal.RowByKey(PolearmKey)));
        Assert.False(BankStockRules.IsStockRow(Appraisal.RowByKey(FoodKey)));
        Assert.False(BankStockRules.IsStockRow(Appraisal.Other));
        Assert.False(BankStockRules.IsStockRow(null));
    }

    [Fact]
    public void LotUnits_PaysAtTheTopOfTheBand_AfterTheToolReserve()
    {
        var purse = CraftTradeRules.ToolReserveGold + Afforded * Ingots.UnitHigh;

        Assert.Equal(Afforded, BankStockRules.LotUnits(Load, 0, purse, Ingots));
        Assert.Equal(0, BankStockRules.LotUnits(Load, 0, CraftTradeRules.ToolReserveGold, Ingots));
    }

    [Fact]
    public void LotUnits_NoMoreThanRoomUnderTheCapOrTheLoad()
    {
        var rich = CraftMarketRules.StockCap * Ingots.UnitHigh * 2;
        var carried = CraftMarketRules.StockCap - Afforded;

        Assert.Equal(Afforded, BankStockRules.LotUnits(Load, carried, rich, Ingots));
        Assert.Equal(Load, BankStockRules.LotUnits(Load, 0, rich, Ingots));
        Assert.Equal(0, BankStockRules.LotUnits(Load, CraftMarketRules.StockCap, rich, Ingots));
    }

    [Fact]
    public void PaysForAUnit_OnlyWithCoinForOneUnitAtTheTopOfTheBandPastTheToolReserve()
    {
        // A carpenter with too little coin stood out its whole bank wait beside a lumberjack
        // holding logs up: it could not pay for one log.
        var wood = Appraisal.RowByKey(WoodKey);

        Assert.True(BankStockRules.PaysForAUnit(CraftTradeRules.ToolReserveGold + wood.UnitHigh, wood));
        Assert.False(BankStockRules.PaysForAUnit(CraftTradeRules.ToolReserveGold + wood.UnitHigh - 1, wood));
        Assert.False(BankStockRules.PaysForAUnit(CraftMarketRules.StockCap * wood.UnitHigh, Appraisal.RowByKey(PolearmKey)));
    }

    [Fact]
    public void LotUnits_NothingForGearOrUnpricedGoods()
    {
        var rich = CraftMarketRules.StockCap * Ingots.UnitHigh * 2;

        Assert.Equal(0, BankStockRules.LotUnits(Load, 0, rich, Appraisal.RowByKey(PolearmKey)));
        Assert.Equal(0, BankStockRules.LotUnits(Load, 0, rich, Appraisal.Other));
    }
}
