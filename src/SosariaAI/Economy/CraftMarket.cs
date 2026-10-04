using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Social;
using SosariaAI.Spawning;

namespace SosariaAI.Economy;

/// <summary>
/// Gatherers and crafters dealing in raw stock for real coin. A miner who brings ingots to the
/// smithy sells them to a smith working there before the shop gets the rest; a lumberjack's
/// logs go to the carpenter or bowyer at the bench; and a crafter short of stock buys from a
/// gatherer standing near it before it walks to a vendor. The gold and the stack change hands in
/// the engine's trade window (<see cref="CharacterTrade"/>): nothing is made or destroyed. A
/// crafter never sells the stock any station work of its own burns, except a crafter who gathers
/// that stock itself: a smith who mines keeps enough ingots for its own forge and sells the rest
/// to other smiths. Cooks, who live by no career, sold one another the ribs on the fire every
/// two seconds for three hours, and every try burnt nothing. World thread only.
/// </summary>
public static class CraftMarket
{
    private static readonly ILogger logger = SosariaLog.For(typeof(CraftMarket));

    /// <summary>The trade a character lives by at a station, or null.</summary>
    public static CraftTrade TradeOf(SosariaCharacter character) =>
        character == null
            ? null
            : CraftCareerRules.TradeByKind(
                CraftCareerRules.TradeOf(
                    (character.PersonProfile ?? PersonProfile.Default).Class,
                    kind => character.Definition?.UsesSkill(kind) == true
                )
            );

    /// <summary>True when the character works the harvest that yields its own trade's stock: a smith whose routine mines.</summary>
    public static bool GathersOwnStock(SosariaCharacter character, CraftTrade trade) =>
        trade?.GatherKind != null && character?.Definition?.UsesSkill(trade.GatherKind) == true;

    /// <summary>
    /// Units of <paramref name="stock"/> the seller parts with to another player: all of it, but
    /// of the stock a station trade it works burns (<see cref="WorkedTradeBurning"/>) only the
    /// surplus past its keep (<see cref="CraftMarketRules.Surplus"/>) when it lives by that trade
    /// and gathers the stock itself, and none when it buys it. A gatherer whose routine works
    /// that trade now and then (<see cref="LivesByHarvest"/>) spares all of its harvest.
    /// </summary>
    public static int SpareUnits(SosariaCharacter seller, Item stock)
    {
        if (stock == null)
        {
            return 0;
        }

        var type = stock.GetType();

        if (WorkedTradeBurning(seller, type) is not { } trade || LivesByHarvest(seller, trade))
        {
            // A tamer's raw ribs are its pets' food, not stock for the cook.
            return Math.Max(0, stock.Amount - PetKeeper.FoodKept(seller, stock));
        }

        return GathersOwnStock(seller, trade)
            ? Math.Min(stock.Amount, CraftMarketRules.Surplus(seller.Backpack?.GetAmount(type) ?? 0))
            : 0;
    }

    /// <summary>
    /// True when the seller gathers the stock <paramref name="trade"/> burns but lives by no such
    /// trade: a lumberjack whose routine fletches now and then sells its logs, and the shop gets
    /// them all anyway (<see cref="SaleGoods"/> keeps back only the career trade's stock). The
    /// roster's lumberjacks fletch and its miners smith a dagger, so each kept its first hundred
    /// units from the crafters: 825 of 841 lumber loads of a night spared none, no gatherer held
    /// stock up at a bank, and the shop counter took every log.
    /// </summary>
    public static bool LivesByHarvest(SosariaCharacter seller, CraftTrade trade) =>
        GathersOwnStock(seller, trade) && TradeOf(seller) != trade;

    /// <summary>
    /// The station trade in the seller's routines that burns <paramref name="type"/>, its career
    /// trade first, or null: a hunter who cooks keeps its raw ribs for its own fire.
    /// </summary>
    public static CraftTrade WorkedTradeBurning(SosariaCharacter seller, Type type)
    {
        if (TradeOf(seller) is { } own && own.BurnsStock(type))
        {
            return own;
        }

        var trades = CraftCareerRules.StationTrades;

        for (var i = 0; i < trades.Count; i++)
        {
            if (trades[i].BurnsStock(type) && seller?.Definition?.UsesSkill(trades[i].Kind) == true)
            {
                return trades[i];
            }
        }

        return null;
    }

