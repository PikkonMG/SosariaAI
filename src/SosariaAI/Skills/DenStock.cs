using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// What the shops of Buccaneer's Den have on their shelves, found the way a red's shopping
/// trip finds a shop (<see cref="ShopFinder"/>), from the Den's square. A fixed list once said
/// the Den sold only arrows and bolts, though its healer sells bandages. A red that turned
/// home for a supply no Den shop sold rode out and back 292 times in one night, so a supply
/// counts only when every type the red is short of is on a Den shelf. The low check runs for
/// every red many times a minute, so each answer holds for <see cref="Freshness"/>.
/// </summary>
public static class DenStock
{
    /// <summary>How long one look at a Den shelf answers for.</summary>
    public static readonly TimeSpan Freshness = TimeSpan.FromMinutes(1);

    private static readonly Dictionary<(SupplyKind Kind, Type Type), (bool Sells, int Price, DateTime Until)> Looks = [];

    /// <summary>True when a Den shop has every type on <paramref name="lines"/> of the supply on its shelf.</summary>
    public static bool SellsAll(SupplyKind kind, IReadOnlyList<(Type Type, int Amount)> lines)
    {
        var tokens = SupplyRules.ShopTokens(kind);

        if (tokens.Count == 0 || lines == null || lines.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            if (!Look(kind, lines[i].Type, tokens).Sells)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// True when a Den shop has every type on <paramref name="lines"/> (<see cref="SellsAll"/>)
    /// and the purse pays for one unit of the cheapest there (<see cref="VendorBuySkill.PaysForOne(int, int, int)"/>).
    /// A broke red counted the healer's bandages as a refill it never bought: it could not ride
    /// out, could not shop, and sat in the Den inn; 326 runs ended "the reagents or bandages ran
    /// short" in six hours.
    /// </summary>
    public static bool PaysAtTheDen(SupplyKind kind, IReadOnlyList<(Type Type, int Amount)> lines, int packGold, int bankGold)
    {
        if (!SellsAll(kind, lines))
        {
            return false;
        }

        var tokens = SupplyRules.ShopTokens(kind);
        var cheapest = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            var price = Look(kind, lines[i].Type, tokens).Price;

            if (price > 0 && (cheapest == 0 || price < cheapest))
            {
                cheapest = price;
            }
        }

        return VendorBuySkill.PaysForOne(cheapest, packGold, bankGold);
    }

    private static (bool Sells, int Price) Look(SupplyKind kind, Type type, IReadOnlyList<string> tokens)
    {
        var now = Core.Now;

        if (Looks.TryGetValue((kind, type), out var look) && now < look.Until)
        {
            return (look.Sells, look.Price);
        }

        var shop = ShopFinder.NearestStocked(FacetNames.Felucca, Map.Felucca, PkRules.BucsDenHaven, red: true, tokens, [type]);
        var price = shop is { } stocked ? VendorDeal.CheapestPrice([stocked.Vendor], [type]) : 0;
        Looks[(kind, type)] = (shop != null, price, now + Freshness);
        return (shop != null, price);
    }
}
