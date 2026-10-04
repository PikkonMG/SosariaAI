using System;
using System.Collections.Generic;
using Server.Items;

namespace SosariaAI.Economy;

/// <summary>
/// A ware on a tailor's or weaver's shelf that scissors turn into bandages: its unit price,
/// the bandages one unit makes, and the units on the shelf across all its lines.
/// </summary>
public readonly record struct ClothOffer(Type Ware, int Price, int BandagesEach, int OnShelf);

/// <summary>
/// Bandages from cloth, as a 1999 player made them when the healer's shelf was bare. The
/// engine's healer restocks 20 bandages for a whole town, and hundreds of fighters share them.
/// Scissors cut cloth and uncut cloth into bandages one for one, and a bolt into
/// <see cref="ClothPerBolt"/> cloth (the engine's Cloth, UncutCloth and BoltOfCloth Scissor).
/// The tailor sells cloth and uncut cloth at 2 gold and a bolt at 100, the weaver uncut cloth
/// at 3 and a bolt at 100, and both a pair of scissors at 11. Pure. No world objects.
/// </summary>
public static class ClothBandageRules
{
    /// <summary>Bandages one cloth or uncut cloth makes under the scissors.</summary>
    public const int BandagesPerCloth = 1;

    /// <summary>Cloth one bolt makes under the scissors.</summary>
    public const int ClothPerBolt = 50;

    /// <summary>Bandages one bolt makes, cut to cloth and the cloth cut again.</summary>
    public const int BandagesPerBolt = ClothPerBolt * BandagesPerCloth;

    /// <summary>
    /// The most a ware may cost per bandage still wanted: the healer's own price for a bandage
    /// (the engine's SBHealer, 5 gold). A bolt for the last five bandages costs 20 each.
    /// </summary>
    public const int MostGoldPerBandage = 5;

    /// <summary>
    /// What scissors make bandages of, in the order a tie in price goes: cut cloth first, since
    /// it buys to the bandage, and the bolt last, since it buys fifty at a time.
    /// </summary>
    public static readonly Type[] Wares = [typeof(Cloth), typeof(UncutCloth), typeof(BoltOfCloth)];

    /// <summary>The scissors and every ware they cut: what a bandage maker buys at the tailor's.</summary>
    public static readonly Type[] Makings = [typeof(Scissors), .. Wares];

    /// <summary>Bandages one unit of <paramref name="ware"/> makes.</summary>
    public static int BandagesEach(Type ware) => ware == typeof(BoltOfCloth) ? BandagesPerBolt : BandagesPerCloth;

    /// <summary>The bandages the cloth and the bolts in a pack would make.</summary>
    public static int BandagesIn(int cloth, int bolts) =>
        Math.Max(0, cloth) * BandagesPerCloth + Math.Max(0, bolts) * BandagesPerBolt;

    /// <summary>
    /// The wares to buy for <paramref name="bandagesShort"/> bandages with <paramref name="gold"/>,
    /// as (ware, units). Each step buys the offer that costs the least per bandage still wanted,
    /// so a bolt is bought only when its fifty bandages come cheaper than the cloth that makes
    /// what is still short: at the weaver, 40 bandages come from one bolt for 100 gold rather than
    /// 40 uncut cloth for 120, and 30 from uncut cloth for 90. No step pays more per bandage still
    /// wanted than <see cref="MostGoldPerBandage"/>. A poor buyer buys the cloth it can pay for.
    /// Empty when nothing is short, no shelf stocks a ware, or the gold buys none.
    /// </summary>
    public static List<(Type Type, int Amount)> Plan(int bandagesShort, int gold, IReadOnlyList<ClothOffer> offers)
    {
        var lines = new List<(Type Type, int Amount)>();
        var count = offers?.Count ?? 0;
        var left = new int[count];

        for (var i = 0; i < count; i++)
        {
            left[i] = Math.Max(0, offers[i].OnShelf);
        }

        var still = bandagesShort;
        var purse = gold;

        while (still > 0)
        {
            var pick = Cheapest(offers, left, still, purse);

            if (pick < 0)
            {
                break;
            }

            var offer = offers[pick];
            var units = Math.Min(Math.Min(left[pick], purse / offer.Price), Math.Max(1, still / offer.BandagesEach));
            left[pick] -= units;
            purse -= units * offer.Price;
            still -= units * offer.BandagesEach;
            AddUnits(lines, offer.Ware, units);
        }

        return lines;
    }

    // The offer with goods left and a price the purse meets that costs the least per bandage
    // still wanted, and no more than the healer asks; the first such offer on a tie; -1 when none.
    private static int Cheapest(IReadOnlyList<ClothOffer> offers, int[] left, int still, int purse)
    {
        var pick = -1;
        var best = double.MaxValue;

        for (var i = 0; i < left.Length; i++)
        {
            var offer = offers[i];

            if (left[i] <= 0 || offer.Price <= 0 || offer.BandagesEach <= 0 || offer.Price > purse)
            {
                continue;
            }

            var perBandage = (double)offer.Price / Math.Min(offer.BandagesEach, still);

            if (perBandage <= MostGoldPerBandage && perBandage < best)
            {
                best = perBandage;
                pick = i;
            }
        }

        return pick;
    }

    private static void AddUnits(List<(Type Type, int Amount)> lines, Type ware, int units)
    {
        var at = lines.FindIndex(line => line.Type == ware);

        if (at < 0)
        {
            lines.Add((ware, units));
        }
        else
        {
            lines[at] = (ware, lines[at].Amount + units);
        }
    }
}
