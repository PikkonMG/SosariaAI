using System;
using System.Collections.Generic;
using System.Globalization;
using SosariaAI.Common;

namespace SosariaAI.Behaviour;

/// <summary>How a town scuffle ended.</summary>
public enum ScuffleResult
{
    OrderWon,
    ChaosWon,
    Draw
}

/// <summary>Where a call to a town street stands: its fighters still gather, it starts, or it lapsed.</summary>
public enum ScuffleCallState
{
    Gathering,
    Starts,
    Lapsed
}

/// <summary>
/// A small Order against Chaos scuffle on a town street, under the guards: blues only, one to
/// three a side, one a town at a time, a few on the shard at once, rare in each town, never near
/// a bank, a healer, a shrine or a moongate, and over when a side gives way or the time runs out.
/// Only the chosen fight: nobody near is drafted in. The first town brawl drew 60 to 70 people
/// into one fight at the Britain bank. Most scuffles are called: a blue of one side in a town
/// calls two or three of its side and of the other to a street away from the bank, and the
/// fight starts with those who came. Chance meetings alone gave three one-on-one scuffles in
/// two hours, two of them the same Yew pair. Pure.
/// </summary>
public static class TownScuffleRules
{
    /// <summary>The fewest fighters of one side: the one who met the foe.</summary>
    public const int MinSide = 1;

    /// <summary>A called scuffle seeks at least this many a side, so most are two on two or three on three.</summary>
    public const int CalledSide = 2;

    /// <summary>A called fighter walks to within this many tiles of the street spot.</summary>
    public const int SpotTiles = 3;

    /// <summary>Road nodes this many round a caller are looked over for a street spot.</summary>
    public const int SpotNodeScan = 96;

    /// <summary>A town whose call lapsed may call again after this long.</summary>
    public static readonly TimeSpan CallRetry = TimeSpan.FromMinutes(5);

    /// <summary>A scuffle is never more than one fighter lopsided: two on one, not three on one.</summary>
    public const int MaxLead = 1;

    /// <summary>A fighter starts a scuffle only with at least this share of its hits.</summary>
    public const double ReadyHits = 0.8;

    /// <summary>At the time limit, sides this close in hits call it even.</summary>
    public const double EvenHitsMargin = 0.05;

    /// <summary>Faction-mates this close to their side's first fighter may be chosen to stand with it.</summary>
    public const int MateRange = 10;

    /// <summary>People this close to the first fighters may watch and call out.</summary>
    public const int WatchRange = 12;

    /// <summary>At most this many watchers call out when a scuffle starts.</summary>
    public const int MaxWatchers = 2;

    /// <summary>The chance, in percent, that one watcher calls out.</summary>
    public const int WatchPercent = 50;

    /// <summary>The shortest time limit a scuffle takes, whatever the file says.</summary>
    public static readonly TimeSpan MinTimeLimit = TimeSpan.FromSeconds(30);

    /// <summary>A scuffle ends inside the skirmish limit, which breaks every fighter off in any case.</summary>
    public static readonly TimeSpan MaxTimeLimit = FactionRules.SkirmishLimit;

    /// <summary>Between two names in a log line.</summary>
    public const string NameSeparator = ", ";

    /// <summary>A blue: no murderer, no red name, not gray. A red Chaos member never scuffles in town.</summary>
    public static bool IsBlue(bool isPk, bool red, bool criminal) => !isPk && !red && !criminal;

    /// <summary>Fit to start: hurt no worse than <see cref="ReadyHits"/>, and rested from its last scuffle.</summary>
    public static bool Ready(double hitsFraction, DateTime lastScuffle, DateTime now, TimeSpan rest) =>
        hitsFraction >= ReadyHits && TimeRules.Rested(lastScuffle, now, rest);

    /// <summary>A fighter stands in the scuffle's ground: under its town's guards and clear of every place of peace.</summary>
    public static bool OnGround(bool underGuards, bool sameTown, bool nearPeace) => underGuards && sameTown && !nearPeace;

    /// <summary>The shard may open a scuffle: the switch is on, fewer than the cap run, and the shard's gap ran out.</summary>
    public static bool ShardOpen(bool enabled, int active, int maxActive, DateTime shardDue, DateTime now) =>
        enabled && active < Math.Max(0, maxActive) && now >= shardDue;

    /// <summary>This town may have a scuffle: none runs there now, and its gap ran out.</summary>
    public static bool TownOpen(bool townBusy, DateTime townDue, DateTime now) => !townBusy && now >= townDue;

    /// <summary>The wait before a town's next scuffle: a whole number of minutes from min to max, both kept.</summary>
    public static TimeSpan TownGap(int roll, int minMinutes, int maxMinutes)
    {
        var low = Math.Max(0, minMinutes);
        var high = Math.Max(low, maxMinutes);
        return TimeSpan.FromMinutes(low + (int)((uint)roll % (uint)(high - low + 1)));
    }

    /// <summary>
    /// The first wait for a town the shard has not seen yet: up to the least gap, so towns do not
    /// all go at once after a boot and the first scuffles come inside the least gap.
    /// </summary>
    public static TimeSpan FirstGap(int roll, int minMinutes) => TownGap(roll, 0, minMinutes);

