using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Economy;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// A crafter's shop stock: the exceptional pieces of its own trade, less the pieces held for an
/// order and its own kit. They never go over an NPC counter. People buy them at the market table's price
/// (<see cref="AskingOf"/>). Pieces past <see cref="CraftShopRules.PackStockCap"/> wait in the
/// bank (<see cref="Balance"/>). World thread only.
/// </summary>
public static class ShopStock
{
    public static bool IsStock(SosariaCharacter crafter, Item item, CraftTrade trade) =>
        trade != null && crafter != null && item is { Deleted: false, Movable: true } && Appraisal.IsExceptional(item) &&
        HawkerGoods.MadeToSell(trade, item) && !crafter.IsOrderPiece(item) && !WorkerTools.IsKitItem(crafter, item);

    public static List<Item> InPack(SosariaCharacter crafter) => Pieces(crafter, crafter?.Backpack);

    public static List<Item> InBank(SosariaCharacter crafter) => Pieces(crafter, crafter?.BankBox);

    /// <summary>The one asking price of a piece: the market table at the roll its serial sets.</summary>
    public static int AskingOf(Item piece) => Appraisal.Value(piece, CraftShopRules.AskingRoll(piece.Serial.Value));

    public static bool HasRoom(SosariaCharacter crafter) => CraftShopRules.HasRoom(InPack(crafter).Count);

    /// <summary>The pieces in the pack worth the most, best first, at most <paramref name="count"/>.</summary>
    public static List<Item> Best(SosariaCharacter crafter, int count)
    {
        var pieces = InPack(crafter);
        var best = new List<Item>();

        foreach (var index in CraftShopRules.BestFirst(pieces.ConvertAll(AskingOf), count))
        {
            best.Add(pieces[index]);
        }

        return best;
    }

    /// <summary>The first piece in the pack that is what <paramref name="claim"/> names, or null.</summary>
    public static Item Named(SosariaCharacter crafter, GoodsClaim claim)
    {
        if (claim.Row == null)
        {
            return null;
        }

        foreach (var piece in InPack(crafter))
        {
            if (claim.Matches(piece))
            {
                return piece;
            }
        }

        return null;
    }

    /// <summary>
    /// At the bank counter: pieces past the pack cap go into the box, the cheapest first, and a
    /// pack running low takes the best pieces back out. Returns how many moved each way.
    /// </summary>
    public static (int Banked, int Taken) Balance(SosariaCharacter crafter)
    {
        var bank = crafter?.BankBox;

        if (bank == null || crafter.Backpack == null)
        {
            return (0, 0);
        }

        var pack = InPack(crafter);
        var banked = 0;

        foreach (var index in CraftShopRules.PiecesToBank(pack.ConvertAll(AskingOf)))
        {
            if (bank.TryDropItem(crafter, pack[index], false))
            {
                banked++;
            }
        }

        var stored = InBank(crafter);
        var taken = 0;
        var room = CraftShopRules.PiecesToTake(pack.Count - banked, stored.Count);

        foreach (var index in CraftShopRules.BestFirst(stored.ConvertAll(AskingOf), room))
        {
            crafter.Backpack.DropItem(stored[index]);
            taken++;
        }

        return (banked, taken);
    }

    private static List<Item> Pieces(SosariaCharacter crafter, Container box)
    {
        var pieces = new List<Item>();
        var trade = CraftMarket.TradeOf(crafter);

        if (box == null || trade == null)
        {
            return pieces;
        }

        foreach (var item in box.Items)
        {
            if (IsStock(crafter, item, trade))
            {
                pieces.Add(item);
            }
        }

        return pieces;
    }
}
