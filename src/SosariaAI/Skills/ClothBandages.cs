using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Bandages from cloth when the healer's shelf is bare: cloth bought at a tailor or a weaver,
/// with one pair of scissors for a person that has none, and cut with the engine's own
/// <see cref="IScissorable.Scissor"/>, bolts to cloth and cloth to bandages, as a player cut
/// them. The healer restocks 20 bandages for a whole town, and 408 of some 500 dungeon stays
/// in one evening ended at once on low supplies (<see cref="ClothBandageRules"/>).
/// </summary>
public static class ClothBandages
{
    /// <summary>The engine's scissors sound, which its scissors target plays on a cut.</summary>
    public const int ScissorSound = 0x248;

    /// <summary>The shops that sell cloth and scissors: the tailor and the weaver.</summary>
    public static readonly string[] Shops = [ShopFinder.TailorToken, ShopFinder.WeaverToken];

    /// <summary>A bolt is cut to cloth first, so its cloth is cut in the same go.</summary>
    private static readonly Type[] CutOrder = [typeof(BoltOfCloth), typeof(Cloth), typeof(UncutCloth)];

    private static readonly Type[] ScissorsTypes = [typeof(Scissors)];

    private static readonly ILogger logger = SosariaLog.For(typeof(ClothBandages));

    /// <summary>
    /// A person whose station trade burns cloth (a tailor) neither buys cloth for bandages nor
    /// cuts any: the cloth in its pack is its stock, and a cut would eat the day's work.
    /// </summary>
    public static bool WorksCloth(SosariaCharacter character) =>
        CraftMarket.TradeOf(character) is { } trade &&
        (trade.BurnsStock(typeof(Cloth)) || trade.BurnsStock(typeof(UncutCloth)));

    /// <summary>True when <paramref name="type"/> is cloth a bandage maker buys or the scissors that cut it.</summary>
    public static bool IsMaking(Type type) => Array.IndexOf(ClothBandageRules.Makings, type) >= 0;

    /// <summary>
    /// Fills <paramref name="wanted"/> with the cloth that makes the bandages of
    /// <paramref name="need"/>, past the cloth the pack already holds, and with a pair of
    /// scissors when the pack has none, at the nearest stocked tailor or weaver in reach. The
    /// cloth is planned on that shop's shelf with the pack gold the scissors leave
    /// (<see cref="ClothBandageRules.Plan"/>). Returns the shop, or null when none in reach
    /// stocks cloth, scissors are wanted and the shop has none, or there is nothing to buy.
    /// </summary>
    public static StockedShop? Plan(SosariaCharacter character, SupplyNeed need, List<(Type Type, int Amount)> wanted)
    {
        var pack = character?.Backpack;

        if (pack == null || WorksCloth(character) ||
            ShopFinder.NearestStocked(character, Shops, ClothBandageRules.Wares) is not { } shop)
        {
            return null;
        }

        var purse = pack.GetAmount(typeof(Gold));

        if (pack.FindItemByType<Scissors>() == null)
        {
            if (VendorDeal.ShelfLine(shop.Vendor, ScissorsTypes) is not { } scissors)
            {
                return null;
            }

            wanted.Add((typeof(Scissors), 1));
            purse -= Math.Max(1, scissors.Price);
        }

        var inPack = ClothBandageRules.BandagesIn(
            pack.GetAmount(typeof(Cloth)) + pack.GetAmount(typeof(UncutCloth)),
            pack.GetAmount(typeof(BoltOfCloth))
        );
        wanted.AddRange(ClothBandageRules.Plan(need.Shortfall - inPack, purse, Offers(shop.Vendor)));
        return wanted.Count > 0 ? shop : null;
    }

    /// <summary>
    /// Cuts every bolt, cloth and uncut cloth in the pack into bandages with the scissors the
    /// pack holds, through the engine's own scissor path. Bandages past the target are spare
    /// stock the bank floor buys (<see cref="SupplyMarket"/>). Returns the bandages made.
    /// </summary>
    public static int Cut(SosariaCharacter character)
    {
        var pack = character?.Backpack;

        if (WorksCloth(character) || pack?.FindItemByType<Scissors>() is not { } scissors)
        {
            return 0;
        }

        var before = pack.GetAmount(typeof(Bandage));

        for (var t = 0; t < CutOrder.Length; t++)
        {
            foreach (var ware in StacksOf(pack, CutOrder[t]))
            {
                if (ware is IScissorable cuttable && ware.Movable && Scissors.CanScissor(character, cuttable) &&
                    cuttable.Scissor(character, scissors))
                {
                    character.PlaySound(ScissorSound);
                }
            }
        }

        var made = Math.Max(0, pack.GetAmount(typeof(Bandage)) - before);

        if (made > 0 && SosariaSettings.LogActivity)
        {
            logger.Information("{Name} cut {Count} cloth into bandages", character.Name, made);
        }

        return made;
    }

    // The shelf's cloth wares, each with the units on all its lines: a weaver keeps its uncut
    // cloth and its bolts on four lines of 20 each.
    private static List<ClothOffer> Offers(BaseVendor vendor)
    {
        var offers = new List<ClothOffer>();

        for (var w = 0; w < ClothBandageRules.Wares.Length; w++)
        {
            var ware = ClothBandageRules.Wares[w];

            if (VendorDeal.ShelfLine(vendor, [ware]) is not { } first)
            {
                continue;
            }

            var units = 0;

            foreach (var info in vendor.GetBuyInfo())
            {
                if (info is GenericBuyInfo { Amount: > 0 } line && line.Type == ware)
                {
                    units += line.Amount;
                }
            }

            offers.Add(new ClothOffer(ware, Math.Max(1, first.Price), ClothBandageRules.BandagesEach(ware), units));
        }

        return offers;
    }

    // Collected first: a cut deletes the stack the scan walks and drops new ones in the pack.
    private static List<Item> StacksOf(Container pack, Type type)
    {
        var stacks = new List<Item>();

        foreach (var item in pack.FindItemsByType(type))
        {
            stacks.Add(item);
        }

        return stacks;
    }
}
