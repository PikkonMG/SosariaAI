using System;
using Server;
using Server.Items;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>A treasure map someone holds up for sale, with its seller and the asking price.</summary>
public readonly record struct MapOffer(SosariaCharacter Seller, TreasureMap Map, int Asking);

/// <summary>
/// The treasure map floor on top of the bank crowd's own offers. A seller holds a map or an
/// SOS up as an ordinary hawker offer, so a person can ask the price and haggle through the
/// trade talk; a character buyer finds the nearest map it can really work and pay for and
/// buys it through <see cref="TradeDeal"/>. World thread only.
/// </summary>
public static class TreasureMarket
{
    /// <summary>Holds the goods up as the seller's offer, priced from the market table. Returns the offer.</summary>
    public static HawkerOffer Offer(SosariaCharacter seller, Item goods, int roll)
    {
        var offer = new HawkerOffer(
            goods.Serial,
            Appraisal.Value(goods, Math.Abs(roll % Appraisal.PercentScale)),
            TreasureLines.NounOf(goods)
        );
        BankCrowd.SetHawkerOffer(seller, offer);
        return offer;
    }

    /// <summary>Takes the goods down, unless the seller has since held up something else.</summary>
    public static void Withdraw(SosariaCharacter seller, Item goods)
    {
        if (seller != null && goods != null && BankCrowd.TryGetHawkerOffer(seller, out var offer) &&
            offer.Item == goods.Serial)
        {
            BankCrowd.ClearHawkerOffer(seller);
        }
    }

    /// <summary>
    /// The nearest map held up within <paramref name="range"/> that the buyer can finish and
    /// pay the seller's floor for, or null.
    /// </summary>
    public static MapOffer? FindFor(SosariaCharacter buyer, int range)
    {
        if (!TradeMarket.MayShop(buyer))
        {
            return null;
        }

        var purse = TradeHandOff.Purse(buyer);
        var found = TradeMarket.NearestOffer(
            buyer,
            range,
            (seller, goods, offer) => seller.Alive && goods is TreasureMap map && TreasureMaps.MayHunt(buyer, map) &&
                                      TreasureMarketRules.Affords(purse, offer.Asking)
        );

        return found is { } pick ? new MapOffer(pick.Hawker, (TreasureMap)pick.Goods, pick.Offer.Asking) : null;
    }
}
