using System;
using SosariaAI.Common;

namespace SosariaAI.Behaviour;

/// <summary>
/// How treasure maps change hands: word of a map for sale carries a town quarter, a buyer
/// only walks over for one it can pay the lowest floor of, and a seller or a browser gives the
/// floor a fixed time. Pure.
/// </summary>
public static class TreasureMarketRules
{
    /// <summary>A map held up for sale is heard of this far away: a pier, a bank and the streets between.</summary>
    public const int NoticeRange = 40;

    /// <summary>Seconds a seller holds a map up at the bank before it gives up for now.</summary>
    public const int SellDwellSeconds = 120;

    /// <summary>Seconds a buyer looks over the bank floor for a map.</summary>
    public const int BrowseDwellSeconds = 60;

    /// <summary>Seconds between two looks over the floor while browsing.</summary>
    public const int BrowseGapSeconds = 5;

    public static readonly TimeSpan SellDwell = TimeSpan.FromSeconds(SellDwellSeconds);
    public static readonly TimeSpan BrowseDwell = TimeSpan.FromSeconds(BrowseDwellSeconds);
    public static readonly TimeSpan BrowseGap = TimeSpan.FromSeconds(BrowseGapSeconds);

    /// <summary>The purse meets the softest seller's floor under this asking price.</summary>
    public static bool Affords(int purse, int asking) =>
        asking > 0 && purse >= TradeMarket.LowestFloor(asking);

    public static bool DwellOver(DateTime now, DateTime since, TimeSpan dwell) => TimeRules.Passed(since, now, dwell);
}
