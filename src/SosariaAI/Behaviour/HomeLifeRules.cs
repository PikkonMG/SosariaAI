using System;
using SosariaAI.Common;

namespace SosariaAI.Behaviour;

/// <summary>
/// When an owner goes home and how long it stays, the way a 1999 player used a house: back
/// after a trip to put the loot away and take supplies, now and then for a long rest behind a
/// locked door, and to lick its wounds when hurt near home. A friend may drop by while it is
/// in. Pure.
/// </summary>
public static class HomeLifeRules
{
    /// <summary>An owner goes home to put pieces away at most this often.</summary>
    public static readonly TimeSpan StashEvery = TimeSpan.FromMinutes(30);

    /// <summary>An owner goes home for a rest at least this often, with or without anything to put away.</summary>
    public static readonly TimeSpan RestEvery = TimeSpan.FromMinutes(90);

    /// <summary>The shortest stay at home.</summary>
    public static readonly TimeSpan StayMin = TimeSpan.FromMinutes(3);

    /// <summary>The longest planned stay at home.</summary>
    public static readonly TimeSpan StayMax = TimeSpan.FromMinutes(8);

    /// <summary>An owner leaves after this long whatever else holds it: a guest or wounds.</summary>
    public static readonly TimeSpan StayLimit = TimeSpan.FromMinutes(15);

    /// <summary>An owner under this share of its hits goes home to heal when home is near.</summary>
    public const double HurtHits = 0.5;

    /// <summary>An owner at this share of its hits, and not poisoned, is healed enough to go out.</summary>
    public const double HealedHits = 0.9;

    /// <summary>Home is near enough for a hurt owner to go and heal there.</summary>
    public const int HurtHomeTiles = 60;

    /// <summary>A friend this close to the doorstep is let in.</summary>
    public const int GuestDoorTiles = 2;

    /// <summary>
    /// An owner goes home when it has rested from its last stay and carries pieces to put away,
    /// is due a rest, or is hurt with home near.
    /// </summary>
    public static bool Due(
        bool hasHouse, bool hasStash, DateTime lastStay, DateTime now, double hitsFraction, int tilesHome
    ) =>
        hasHouse && TimeRules.Rested(lastStay, now, StashEvery) &&
        (hasStash || TimeRules.Rested(lastStay, now, RestEvery) || Hurt(hitsFraction, tilesHome));

    public static bool Hurt(double hitsFraction, int tilesHome) => hitsFraction < HurtHits && tilesHome <= HurtHomeTiles;

    /// <summary>How long this stay lasts, from a roll in [0, int.MaxValue).</summary>
    public static TimeSpan StayLength(int roll)
    {
        var spread = (StayMax - StayMin).Ticks;
        return StayMin + TimeSpan.FromTicks(spread == 0 ? 0 : Math.Abs(roll) % spread);
    }

    /// <summary>
    /// The owner goes out once its stay ran, it is healed and no guest is inside, or once the
    /// stay limit ran in any case.
    /// </summary>
    public static bool ReadyToLeave(TimeSpan inside, TimeSpan stay, double hitsFraction, bool poisoned, bool guestInside) =>
        inside >= StayLimit ||
        inside >= stay && hitsFraction >= HealedHits && !poisoned && !guestInside;
}
