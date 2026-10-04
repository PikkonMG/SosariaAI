using System;
using SosariaAI.Common;

namespace SosariaAI.Combat;

/// <summary>Why a hunt ends, or <see cref="None"/> while it goes on.</summary>
public enum HuntEndReason
{
    None,

    /// <summary>The pack is too heavy to carry more loot.</summary>
    PackFull,

    /// <summary>The bandages, arrows or reagents that keep the fight going ran out.</summary>
    SuppliesLow,

    /// <summary>The planned time is over.</summary>
    TimeUp,

    /// <summary>No prey on the ground for a while.</summary>
    Empty,

    /// <summary>Hurt again and again.</summary>
    Hurt
}

/// <summary>
/// When a hunter leaves the ground: overloaded, out of supplies, hurt too often, nothing left
/// to kill, or the time is up. A hunt that still pays when the time runs out goes on a while,
/// the way a player stays while the spawn keeps coming. Pure.
/// </summary>
public static class HuntEndDecision
{
    /// <summary>A ground with no prey seen for this long is left, unless the hunt set its own look.</summary>
    public static readonly TimeSpan EmptyHuntLimit = TimeSpan.FromMinutes(2);

    /// <summary>A kill this recent when the time runs out means the hunt pays.</summary>
    public static readonly TimeSpan PayingWindow = TimeSpan.FromMinutes(3);

    /// <summary>How much longer a paying hunt stays, each time.</summary>
    public static readonly TimeSpan StayLonger = TimeSpan.FromMinutes(5);

    /// <summary>A paying hunt stays longer at most this many times.</summary>
    public const int MaxStays = 2;

    /// <summary>
    /// True when the supplies ran low during this run: low now, and not low when the run began.
    /// A run that began low fights on with what it has, as a 1999 player hunted the sewers with
    /// a few bandages or none. Fighters start with 20 bandages against a low mark of 10, the
    /// healer stocks 20 for the whole town, and 408 of some 500 dungeon stays ended "after 0
    /// minutes: its supplies ran low": Eira Kipling walked into Britain Sewer and straight out
    /// five times in 90 minutes.
    /// </summary>
    public static bool RanLowOnRun(bool lowAtStart, bool lowNow) => lowNow && !lowAtStart;

    /// <summary>
    /// Why the hunt ends now, or <see cref="HuntEndReason.None"/>. <paramref name="suppliesLow"/>
    /// is supplies that send the hunter home: a hunt passes those that ran low during it
    /// (<see cref="RanLowOnRun"/>).
    /// </summary>
    public static HuntEndReason Reason(
        DateTime now,
        DateTime endsAt,
        bool packFull,
        double hitsFraction,
        double stopBelowHitsFraction,
        int lowHitsCount,
        DateTime lastPreyAt,
        TimeSpan emptyLimit,
        bool suppliesLow
    )
    {
        if (packFull)
        {
            return HuntEndReason.PackFull;
        }

        if (suppliesLow)
        {
            return HuntEndReason.SuppliesLow;
        }

        if (hitsFraction < stopBelowHitsFraction && lowHitsCount >= SosariaCombat.HuntLowHitsLimit)
        {
            return HuntEndReason.Hurt;
        }

        if (TimeRules.Passed(lastPreyAt, now, emptyLimit))
        {
            return HuntEndReason.Empty;
        }

        return endsAt != default && now >= endsAt ? HuntEndReason.TimeUp : HuntEndReason.None;
    }

    /// <summary>The time is up, but a kill came lately: stay a while more.</summary>
    public static bool ShouldStay(HuntEndReason reason, DateTime now, DateTime lastKillAt, int stays) =>
        reason == HuntEndReason.TimeUp &&
        stays < MaxStays &&
        lastKillAt != default &&
        now - lastKillAt <= PayingWindow;

    public static int CountLowHits(int previousCount, bool wasBelow, bool isBelow)
    {
        if (isBelow && !wasBelow)
        {
            return previousCount + 1;
        }

        return previousCount;
    }
}
