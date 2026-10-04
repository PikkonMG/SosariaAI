using SosariaAI.Economy;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class CraftMarketRulesTests
{
    private const int IngotShelfPrice = 5;
    private const int BoardShelfPrice = 3;
    private const int CheapShelfPrice = 1;
    private const int Load = 60;

    // T2A alchemist shelf and counter (SBAlchemist): garlic and sulfurous ash 3, an empty bottle 5;
    // the counter pays 7 for a lesser cure potion and 10 for a lesser explosion potion.
    private const int ReagentShelfPrice = 3;
    private const int BottleShelfPrice = 5;
    private const int LesserCureCounterPrice = 7;
    private const int LesserExplosionCounterPrice = 10;
    private const double CertainChance = 1.0;
    private const int ManyRolls = 1200;

    [Fact]
    public void UnitPrice_UndercutsTheShelf_AndFallsBackWithoutOne()
    {
        Assert.Equal(3, CraftMarketRules.UnitPrice(IngotShelfPrice));
        Assert.Equal(2, CraftMarketRules.UnitPrice(BoardShelfPrice));
        Assert.Equal(1, CraftMarketRules.UnitPrice(CheapShelfPrice));
        Assert.Equal(CraftMarketRules.FallbackUnitPrice, CraftMarketRules.UnitPrice(0));
        Assert.True(CraftMarketRules.UnitPrice(IngotShelfPrice) < IngotShelfPrice);
    }

    [Fact]
    public void MaterialUnitPrice_GathererStockAtItsShare_ShopOnlyStockAtTheFullShelf()
    {
        Assert.Equal(CraftMarketRules.UnitPrice(IngotShelfPrice), CraftMarketRules.MaterialUnitPrice(IngotShelfPrice, gathererSells: true));
        Assert.Equal(BottleShelfPrice, CraftMarketRules.MaterialUnitPrice(BottleShelfPrice, gathererSells: false));
        Assert.Equal(CraftMarketRules.FallbackUnitPrice, CraftMarketRules.MaterialUnitPrice(0, gathererSells: false));
        Assert.Equal(CraftMarketRules.FallbackUnitPrice, CraftMarketRules.MaterialUnitPrice(0, gathererSells: true));
    }

    [Fact]
    public void ShopOnlyMaterials_ALesserCureLosesGold_SoTheAlchemistBrewsExplosionPotions()
    {
        // Alchemists spent 40 to 75 gold a batch and sold it for 35 to 56 until every purse sat
        // under the tool reserve and alchemy made nothing for an evening.
        var reagent = CraftMarketRules.MaterialUnitPrice(ReagentShelfPrice, gathererSells: false);
        var bottle = CraftMarketRules.MaterialUnitPrice(BottleShelfPrice, gathererSells: false);
        var cure = CraftTradeRules.ExpectedProfit(CertainChance, LesserCureCounterPrice, reagent + bottle);
        var explosion = CraftTradeRules.ExpectedProfit(CertainChance, LesserExplosionCounterPrice, reagent + bottle);
        var sharePriced = CraftMarketRules.UnitPrice(ReagentShelfPrice) + CraftMarketRules.UnitPrice(BottleShelfPrice);

        Assert.True(CraftTradeRules.ExpectedProfit(CertainChance, LesserCureCounterPrice, sharePriced) > 0);
        Assert.True(cure < 0);
        Assert.True(explosion > 0);

        CraftOption[] options =
        [
            new(0, CertainChance, HasMaterials: true, cure, HasBuyer: true),
            new(1, CertainChance, HasMaterials: true, explosion, HasBuyer: true)
        ];

        for (var roll = 0; roll < ManyRolls; roll++)
        {
            Assert.Equal(1, CraftTradeRules.Pick(options, roll));
        }
    }

    [Fact]
    public void UnitsToBuy_TakesTheWholeLoadWhenGoldAndRoomAllow()
    {
        var gold = CraftTradeRules.ToolReserveGold + Load * IngotShelfPrice;

        Assert.Equal(Load, CraftMarketRules.UnitsToBuy(Load, 0, gold, IngotShelfPrice));
    }

    [Fact]
    public void UnitsToBuy_KeepsTheToolReserve()
    {
        var gold = CraftTradeRules.ToolReserveGold + 10 * IngotShelfPrice;

        Assert.Equal(10, CraftMarketRules.UnitsToBuy(Load, 0, gold, IngotShelfPrice));
        Assert.Equal(0, CraftMarketRules.UnitsToBuy(Load, 0, CraftTradeRules.ToolReserveGold, IngotShelfPrice));
    }

    [Fact]
    public void UnitsToBuy_StopsAtTheStockCap()
    {
        var rich = 100_000;

        Assert.Equal(10, CraftMarketRules.UnitsToBuy(Load, CraftMarketRules.StockCap - 10, rich, IngotShelfPrice));
        Assert.Equal(0, CraftMarketRules.UnitsToBuy(Load, CraftMarketRules.StockCap, rich, IngotShelfPrice));
    }

    [Fact]
    public void UnitsToBuy_NothingOfferedNothingBought()
    {
        Assert.Equal(0, CraftMarketRules.UnitsToBuy(0, 0, 1000, IngotShelfPrice));
        Assert.Equal(0, CraftMarketRules.UnitsToBuy(Load, 0, 1000, 0));
    }

    [Fact]
    public void Surplus_IsWhatAGathererCrafterCarriesPastItsOwnKeep()
    {
        Assert.Equal(0, CraftMarketRules.Surplus(0));
        Assert.Equal(0, CraftMarketRules.Surplus(CraftMarketRules.OwnStockKeep));
        Assert.Equal(Load, CraftMarketRules.Surplus(CraftMarketRules.OwnStockKeep + Load));
        Assert.True(CraftMarketRules.OwnStockKeep < CraftMarketRules.StockCap);
    }

    [Fact]
    public void GoesForOwnStock_WhenLowAndNoGathererSells()
    {
        // Smiths bought shelf ingots at a loss and ended "could not get materials" with an empty
        // purse: the shelf is no reason to stay out of the mine.
        Assert.True(CraftMarketRules.GoesForOwnStock(0, gathererSells: false));
        Assert.True(CraftMarketRules.GoesForOwnStock(CraftMarketRules.OwnStockLow - 1, gathererSells: false));
        Assert.False(CraftMarketRules.GoesForOwnStock(0, gathererSells: true));
        Assert.False(CraftMarketRules.GoesForOwnStock(CraftMarketRules.OwnStockLow, gathererSells: false));
        Assert.True(CraftMarketRules.OwnStockLow < CraftMarketRules.OwnStockKeep);
    }

    [Fact]
    public void PieceMaterialCost_CarriedStockAtTheMarketPrice_TheRestAtTheShelf()
    {
        const int perPiece = 3;
        var market = CraftMarketRules.UnitPrice(IngotShelfPrice);

        Assert.Equal(perPiece * market, CraftMarketRules.PieceMaterialCost(perPiece, perPiece, IngotShelfPrice, gathererSells: true));
        Assert.Equal(perPiece * IngotShelfPrice, CraftMarketRules.PieceMaterialCost(perPiece, 0, IngotShelfPrice, gathererSells: true));
        Assert.Equal(
            market + (perPiece - 1) * IngotShelfPrice,
            CraftMarketRules.PieceMaterialCost(perPiece, 1, IngotShelfPrice, gathererSells: true)
        );
        Assert.Equal(perPiece * BottleShelfPrice, CraftMarketRules.PieceMaterialCost(perPiece, perPiece, BottleShelfPrice, gathererSells: false));
        Assert.Equal(0, CraftMarketRules.PieceMaterialCost(0, Load, IngotShelfPrice, gathererSells: true));
    }
}