    /// <summary>The gatherer sells its stock to crafters in reach whose trade burns it. Returns the units sold.</summary>
    public static int SellToCrafters(SosariaCharacter gatherer)
    {
        var sold = 0;

        foreach (var crafter in PeopleNear(gatherer))
        {
            if (TradeOf(crafter) is { } trade)
            {
                sold += DealAll(gatherer, crafter, trade);
            }
        }

        return sold;
    }

    /// <summary>The crafter buys its trade's stock from people in reach who carry it. Returns the units bought.</summary>
    public static int BuyFromGatherers(SosariaCharacter crafter, CraftTrade trade)
    {
        var bought = 0;

        if (trade == null)
        {
            return bought;
        }

        foreach (var seller in PeopleNear(crafter))
        {
            bought += DealAll(seller, crafter, trade);
        }

        return bought;
    }

    private static List<SosariaCharacter> PeopleNear(SosariaCharacter person)
    {
        var found = new List<SosariaCharacter>();

        if (person?.Map == null || person.Map == Map.Internal || !person.Alive)
        {
            return found;
        }

        foreach (var mobile in person.Map.GetMobilesInRange(person.Location, CraftMarketRules.MeetRange))
        {
            if (mobile is SosariaCharacter { Deleted: false, Alive: true } other && other != person)
            {
                found.Add(other);
            }
        }

        return found;
    }

    private static int DealAll(SosariaCharacter seller, SosariaCharacter buyer, CraftTrade trade)
    {
        var pack = seller.Backpack;

        if (pack == null || buyer.Backpack == null)
        {
            return 0;
        }

        var stock = new List<Item>();

        foreach (var item in pack.Items)
        {
            if (item is { Deleted: false, Movable: true } && trade.BurnsStock(item.GetType()) && SpareUnits(seller, item) > 0)
            {
                stock.Add(item);
            }
        }

        var units = 0;

        for (var i = 0; i < stock.Count; i++)
        {
            units += Deal(seller, buyer, trade, stock[i]);
        }

        return units;
    }

    private static int Deal(SosariaCharacter seller, SosariaCharacter buyer, CraftTrade trade, Item stock)
    {
        var type = stock.GetType();
        var buyerPack = buyer.Backpack;
        var shelf = VendorDeal.CheapestPrice(VendorDeal.VendorsNear(buyer, VendorDeal.CounterRange), [type]);
        var unit = CraftMarketRules.UnitPrice(shelf);
        var units = CraftMarketRules.UnitsToBuy(SpareUnits(seller, stock), buyerPack.GetAmount(type), buyerPack.GetAmount(typeof(Gold)), unit);

        if (units <= 0)
        {
            return 0;
        }

        // The rest of the stack stays in the seller's pack as a stack of its own.
        if (units < stock.Amount && Mobile.LiftItemDupe(stock, units) == null)
        {
            return 0;
        }

        var price = units * unit;

        if (!CharacterTrade.Swap(seller, stock, buyer, price))
        {
            return 0;
        }

        CraftTally.NoteStockDeal(units, price);
        var noun = Appraisal.SplitWords(type.Name);
        Talk.Maybe(seller, TalkCategory.GatherDeliver, TalkOdds.CraftDonePercent, new TalkSlots { Name = buyer.Name, Item = noun });
        Talk.Maybe(buyer, TalkCategory.CraftBuyStock, TalkOdds.CraftDonePercent, new TalkSlots { Name = seller.Name, Item = noun });

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Seller} sold {Units} {Stock} to the {Trade} crafter {Buyer} for {Gold} gold at {Location}",
                seller.Name,
                units,
                type.Name,
                trade.Kind,
                buyer.Name,
                price,
                buyer.Location
            );
        }

        return units;
    }
}
