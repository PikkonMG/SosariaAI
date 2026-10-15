namespace SosariaAI.Combat;

/// <summary>
/// A group may be too much to take head on, but one foe can still be pulled. A player does
/// not walk into the middle of that: it gets the foe's attention and backs off the group, and
/// the one that follows is fought on its own while the rest lose interest. The draw is short,
/// and it ends early once nothing but the target is near or the group is well behind.
/// </summary>
public static class FightPullRules
{
    public const int IsolateRange = 8;

    /// <summary>A melee pull backs off the group at most this long, then fights where it stands.</summary>
    public const int DrawMs = 5000;

    /// <summary>This far from the group's middle the rest will not join in: the draw is done.</summary>
    public const int DrawClearTiles = IsolateRange;

    /// <summary>The one foe a pull is for.</summary>
    public const int SoleTarget = 1;

    public static bool NeedsIsolate(int extraHostilesInRange) => extraHostilesInRange > 0;

    /// <summary>
    /// A melee pull walks in and lands its first blow before it backs off: nothing follows a
    /// fighter that never touched it. Backing off first drew nothing, then walked back into
    /// the middle of the group it meant to split.
    /// </summary>
    public static bool WaitsForFirstBlow(bool drawStarted, bool targetFollows) => !drawStarted && !targetFollows;

    /// <summary>
    /// The draw goes on while the target follows, its time lasts, more than the target is
    /// still near, and the group is still close behind.
    /// </summary>
    public static bool KeepsDrawing(bool targetFollows, long drawingMs, int hostilesNear, int tilesFromGroup) =>
        targetFollows && drawingMs < DrawMs && hostilesNear > SoleTarget && tilesFromGroup < DrawClearTiles;

    /// <summary>
    /// A bow or a spell does not pull a foe with a packmate near it out of a group too strong to
    /// take: it never shoots into a pack it cannot handle. It fights from where it stands and
    /// cannot draw the one foe off, and a creature takes up the nearest foe it perceives
    /// (BaseAI.AcquireFocusMob, sixteen tiles by default: BaseCreature.DefaultRangePerception),
    /// so a shooter four to eight tiles from a foe brings in every packmate within
    /// <see cref="IsolateRange"/> of it. Melee draws the one foe off first (<see cref="KeepsDrawing"/>).
    /// </summary>
    public static bool ShootsIntoPack(bool melee, int packmates) => !melee && packmates > 0;

    public static bool CanPullOne(
        int power,
        int focusThreat,
        int groupThreat,
        double hitsFraction,
        bool hasHealing,
        int alliesPower,
        double threatMultiple,
        double minHitsToOpen,
        bool alreadyAttacked
    )
    {
        if (alreadyAttacked)
        {
            return false;
        }

        if (ThreatRating.ShouldEngage(
                power,
                groupThreat,
                hitsFraction,
                hasHealing,
                alliesPower,
                threatMultiple,
                minHitsToOpen,
                alreadyAttacked: false))
        {
            return false;
        }

        return ThreatRating.ShouldEngage(
            power,
            focusThreat,
            hitsFraction,
            hasHealing,
            alliesPower,
            threatMultiple,
            minHitsToOpen,
            alreadyAttacked: false);
    }
}
