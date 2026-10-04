using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Economy;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>A cut of one supply a seller on the floor spares for a buyer short of it.</summary>
public readonly record struct SupplyOffer(SosariaCharacter Seller, Item Stack, int Units);

/// <summary>
/// Supplies between players. The shops ran dry: of one run's shopping trips, 36 ended at a shelf
/// with no bandages, black pearl or bolts left, and nobody on the bank floor sold any, because a
/// supply a person burns is never for sale (<see cref="HawkerGoods.IsSupply"/>). A person on the
/// floor sells what it carries past its own restock target (<see cref="SupplyRules.Spare"/>) to a
/// buyer short of it, and no more than that buyer's shortfall, so neither side ever sells back
/// or draws back what changed hands. World thread only.
/// </summary>
public static class SupplyMarket
{
    private static readonly Dictionary<Type, List<SupplyKind>> KindsByType = new();

    /// <summary>The supply kinds a type counts toward: black pearl toward reagents and travel reagents.</summary>
    public static IReadOnlyList<SupplyKind> KindsOf(Type type)
    {
        if (type == null)
        {
            return [];
        }

        if (KindsByType.TryGetValue(type, out var kinds))
        {
            return kinds;
        }

        kinds = [];

        foreach (var kind in Enum.GetValues<SupplyKind>())
        {
            if (Contains(SupplyCheck.TypesOf(kind), type))
            {
                kinds.Add(kind);
            }
        }

        KindsByType[type] = kinds;
        return kinds;
    }

    /// <summary>Units of <paramref name="type"/> in the person's pack past its own keep.</summary>
    public static int SpareUnits(SosariaCharacter person, Type type) =>
        person?.Backpack == null
            ? 0
            : SupplyRules.Spare(person.Backpack.GetAmount(type), SupplyRules.KeepOf(SupplyCheck.ProfileOf(person), KindsOf(type)));

    /// <summary>Every supply line the person is short of, each type with the units that bring it to its target.</summary>
    public static List<(Type Type, int Amount)> Wanted(SosariaCharacter person)
    {
        var lines = new List<(Type, int)>();

        foreach (var need in SupplyRules.Shortfalls(SupplyCheck.ProfileOf(person)))
        {
            lines.AddRange(SupplyCheck.BuyLines(person.Backpack, need));
        }

        return lines;
    }

    /// <summary>Units the person is short of across every supply it burns.</summary>
    public static int ShortUnits(SosariaCharacter person)
    {
        var units = 0;

        foreach (var need in SupplyRules.Shortfalls(SupplyCheck.ProfileOf(person)))
        {
            units += need.Shortfall;
        }

        return units;
    }

    /// <summary>
    /// The nearest person idling on the floor within <paramref name="reach"/> who spares a supply
    /// the buyer is short of, at a price the buyer could meet, or null.
    /// </summary>
    public static SupplyOffer? SellerFor(SosariaCharacter buyer, int reach, int roll)
    {
        if (buyer?.Backpack == null || !TradeMarket.MayShop(buyer) || Wanted(buyer) is not { Count: > 0 } wanted)
        {
            return null;
        }

        var purse = TradeHandOff.Purse(buyer);
        SupplyOffer? pick = null;
        var best = double.MaxValue;

        foreach (var mobile in buyer.GetMobilesInRange(reach))
        {
            if (mobile is not SosariaCharacter seller || seller == buyer || !TradeMarket.MayAnswer(seller) ||
                !People.Perceives(seller, buyer) || TradeMarket.Refused(buyer, seller) || TradeMarket.Refused(seller, buyer))
            {
                continue;
            }

            var distance = buyer.GetDistanceToSqrt(seller);

            if (distance < best && OfferFrom(seller, buyer, wanted, purse, roll) is { } offer)
            {
                best = distance;
                pick = offer;
            }
        }

        return pick;
    }

    /// <summary>
    /// Cuts the offered units off the seller's stack into a stack of their own, the goods of the
    /// deal; the rest stays in the pack. Null when the stack left the pack.
    /// </summary>
    public static Item CutLot(SupplyOffer offer)
    {
        var stack = offer.Stack;

        if (stack is not { Deleted: false } || !stack.IsChildOf(offer.Seller.Backpack) || offer.Units <= 0)
        {
            return null;
        }

        return offer.Units < stack.Amount && Mobile.LiftItemDupe(stack, offer.Units) == null ? null : stack;
    }

    /// <summary>The asking price of <paramref name="units"/> of the stack's kind at <paramref name="roll"/>.</summary>
    public static int Asking(Item stack, int units, int roll) =>
        new GoodsClaim(Appraisal.RowOf(stack), units, false, Appraisal.NoMagic).Value(Math.Abs(roll) % Appraisal.PercentScale);

    // The first line the buyer wants that this seller spares, cut to the buyer's shortfall.
    private static SupplyOffer? OfferFrom(SosariaCharacter seller, SosariaCharacter buyer, List<(Type Type, int Amount)> wanted, int purse, int roll)
    {
        foreach (var (type, amount) in wanted)
        {
            if (seller.Backpack.FindItemByType(type) is not { } stack)
            {
                continue;
            }

            var units = Math.Min(Math.Min(amount, stack.Amount), SpareUnits(seller, type));

            if (units > 0 &&
                TradeMarket.MayMeet(buyer, new GoodsClaim(Appraisal.RowOf(stack), units, false, Appraisal.NoMagic), Asking(stack, units, roll), purse))
            {
                return new SupplyOffer(seller, stack, units);
            }
        }

        return null;
    }

    private static bool Contains(IReadOnlyList<Type> types, Type type)
    {
        for (var i = 0; i < types.Count; i++)
        {
            if (types[i] == type)
            {
                return true;
            }
        }

        return false;
    }
}
