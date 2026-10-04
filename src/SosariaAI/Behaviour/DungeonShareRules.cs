using System;
using System.Collections.Generic;
using System.Text;
using SosariaAI.Logging;

namespace SosariaAI.Behaviour;

/// <summary>
/// How hard the shard pulls its fighters underground, and how it spreads them over the
/// dungeons. In 1999 a quarter of the fighters online were in a dungeon at any hour; a live
/// run of 24 minutes had 3 in 100 of the people online in a dungeon. The pull is the target
/// share over the share standing underground now: strong while the halls are empty, neutral
/// at the target, soft above it, so the halls fill and stay busy without emptying the towns.
/// A dungeon that already holds a crowd weighs less in the next pick, so no one dungeon
/// takes the population: a run of 78 minutes sent 200 floor entries to Sanctuary and none
/// to nine others. Pure. No world objects.
/// </summary>
public static class DungeonShareRules
{
    /// <summary>The share of live fighters standing in a dungeon the shard aims for. One on the road there does not count.</summary>
    public const double TargetShare = 0.25;

    public const double Neutral = 1.0;
    public const double MinDemand = 0.5;
    /// <summary>
    /// The strongest pull. At three it held: 3 in 100 fighters stood underground at the cap,
    /// while bank and shop jobs outpicked the dungeon fourteen to one among strong blues. The
    /// pull now grows until the halls fill, about to the target over three in a hundred.
    /// </summary>
    public const double MaxDemand = 8.0;

    /// <summary>Below this many live fighters the count says nothing: the pull stays neutral.</summary>
    public const int MinFighters = 10;

    /// <summary>
    /// A healthy fighter standing in a dungeon keeps fighting there: its run wins over the
    /// soft pull home (<see cref="ActionScorer.HomeLeashBoost"/>). One a restart put back
    /// underground (<see cref="DungeonReturnRules"/>) takes up its run again.
    /// </summary>
    public const double InDungeonScoreBoost = ActionScorer.HomeLeashBoost + 1;

    /// <summary>The job roll of a fighter standing in a dungeon leans to the dungeon this much.</summary>
    public const double InDungeonJobBoost = 4.0;

    /// <summary>A dungeon with this many people inside or on their way weighs half as much in the next pick.</summary>
    public const int CrowdHalf = 5;

    public static double Share(int underground, int fighters) =>
        fighters <= 0 ? 0 : (double)Math.Max(0, underground) / fighters;

    /// <summary>The pull on a fighter's dungeon weight: above one while too few stand underground.</summary>
    public static double Demand(int underground, int fighters)
    {
        if (fighters < MinFighters)
        {
            return Neutral;
        }

        var share = Math.Max(Share(underground, fighters), TargetShare / MaxDemand);
        return Math.Clamp(TargetShare / share, MinDemand, MaxDemand);
    }

    /// <summary>A person in a dungeon keeps its run only while it is well and stocked.</summary>
    public static bool StaysUnderground(bool inDungeon, bool healthy, bool suppliesLow) =>
        inDungeon && healthy && !suppliesLow;

    /// <summary>
    /// What a dungeon weighs in a fighter's pick for the people already inside or on their
    /// way: one when empty, a half at <see cref="CrowdHalf"/>, a third at twice that.
    /// </summary>
    public static double SpreadWeight(int visitors) => 1.0 / (1.0 + (double)Math.Max(0, visitors) / CrowdHalf);

    /// <summary>
    /// The census line, busiest dungeon first: "Dungeon visitors: Sanctuary 12, Orc Cave 8
    /// (20 underground of 300 fighters)". Easy to count across a run.
    /// </summary>
    public static string CensusLine(IReadOnlyDictionary<string, int> visitors, int underground, int fighters) =>
        CensusText.Counted(new StringBuilder("Dungeon visitors: "), visitors)
            .Append(" (").Append(underground).Append(" underground of ").Append(fighters).Append(" fighters)").ToString();

    /// <summary>The reason a fighter above ground is on a dungeon job: it holds one now.</summary>
    public const string OnDungeonJob = "on a dungeon job";

    /// <summary>The reason of a red above ground: it delves on its own red runs, not by the dungeon job.</summary>
    public const string RedRuns = "red";

    /// <summary>The reason a fighter above ground has no dungeon run: it can get to no dungeon door at all.</summary>
    public const string NoDoorInReach = "no door in reach";

    /// <summary>The reason a fighter above ground has no dungeon run: the doors it can get to have no hall that fits its power.</summary>
    public const string NoHallFits = "no hall fits its power";

    /// <summary>The reason of a fighter whose goal is no job at all (a red's run, a recovery, a sale).</summary>
    public static string GoalReason(string goalKind) => "goal " + goalKind;

    /// <summary>The reason of a fighter whose dungeon job was open and whose dice landed on another job.</summary>
    public static string RolledReason(string job) => "rolled " + job;

    /// <summary>
    /// Why the fighters above ground are not underground, biggest reason first, with the mean
    /// chance a fighter whose delve is open rolls the dungeon job: "Dungeon misses: no door in
    /// reach 400, rolled bank 90 (dungeon odds 12 % for 300 open of 900 above ground)".
    /// </summary>
    public static string MissLine(IReadOnlyDictionary<string, int> reasons, double oddsSum, int open, int aboveGround)
    {
        var meanOdds = open <= 0 ? 0 : oddsSum / open;
        return CensusText.Counted(new StringBuilder("Dungeon misses: "), reasons)
            .Append(" (dungeon odds ").Append(Math.Round(meanOdds * PercentScale)).Append(" % for ")
            .Append(open).Append(" open of ").Append(aboveGround).Append(" above ground)").ToString();
    }

    private const double PercentScale = 100.0;
}
