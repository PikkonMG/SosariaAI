using System;
using System.Collections.Generic;

namespace SosariaAI.Economy;

/// <summary>
/// A crafter's shop: its own exceptional pieces, held for people and never sold over an NPC
/// counter. It carries up to <see cref="PackStockCap"/> pieces and keeps the rest in its bank,
/// and takes more out when the pack runs low. Each piece has one asking price, set by its
/// serial, so the crafter names the same number every time. At its station it shouts its best
/// piece every few minutes and lists its best few when asked. Pure.
/// </summary>
public static class CraftShopRules
{
    public const int PackStockCap = 8;

    /// <summary>A pack with fewer pieces than this takes more out of the bank.</summary>
    public const int RestockBelow = 3;

    /// <summary>The most pieces a crafter names when asked what it has.</summary>
    public const int ListedPieces = 4;

    public const int MinShoutGapSeconds = 120;
    public const int MaxShoutGapSeconds = 240;

    /// <summary>Out of a hundred shouts, how many add that the crafter takes orders.</summary>
    public const int OrderTalkPercent = 50;

    /// <summary>How often the station shop looks over its stock and its orders.</summary>
    public static readonly TimeSpan RefreshGap = TimeSpan.FromSeconds(10);

    private const int InclusiveSpanPad = 1;

    public static int AskingRoll(uint serial) => (int)(serial % Appraisal.PercentScale);

    public static bool HasRoom(int packStock) => packStock < PackStockCap;

    /// <summary>What shop stock adds to a try: the exceptional chance of the piece's value.</summary>
    public static int ShopShare(double exceptionalChance, int exceptionalValue) =>
        (int)Math.Round(Math.Clamp(exceptionalChance, 0, 1) * Math.Max(0, exceptionalValue));

    public static int PiecesToTake(int packStock, int bankStock) =>
        packStock >= RestockBelow ? 0 : Math.Min(Math.Max(0, bankStock), PackStockCap - Math.Max(0, packStock));

    /// <summary>The places in <paramref name="values"/> worth the most, best first, at most <paramref name="count"/>.</summary>
    public static List<int> BestFirst(IReadOnlyList<int> values, int count)
    {
        var order = new List<int>();

        for (var i = 0; i < (values?.Count ?? 0); i++)
        {
            order.Add(i);
        }

        order.Sort((a, b) => values[b] != values[a] ? values[b].CompareTo(values[a]) : a.CompareTo(b));
        return order.GetRange(0, Math.Clamp(count, 0, order.Count));
    }

    /// <summary>The places of the pieces that go to the bank: all but the <see cref="PackStockCap"/> worth the most.</summary>
    public static List<int> PiecesToBank(IReadOnlyList<int> values)
    {
        var kept = new HashSet<int>(BestFirst(values, PackStockCap));
        var banked = new List<int>();

        for (var i = 0; i < (values?.Count ?? 0); i++)
        {
            if (!kept.Contains(i))
            {
                banked.Add(i);
            }
        }

        return banked;
    }

    public static TimeSpan ShoutGap(int roll) =>
        TimeSpan.FromSeconds(MinShoutGapSeconds + Math.Abs(roll % (MaxShoutGapSeconds - MinShoutGapSeconds + InclusiveSpanPad)));
}