    /// <summary>
    /// The side sizes to seek: each from its roll, <paramref name="least"/> to
    /// <paramref name="maxSide"/>, and the two together no more than <paramref name="maxFighters"/>,
    /// the bigger side giving way first. A chance meeting seeks <see cref="MinSide"/> or more, a
    /// call <see cref="CalledSide"/> or more.
    /// </summary>
    public static (int Order, int Chaos) SideSizes(int orderRoll, int chaosRoll, int least, int maxSide, int maxFighters)
    {
        var side = Math.Max(MinSide, maxSide);
        var low = Math.Clamp(least, MinSide, side);
        var span = (uint)(side - low + 1);
        var total = Math.Max(MinSide * 2, maxFighters);
        var order = low + (int)((uint)orderRoll % span);
        var chaos = low + (int)((uint)chaosRoll % span);

        while (order + chaos > total)
        {
            if (order >= chaos)
            {
                order--;
            }
            else
            {
                chaos--;
            }
        }

        return (order, chaos);
    }

    /// <summary>The sides as found, the bigger cut to at most <see cref="MaxLead"/> more than the smaller.</summary>
    public static (int Order, int Chaos) Balance(int order, int chaos) =>
        (Math.Min(order, chaos + MaxLead), Math.Min(chaos, order + MaxLead));

    /// <summary>
    /// Where a call stands: it starts when every fighter still called stands at the spot, or at
    /// the end of the gather time with one a side at least; it lapses when a side has nobody
    /// left called, or nobody came by the end.
    /// </summary>
    public static ScuffleCallState CallState(int orderCalled, int chaosCalled, int orderCame, int chaosCame, bool timeUp)
    {
        if (orderCalled < MinSide || chaosCalled < MinSide)
        {
            return ScuffleCallState.Lapsed;
        }

        var everyone = orderCame >= orderCalled && chaosCame >= chaosCalled;
        var both = orderCame >= MinSide && chaosCame >= MinSide;

        return everyone || timeUp && both ? ScuffleCallState.Starts
            : timeUp ? ScuffleCallState.Lapsed
            : ScuffleCallState.Gathering;
    }

    /// <summary>The foe a fighter opens on: round the other side in turn, so the smaller side is doubled on.</summary>
    public static int FoeIndex(int fighterIndex, int foeCount) => foeCount <= 0 ? -1 : fighterIndex % foeCount;

    /// <summary>The time limit from the file, kept between <see cref="MinTimeLimit"/> and <see cref="MaxTimeLimit"/>.</summary>
    public static TimeSpan TimeLimit(int seconds)
    {
        var limit = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return limit < MinTimeLimit ? MinTimeLimit : limit > MaxTimeLimit ? MaxTimeLimit : limit;
    }

    /// <summary>
    /// The end of a scuffle, or null while it goes on. A side with nobody left standing lost; both
    /// gone is a draw. At the time limit the side with the smaller share of its hits gives way,
    /// and two sides within <see cref="EvenHitsMargin"/> of each other call it even.
    /// </summary>
    public static ScuffleResult? Result(int orderStanding, int chaosStanding, bool timeUp, double orderHits, double chaosHits)
    {
        if (orderStanding <= 0 && chaosStanding <= 0)
        {
            return ScuffleResult.Draw;
        }

        if (orderStanding <= 0)
        {
            return ScuffleResult.ChaosWon;
        }

        if (chaosStanding <= 0)
        {
            return ScuffleResult.OrderWon;
        }

        if (!timeUp)
        {
            return null;
        }

        return Math.Abs(orderHits - chaosHits) <= EvenHitsMargin ? ScuffleResult.Draw
            : orderHits > chaosHits ? ScuffleResult.OrderWon
            : ScuffleResult.ChaosWon;
    }

    /// <summary>The one line at a scuffle's start: the town, both sides by name, and where.</summary>
    public static string StartLine(string town, IReadOnlyList<string> order, IReadOnlyList<string> chaos, int x, int y) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Town scuffle in {0}: Order ({1}) against Chaos ({2}) at {3},{4}",
            town,
            string.Join(NameSeparator, order),
            string.Join(NameSeparator, chaos),
            x,
            y
        );

    /// <summary>The one line at a call: the town, who of each side is called, and the street spot.</summary>
    public static string CallLine(string town, IReadOnlyList<string> order, IReadOnlyList<string> chaos, int x, int y) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Town scuffle call in {0}: Order ({1}) and Chaos ({2}) gather at {3},{4}",
            town,
            string.Join(NameSeparator, order),
            string.Join(NameSeparator, chaos),
            x,
            y
        );

    /// <summary>The one line when a call lapses: the town and how many of each side came.</summary>
    public static string LapseLine(string town, int orderCame, int chaosCame) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Town scuffle call in {0} lapsed: {1} of Order and {2} of Chaos came",
            town,
            orderCame,
            chaosCame
        );

    /// <summary>The one line at a scuffle's end: the town, the result, how long it ran and how many fought.</summary>
    public static string EndLine(string town, ScuffleResult result, TimeSpan ran, int fighters) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Town scuffle in {0} ended: {1} after {2}s; {3} fought",
            town,
            ResultWords(result),
            (int)ran.TotalSeconds,
            fighters
        );

    /// <summary>The ten-minute summary line, or null when no call went out and no scuffle began or ended.</summary>
    public static string SummaryLine(int called, int lapsed, int begun, int fighters, int orderWon, int chaosWon, int draws, int minutes)
    {
        var ended = orderWon + chaosWon + draws;

        if (called == 0 && begun == 0 && ended == 0)
        {
            return null;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "Town scuffles in the last {0} minutes: {1} called, {2} lapsed; {3} begun with {4} fighters; {5} ended: Order won {6}, Chaos won {7}, even {8}",
            minutes,
            called,
            lapsed,
            begun,
            fighters,
            ended,
            orderWon,
            chaosWon,
            draws
        );
    }

    public static string ResultWords(ScuffleResult result) =>
        result switch
        {
            ScuffleResult.OrderWon => "Order won",
            ScuffleResult.ChaosWon => "Chaos won",
            _ => "even"
        };
}
