using System;
using System.Collections.Generic;
using SosariaAI.Combat;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class CraftTradeRulesTests
{
    private const double Certain = 1.0;
    private const double Fair = 0.6;
    private const double Poor = 0.3;
    private const double Half = 0.5;
    private const int Profit = 20;
    private const int Loss = -5;
    private const int Worth = 40;
    private const int MaterialCost = 6;
    private const int ManyRolls = 1200;
    private const int HalfWorth = Worth / 2;
    private const int TenthsScale = 10;

    [Fact]
    public void Pick_NothingWithoutOptions()
    {
        Assert.Equal(CraftTradeRules.NoPick, CraftTradeRules.Pick(null, 0));
        Assert.Equal(CraftTradeRules.NoPick, CraftTradeRules.Pick([], 0));
    }

    [Fact]
    public void Pick_SkipsItemsWithoutMaterialsBelowEvenOddsOrWithNoBuyer()
    {
        // Tailors sewed oil cloth and carpenters made writing tables that no vendor buys.
        CraftOption[] options =
        [
            new(0, Fair, HasMaterials: false, Profit, HasBuyer: true),
            new(1, Poor, HasMaterials: true, Profit, HasBuyer: true),
            new(2, Certain, HasMaterials: true, Profit, HasBuyer: false)
        ];

        Assert.Equal(CraftTradeRules.NoPick, CraftTradeRules.Pick(options, 0));
    }

    [Fact]
    public void Pick_DropsLossesWhileSomethingEarns()
    {
        CraftOption[] options =
        [
            new(0, Certain, true, Loss, true),
            new(1, Certain, true, Profit, true)
        ];

        for (var roll = 0; roll < ManyRolls; roll++)
        {
            Assert.Equal(1, CraftTradeRules.Pick(options, roll));
        }
    }

    [Fact]
    public void Pick_WeighsByTheGoldATryEarns_AndATeachingItemWeighsMore()
    {
        CraftOption[] options =
        [
            new(0, Certain, true, Profit, true),
            new(1, Certain, true, Profit * CraftTradeRules.TeachWeight, true),
            new(2, Fair, true, Profit, true)
        ];
        var counts = new int[options.Length];
        var totalWeight = Profit + Profit * CraftTradeRules.TeachWeight + Profit * CraftTradeRules.TeachWeight;

        for (var roll = 0; roll < totalWeight; roll++)
        {
            counts[CraftTradeRules.Pick(options, roll)]++;
        }

        Assert.True(counts[1] > counts[0]);
        Assert.Equal(counts[1], counts[2]);
    }

    [Fact]
    public void Pick_OnlyLossesLeft_StillMakesSomethingThatSells()
    {
        CraftOption[] options = [new(0, Certain, true, Loss, true)];

        Assert.Equal(0, CraftTradeRules.Pick(options, 0));
    }

    [Fact]
    public void Pick_NothingEarns_PractisesTheCheapestItemThatStillTeaches()
    {
        // No potion, scroll, meal or map pays back shelf stock at a counter: a crafter practises
        // on the cheap lesson, not on plate it cannot sell for its ingots.
        CraftOption[] options =
        [
            new(0, Certain, true, Loss + 1, true),
            new(1, Fair, true, Loss, true),
            new(2, Fair, true, Loss * TenthsScale, true)
        ];

        for (var roll = 0; roll < ManyRolls; roll++)
        {
            Assert.Equal(1, CraftTradeRules.Pick(options, roll));
        }
    }

    [Fact]
    public void Pick_NothingEarns_KeepsTheLossesWithinTheSlack()
    {
        CraftOption[] options =
        [
            new(0, Fair, true, Loss, true),
            new(1, Fair, true, Loss - CraftTradeRules.PracticeLossSlack, true),
            new(2, Fair, true, Loss - CraftTradeRules.PracticeLossSlack - 1, true)
        ];
        var counts = new int[options.Length];

        for (var roll = 0; roll < ManyRolls; roll++)
        {
            counts[CraftTradeRules.Pick(options, roll)]++;
        }

        Assert.True(counts[0] > 0 && counts[1] > 0);
        Assert.Equal(0, counts[2]);
    }

    [Fact]
    public void Pick_NothingEarnsOrTeaches_TakesTheSmallestLoss()
    {
        CraftOption[] options =
        [
            new(0, Certain, true, Loss * TenthsScale, true),
            new(1, Certain, true, Loss, true)
        ];

        for (var roll = 0; roll < ManyRolls; roll++)
        {
            Assert.Equal(1, CraftTradeRules.Pick(options, roll));
        }
    }

    [Fact]
    public void TryPause_StaysInThePauseWindow_AndVaries()
    {
        var seen = new HashSet<double>();

        for (var roll = 0; roll < CraftTradeRules.MaxTryPauseSeconds * 2; roll++)
        {
            var pause = CraftTradeRules.TryPause(roll).TotalSeconds;
            Assert.InRange(pause, CraftTradeRules.MinTryPauseSeconds, CraftTradeRules.MaxTryPauseSeconds);
            seen.Add(pause);
        }

        Assert.Equal(CraftTradeRules.MaxTryPauseSeconds - CraftTradeRules.MinTryPauseSeconds + 1, seen.Count);
        Assert.InRange(CraftTradeRules.TryPause(int.MinValue).TotalSeconds, CraftTradeRules.MinTryPauseSeconds, CraftTradeRules.MaxTryPauseSeconds);
    }

    [Fact]
    public void WaitsToRecover_OnlyForAPoolThatRefillsFarEnough()
    {
        const int need = 20;

        Assert.True(CraftTradeRules.WaitsToRecover(need - 1, need, need));
        Assert.False(CraftTradeRules.WaitsToRecover(need, need, need));
        Assert.False(CraftTradeRules.WaitsToRecover(0, need, need - 1));
        Assert.False(CraftTradeRules.WaitsToRecover(0, 0, need));
    }

    [Fact]
    public void ExpectedProfit_TheOddsTimesTheWorthLessTheMaterials()
    {
        Assert.Equal(HalfWorth - MaterialCost, CraftTradeRules.ExpectedProfit(Half, Worth, MaterialCost));
        Assert.Equal(Worth - MaterialCost, CraftTradeRules.ExpectedProfit(Certain + Fair, Worth, MaterialCost));
        Assert.Equal(-MaterialCost, CraftTradeRules.ExpectedProfit(Certain, 0, MaterialCost));
    }

    [Fact]
    public void PieceWorth_ACounterFirst_ElseAShareOfWhatPeoplePay_ElseNothing()
    {
        Assert.Equal(Worth, CraftTradeRules.PieceWorth(Worth, Worth + Worth));
        Assert.Equal(Worth * CraftTradeRules.UnvendedShareTenths / TenthsScale, CraftTradeRules.PieceWorth(0, Worth));
        Assert.Equal(0, CraftTradeRules.PieceWorth(0, 0));
    }

    [Fact]
    public void EmptyBatches_DropTheItemThenRest()
    {
        // Cooks tried the same ribs every two seconds for three hours and made nothing.
        Assert.False(CraftTradeRules.DropsItem(CraftTradeRules.EmptyBatchesPerItem - 1));
        Assert.True(CraftTradeRules.DropsItem(CraftTradeRules.EmptyBatchesPerItem));
        Assert.False(CraftTradeRules.RestsAfterEmpty(CraftTradeRules.MaxEmptyBatches - 1));
        Assert.True(CraftTradeRules.RestsAfterEmpty(CraftTradeRules.MaxEmptyBatches));
    }

    [Fact]
    public void AnswersMakersMark_OnlyAGrandmastersUnansweredMarkablePiece()
    {
        Assert.True(CraftTradeRules.AnswersMakersMark(true, SkillTierRules.GrandmasterSkill, true, true, true));
        Assert.False(CraftTradeRules.AnswersMakersMark(false, SkillTierRules.GrandmasterSkill, true, true, true));
        Assert.False(CraftTradeRules.AnswersMakersMark(true, SkillTierRules.GrandmasterSkill - Fair, true, true, true));
        Assert.False(CraftTradeRules.AnswersMakersMark(true, SkillTierRules.GrandmasterSkill, false, true, true));
        Assert.False(CraftTradeRules.AnswersMakersMark(true, SkillTierRules.GrandmasterSkill, true, false, true));
        Assert.False(CraftTradeRules.AnswersMakersMark(true, SkillTierRules.GrandmasterSkill, true, true, false));
    }

    [Fact]
    public void BatchSize_StaysInTheBatchWindow()
    {
        for (var roll = 0; roll < CraftTradeRules.MaxBatchCrafts * 2; roll++)
        {
            Assert.InRange(CraftTradeRules.BatchSize(roll), CraftTradeRules.MinBatchCrafts, CraftTradeRules.MaxBatchCrafts);
        }
    }

    [Fact]
    public void SessionLength_StaysInTheSessionWindow()
    {
        for (var roll = 0; roll < CraftTradeRules.MaxSessionMinutes * 2; roll++)
        {
            Assert.InRange(
                CraftTradeRules.SessionLength(roll).TotalMinutes,
                CraftTradeRules.MinSessionMinutes,
                CraftTradeRules.MaxSessionMinutes
            );
        }
    }

    [Theory]
    [InlineData(3, 5, 0, 15)]
    [InlineData(3, 5, 10, 5)]
    [InlineData(3, 5, 20, 0)]
    [InlineData(-1, 5, 0, 0)]
    public void MaterialsToBuy_CoversTheBatch(int perCraft, int crafts, int have, int expected) =>
        Assert.Equal(expected, CraftTradeRules.MaterialsToBuy(perCraft, crafts, have));

    [Fact]
    public void CraftsCovered_CountsWholePieces()
    {
        Assert.Equal(3, CraftTradeRules.CraftsCovered(3, 11));
        Assert.Equal(0, CraftTradeRules.CraftsCovered(3, 2));
        Assert.Equal(int.MaxValue, CraftTradeRules.CraftsCovered(0, 2));
    }

    [Fact]
    public void PiecesToKeep_HoldsBackTheMostValuable()
    {
        Assert.Equal(CraftTradeRules.HawkerStock, CraftTradeRules.PiecesToKeep([60, 480, 90, 480]).Count);
        Assert.Equal([1, 3], CraftTradeRules.PiecesToKeep([60, 480, 90, 480]));
        Assert.Equal([2, 0], CraftTradeRules.PiecesToKeep([90, 60, 480]));
    }

    [Fact]
    public void PiecesToKeep_FewerPiecesAreAllKept()
    {
        Assert.Equal([0], CraftTradeRules.PiecesToKeep([60]));
        Assert.Empty(CraftTradeRules.PiecesToKeep([]));
        Assert.Empty(CraftTradeRules.PiecesToKeep(null));
    }

    [Fact]
    public void StockBudget_KeepsTheToolReserve()
    {
        Assert.Equal(0, CraftTradeRules.StockBudget(CraftTradeRules.ToolReserveGold));
        Assert.Equal(0, CraftTradeRules.StockBudget(0));
        Assert.Equal(100, CraftTradeRules.StockBudget(CraftTradeRules.ToolReserveGold + 100));
    }

    [Fact]
    public void StockBudget_TheSpareToolIsTheReserve()
    {
        const int purse = 60;

        Assert.Equal(CraftTradeRules.ToolReserveGold, CraftTradeRules.ToolReserve(CraftTradeRules.ToolsKept - 1));
        Assert.Equal(0, CraftTradeRules.ToolReserve(CraftTradeRules.ToolsKept));
        Assert.Equal(purse - CraftTradeRules.ToolReserveGold, CraftTradeRules.StockBudget(purse, CraftTradeRules.ToolsKept - 1));
        Assert.Equal(purse, CraftTradeRules.StockBudget(purse, CraftTradeRules.ToolsKept));
        Assert.Equal(CraftTradeRules.StockBudget(purse), CraftTradeRules.StockBudget(purse, CraftTradeRules.NoToolsCounted));
    }

    [Fact]
    public void AnnouncesMasterwork_OnlyAGrandmastersExceptionalPiece()
    {
        Assert.True(CraftTradeRules.AnnouncesMasterwork(SkillTierRules.GrandmasterSkill, exceptional: true));
        Assert.False(CraftTradeRules.AnnouncesMasterwork(SkillTierRules.GrandmasterSkill, exceptional: false));
        Assert.False(CraftTradeRules.AnnouncesMasterwork(SkillTierRules.GrandmasterSkill - 1, exceptional: true));
    }

    [Fact]
    public void SessionOver_AtTheLength()
    {
        var length = TimeSpan.FromMinutes(CraftTradeRules.MinSessionMinutes);

        Assert.False(CraftTradeRules.SessionOver(length - TimeSpan.FromSeconds(1), length));
        Assert.True(CraftTradeRules.SessionOver(length, length));
    }

    [Fact]
    public void MeetsNeed_AToolTripGoesOnlyWhereTheToolIsSold()
    {
        // The Trinsic tinker walked to the blacksmith for tinker tools: it sold ingots, not tools.
        Assert.False(CraftTradeRules.MeetsNeed(shelvesStock: true, sellsTool: false, needTool: true));
        Assert.True(CraftTradeRules.MeetsNeed(shelvesStock: false, sellsTool: true, needTool: true));
        Assert.True(CraftTradeRules.MeetsNeed(shelvesStock: true, sellsTool: false, needTool: false));
        Assert.False(CraftTradeRules.MeetsNeed(shelvesStock: false, sellsTool: true, needTool: false));
    }

    [Fact]
    public void ToolsToBuy_KeepsTheWorkingToolAndASpare()
    {
        Assert.Equal(CraftTradeRules.ToolsKept, CraftTradeRules.ToolsToBuy(0));
        Assert.Equal(1, CraftTradeRules.ToolsToBuy(CraftTradeRules.ToolsKept - 1));
        Assert.Equal(0, CraftTradeRules.ToolsToBuy(CraftTradeRules.ToolsKept));
        Assert.Equal(0, CraftTradeRules.ToolsToBuy(CraftTradeRules.ToolsKept + 1));
    }

    [Fact]
    public void MakesSpareTool_OnlyATradeThatMakesItsToolAndStillHasOne()
    {
        Assert.True(CraftTradeRules.MakesSpareTool(makesItsTool: true, carried: 1));
        Assert.False(CraftTradeRules.MakesSpareTool(makesItsTool: true, carried: 0));
        Assert.False(CraftTradeRules.MakesSpareTool(makesItsTool: true, carried: CraftTradeRules.ToolsKept));
        Assert.False(CraftTradeRules.MakesSpareTool(makesItsTool: false, carried: 1));
    }

    [Fact]
    public void WaitsForSeller_OnlyForStockPeopleBring_AndNotWhenItGathersItsOwn()
    {
        Assert.True(CraftTradeRules.WaitsForSeller(peopleBringStock: true, gathersOwnStock: false));
        Assert.False(CraftTradeRules.WaitsForSeller(peopleBringStock: true, gathersOwnStock: true));
        Assert.False(CraftTradeRules.WaitsForSeller(peopleBringStock: false, gathersOwnStock: false));
    }

    [Fact]
    public void DryWaitOver_AfterTheDryWait()
    {
        Assert.False(CraftTradeRules.DryWaitOver(CraftTradeRules.DryWait - TimeSpan.FromSeconds(1)));
        Assert.True(CraftTradeRules.DryWaitOver(CraftTradeRules.DryWait));
    }

    [Fact]
    public void DryCheckDue_EveryCheckGap()
    {
        Assert.False(CraftTradeRules.DryCheckDue(CraftTradeRules.DryCheckGap - TimeSpan.FromSeconds(1)));
        Assert.True(CraftTradeRules.DryCheckDue(CraftTradeRules.DryCheckGap));
    }

    [Fact]
    public void PaysForUnit_NeedsCoinForOneUnitPastTheToolReserve()
    {
        // Tailors waited three minutes at the station over a hundred times in half an hour for
        // stock they could not get: a shelf is stock in reach only with coin for a unit of it.
        const int unitPrice = 16;

        Assert.True(CraftTradeRules.PaysForUnit(CraftTradeRules.ToolReserveGold + unitPrice, unitPrice));
        Assert.False(CraftTradeRules.PaysForUnit(CraftTradeRules.ToolReserveGold + unitPrice - 1, unitPrice));
        Assert.True(CraftTradeRules.PaysForUnit(CraftTradeRules.ToolReserveGold + 1, 0));
        Assert.False(CraftTradeRules.PaysForUnit(CraftTradeRules.ToolReserveGold, 0));
        Assert.False(CraftTradeRules.PaysForUnit(0, unitPrice));
        Assert.True(CraftTradeRules.PaysForUnit(unitPrice, unitPrice, CraftTradeRules.ToolsKept));
        Assert.False(CraftTradeRules.PaysForUnit(unitPrice, unitPrice, CraftTradeRules.ToolsKept - 1));
    }

    [Fact]
    public void PaysForTool_NeedsThePriceOfTheToolAndNoReserve()
    {
        // A scribe with four coins walked to the mage shop for an 8 gold pen and failed
        // "could not get a tool" every few minutes: a shelf of tools helps only a purse that pays.
        const int penPrice = 8;

        Assert.True(CraftTradeRules.PaysForTool(penPrice, penPrice));
        Assert.False(CraftTradeRules.PaysForTool(penPrice - 1, penPrice));
        Assert.True(CraftTradeRules.PaysForTool(penPrice, 0));
        Assert.False(CraftTradeRules.PaysForTool(0, 0));
        Assert.True(CraftTradeRules.PaysForTool(penPrice, penPrice) && !CraftTradeRules.PaysForUnit(penPrice, penPrice));
    }

    [Fact]
    public void PieceWorth_ShopValueBeatsTheCounter()
    {
        const int counter = 151;
        const int shop = 2400;

        Assert.Equal(shop, CraftTradeRules.PieceWorth(counter, 0, shop));
        Assert.Equal(counter, CraftTradeRules.PieceWorth(counter, 0, 0));
    }
}
