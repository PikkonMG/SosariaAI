using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// GM gear a fighter gets from a crafter at its station (<see cref="CraftShopBoard"/>): the
/// pickup of its own ready order (<see cref="Pickup"/>), else a piece of shop stock that betters what it wears, then an
/// order for the GM version of a plain piece it wears when no crafter in reach has one. The purse
/// counts one withdraw at the bank, which the trip makes on the way. A red buys none: it never
/// stands at a guarded forge. World thread only.
/// </summary>
public static class CraftedGear
{
    /// <summary>How far, in tiles, a fighter walks to a crafter's station.</summary>
    public const int TownReach = 200;

    private static readonly GearSlot[] GearSlotsBodyFirst =
        [GearSlot.Chest, GearSlot.Legs, GearSlot.Arms, GearSlot.Helm, GearSlot.Gloves, GearSlot.Neck];

    /// <summary>The pickup of the buyer's own ready order, or null. It comes before any other buy: the deposit is paid.</summary>
    public static GearOffer? Pickup(SosariaCharacter buyer) =>
        MayBuy(buyer) ? PickupFor(buyer, CraftShopBoard.Near(buyer.Map, buyer.Location, TownReach), Purse(buyer)) : null;

    /// <summary>A piece of shop stock that betters the buyer, else an order for one, or null.</summary>
    public static GearOffer? OfferFor(SosariaCharacter buyer)
    {
        if (!MayBuy(buyer))
        {
            return null;
        }

        var purse = Purse(buyer);
        var crafters = CraftShopBoard.Near(buyer.Map, buyer.Location, TownReach);

        return StockFor(buyer, crafters, purse) ?? OrderFor(buyer, crafters, purse);
    }

    private static bool MayBuy(SosariaCharacter buyer) => buyer != null && !PkRules.IsRed(buyer.Kills) && TradeMarket.MayShop(buyer);

    private static int Purse(SosariaCharacter buyer) => TradeHandOff.PurseAtBank(buyer) - SosariaSettings.GearGoldReserve;

    /// <summary>
    /// True when <paramref name="piece"/> betters what the buyer wears in its place: armor its
    /// class buys in the same slot, a weapon of the skill it fights with, or a shield it carries.
    /// </summary>
    public static bool Betters(SosariaCharacter buyer, Item piece)
    {
        if (piece == null || !TradeDemandRules.Wants(TradeMarket.AppetiteOf(buyer), Appraisal.RowOf(piece)))
        {
            return false;
        }

        return piece switch
        {
            BaseShield => buyer.FindItemOnLayer(Layer.TwoHanded) is BaseShield worn && GearScore.RankOf(piece) > GearScore.RankOf(worn),
            BaseArmor armor => SlotOf(armor) is { } slot && GearScore.RankOf(piece) > GearScore.SlotRank(buyer, slot),
            BaseWeapon weapon => buyer.Weapon is BaseWeapon held && held.Skill == weapon.Skill &&
                                 GearScore.RankOf(piece) > GearScore.WeaponRank(buyer),
            _ => false
        };
    }

    private static GearOffer? PickupFor(SosariaCharacter buyer, List<SosariaCharacter> crafters, int purse)
    {
        foreach (var crafter in crafters)
        {
            if (crafter.OrderFor(buyer.Serial.Value) is { Ready: true } order && purse >= order.Rest)
            {
                return Offer(GearBuyKind.Pickup, order.ItemTypes[0], order.Rest, crafter);
            }
        }

        return null;
    }

    // The piece that betters the buyer most, the cheaper of equals.
    private static GearOffer? StockFor(SosariaCharacter buyer, List<SosariaCharacter> crafters, int purse)
    {
        GearOffer? pick = null;
        var bestGain = 0;

        foreach (var crafter in crafters)
        {
            if (crafter == buyer || TradeSessions.IsBusy(crafter))
            {
                continue;
            }

            foreach (var piece in ShopStock.InPack(crafter))
            {
                var asking = ShopStock.AskingOf(piece);

                if (!Betters(buyer, piece) || !TradeMarket.MayMeet(buyer, piece, asking, purse))
                {
                    continue;
                }

                var gain = GearScore.RankOf(piece);

                if (gain > bestGain || gain == bestGain && pick is { } held && asking < held.Price)
                {
                    bestGain = gain;
                    pick = Offer(GearBuyKind.Crafted, piece.GetType().Name, asking, crafter);
                }
            }
        }

        return pick;
    }

    // An order for the GM version of a plain piece the buyer wears, at the first crafter that takes it.
    private static GearOffer? OrderFor(SosariaCharacter buyer, List<SosariaCharacter> crafters, int purse)
    {
        foreach (var crafter in crafters)
        {
            if (crafter.OrderFor(buyer.Serial.Value) != null)
            {
                return null;
            }
        }

        foreach (var worn in PlainWorn(buyer))
        {
            var type = worn.GetType().Name;

            foreach (var crafter in crafters)
            {
                var price = OrderDesk.PriceFor(crafter, type);

                // The buyer weighs the price against the GM piece it orders, not the plain one it wears.
                if (price > 0 && purse >= CraftOrderRules.DepositOf(price) &&
                    TradeMarket.MayMeet(buyer, Appraisal.ClaimOf(worn) with { Exceptional = true }, price, purse))
                {
                    return Offer(GearBuyKind.Order, type, CraftOrderRules.DepositOf(price), crafter);
                }
            }
        }

        return null;
    }

    // The plain armor and weapon the buyer wears, body first.
    private static List<Item> PlainWorn(SosariaCharacter buyer)
    {
        var plain = new List<Item>();

        foreach (var slot in GearSlotsBodyFirst)
        {
            if (buyer.FindItemOnLayer(GearScore.LayerOf(slot)) is BaseArmor armor && IsPlain(armor))
            {
                plain.Add(armor);
            }
        }

        // Bare hands are a weapon to the engine, but not one in hand.
        if (buyer.Weapon is BaseWeapon weapon && weapon.Parent == buyer && IsPlain(weapon))
        {
            plain.Add(weapon);
        }

        return plain;
    }

    private static bool IsPlain(Item item) => !Appraisal.IsExceptional(item) && GearScore.MagicLevel(item) == GearScore.NoMagicLevel;

    private static GearSlot? SlotOf(BaseArmor armor) =>
        Appraisal.PieceOf(armor) switch
        {
            ArmorPiece.Chest => GearSlot.Chest,
            ArmorPiece.Legs => GearSlot.Legs,
            ArmorPiece.Arms => GearSlot.Arms,
            ArmorPiece.Gloves => GearSlot.Gloves,
            ArmorPiece.Gorget => GearSlot.Neck,
            ArmorPiece.Helm => GearSlot.Helm,
            _ => null
        };

    private static GearOffer Offer(GearBuyKind kind, string type, int price, SosariaCharacter crafter) =>
        new(type, null, price, GearPlan.NoGearScore, null, null, kind, null, Upgrade: kind != GearBuyKind.Order, crafter.Serial.Value);
}
