using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Economy;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class TradeDemandRulesTests
{
    private const int Poor = 10;
    private const int Rich = 100000;
    private const int FirstPick = 0;

    [Fact]
    public void AppetiteOf_MagesBuyRegsFightersBuyPlate()
    {
        var mage = TradeDemandRules.AppetiteOf(PersonClass.Mage, CombatStyle.Mage);
        var warrior = TradeDemandRules.AppetiteOf(PersonClass.Warrior, CombatStyle.Melee);
        var regs = Appraisal.RowByKey(Appraisal.ReagentsKey);
        var plate = Appraisal.RowByKey("plate");

        Assert.True(TradeDemandRules.Wants(mage, regs));
        Assert.False(TradeDemandRules.Wants(mage, plate));
        Assert.True(TradeDemandRules.Wants(warrior, plate));
        Assert.False(TradeDemandRules.Wants(warrior, regs));
    }

    [Fact]
    public void AppetiteOf_LaterClasses_ShopLikeTheirSecondAgeKin()
    {
        var regs = Appraisal.RowByKey(Appraisal.ReagentsKey);
        var plate = Appraisal.RowByKey("plate");

        Assert.True(TradeDemandRules.Wants(TradeDemandRules.AppetiteOf(PersonClass.Necromancer, CombatStyle.Mage), regs));
        Assert.True(TradeDemandRules.Wants(TradeDemandRules.AppetiteOf(PersonClass.Paladin, CombatStyle.Melee), plate));
        Assert.True(TradeDemandRules.Wants(TradeDemandRules.AppetiteOf(PersonClass.Samurai, CombatStyle.Melee), plate));
        Assert.Equal(
            TradeDemandRules.AppetiteOf(PersonClass.Fencer, CombatStyle.Melee),
            TradeDemandRules.AppetiteOf(PersonClass.Ninja, CombatStyle.Melee)
        );
    }

    [Fact]
    public void AppetiteOf_AMerchantBuysAnythingWithAMarket()
    {
        // Treasure maps and SOS bottles have no general market: only a character who can
        // decode one buys it, through the treasure market.
        var merchant = TradeDemandRules.AppetiteOf(PersonClass.Merchant, CombatStyle.Melee);

        foreach (var row in Appraisal.Rows)
        {
            Assert.Equal(row.Appetite != TradeAppetite.None, TradeDemandRules.Wants(merchant, row));
        }
    }

    [Fact]
    public void Runebook_SellsToARecallerWithoutOne()
    {
        // A scribe's runebook went unsold at the bank: no one's appetite took it.
        var runebooks = Appraisal.RowByKey(Appraisal.RunebookKey);
        var warrior = TradeDemandRules.AppetiteOf(PersonClass.Warrior, CombatStyle.Melee);

        Assert.NotNull(runebooks);
        Assert.False(TradeDemandRules.Wants(warrior, runebooks));
        Assert.True(TradeDemandRules.Wants(warrior | TradeAppetite.Travel, runebooks));
    }

    [Fact]
    public void DemandPercent_MarkAndMagicSell()
    {
        var row = Appraisal.RowByKey("polearm");
        var plain = TradeDemandRules.DemandPercent(new GoodsClaim(row, 1, false, Appraisal.NoMagic));
        var gm = TradeDemandRules.DemandPercent(new GoodsClaim(row, 1, true, Appraisal.NoMagic));
        var vanq = TradeDemandRules.DemandPercent(new GoodsClaim(row, 1, false, Appraisal.MaxMagicLevel));

        Assert.True(gm > plain);
        Assert.True(vanq > plain);
        Assert.True(vanq <= TradeDemandRules.MaxDemandPercent);
    }

    [Fact]
    public void WantFor_TheShortSupplyWhenThePurseCovers()
    {
        var need = new SupplyNeed(SupplyKind.Reagents, 5, 40);
        var want = TradeDemandRules.WantFor(need, TradeAppetite.Caster, Rich, FirstPick);

        Assert.Equal(Appraisal.ReagentsKey, want?.Row.Key);
        Assert.Equal(need.Shortfall, want?.Amount);
    }

    [Fact]
    public void WantFor_TravelReagentsTradeAsReagents()
    {
        var want = TradeDemandRules.WantFor(new SupplyNeed(SupplyKind.TravelReagents, 5, 30), TradeAppetite.Caster, Rich, FirstPick);

        Assert.Equal(Appraisal.ReagentsKey, want?.Row.Key);
    }

    [Fact]
    public void WantFor_NobodyShoutsForBlankRunes()
    {
        Assert.Null(TradeDemandRules.RowFor(SupplyKind.RecallRunes));

        var want = TradeDemandRules.WantFor(new SupplyNeed(SupplyKind.RecallRunes, 0, 4), TradeAppetite.Caster, Rich, FirstPick);

        Assert.NotEqual(Appraisal.RecallKey, want?.Row.Key);
        Assert.True(want?.Row.IsGear != false);
    }

    [Fact]
    public void WantFor_NothingAnEmptyPurseCannotBack() =>
        Assert.Null(TradeDemandRules.WantFor(new SupplyNeed(SupplyKind.Reagents, 5, 40), TradeAppetite.Caster, Poor, FirstPick));

    [Fact]
    public void WantFor_AMakersPieceOfClassGearWithoutASupplyNeed()
    {
        var want = TradeDemandRules.WantFor(null, TradeAppetite.Melee | TradeAppetite.Everyone, Rich, FirstPick);

        Assert.True(want?.Exceptional);
        Assert.True(want?.Row.IsGear);
        Assert.True(TradeDemandRules.Wants(TradeAppetite.Melee, want?.Row));
    }

    [Fact]
    public void SellerScore_NamedBeatsGoodsBeatsNearer()
    {
        const int near = 1;
        const int far = 5;

        Assert.True(TradeMarket.SellerScore(named: true, sellsTheGoods: false, far) >
                    TradeMarket.SellerScore(named: false, sellsTheGoods: true, near));
        Assert.True(TradeMarket.SellerScore(named: false, sellsTheGoods: true, far) >
                    TradeMarket.SellerScore(named: false, sellsTheGoods: false, near));
        Assert.True(TradeMarket.SellerScore(named: false, sellsTheGoods: false, near) >
                    TradeMarket.SellerScore(named: false, sellsTheGoods: false, far));
    }
}
