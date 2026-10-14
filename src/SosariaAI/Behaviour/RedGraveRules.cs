using System;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// Where the reds fell lately, and what a red makes of it, the 1999 way: a red that died did
/// not walk back into the pack that killed it. It kept off the ground where it or a gang mate
/// fell for an hour, and every red kept off ground where the blues had put down two reds
/// lately: an anti-PK guild held it. Live, 68 reds died 1,990 times in 9.4 hours, each about
/// every 19 minutes, and a gang kept its hot spot for the whole camp slot whatever fell there.
/// Pure.
/// </summary>
public static class RedGraveRules
{
    /// <summary>A red fell at a place when it fell this close to it: a hot spot's patrol roads and camps lie inside it.</summary>
    public const int GraveTiles = 60;

    /// <summary>This many reds down at a place within <see cref="HeldFor"/>: the blues hold it.</summary>
    public const int HeldFalls = 2;

    /// <summary>Blues hold a place this long after reds fell there: the half-life of the shard's danger heat.</summary>
    public static readonly TimeSpan HeldFor = TimeSpan.FromMinutes(DangerMap.HalfLifeMinutes);

    /// <summary>A red keeps off the place where it or a gang mate fell this long.</summary>
    public static readonly TimeSpan OwnGraveFor = TimeSpan.FromMinutes(60);

    /// <summary>A fall is remembered for the longer of the two windows.</summary>
    public static readonly TimeSpan Remembered = OwnGraveFor > HeldFor ? OwnGraveFor : HeldFor;

    /// <summary>A fall at <paramref name="fellAt"/> still counts at <paramref name="now"/> inside <paramref name="window"/>.</summary>
    public static bool Fresh(DateTime fellAt, DateTime now, TimeSpan window) => fellAt <= now && now - fellAt < window;

    /// <summary>
    /// A red keeps off a place where it or a gang mate fell lately (<paramref name="ownSideFell"/>),
    /// and off one the blues hold: <see cref="HeldFalls"/> reds or more down there lately.
    /// </summary>
    public static bool Shuns(bool ownSideFell, int redsFell) => ownSideFell || redsFell >= HeldFalls;
}
