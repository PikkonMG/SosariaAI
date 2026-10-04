using System;
using Server;
using SosariaAI.Common;

namespace SosariaAI.Behaviour;

/// <summary>The dungeon a walk home failed from, and since the first such failure. Default: none.</summary>
public readonly record struct DungeonTrouble(string Dungeon, DateTime Since);

/// <summary>A way out of a dungeon when the walk home found none.</summary>
public enum DungeonWayOut
{
    /// <summary>No way out is left to try yet.</summary>
    None,

    /// <summary>Recall to the rune marked nearest home.</summary>
    Recall,

    /// <summary>Walk onto a teleporter pad that lands outside every dungeon.</summary>
    ExitPad,

    /// <summary>Stand at the dungeon's door outside, like the stuck help a player could call.</summary>
    Door
}

/// <summary>
/// How a person gets out of a dungeon its walk home cannot leave. Four walkers stood in
/// Felucca dungeons for an hour writing "every way home failed": the moongate walk used the
/// same graph as the walk home, and the wait made the dungeon tile their home. A person tries
/// what a player tries: Recall where the place allows it, then a pad that leads out, and
/// after five minutes stuck, the door. Pure.
/// </summary>
public static class DungeonEscapeRules
{
    /// <summary>How long home walks may fail inside one dungeon before the person stands at its door.</summary>
    public static readonly TimeSpan StuckBeforeDoor = TimeSpan.FromMinutes(5);

    /// <summary>How far round the person a pad that leads out is looked for.</summary>
    public const int ExitPadSearchTiles = 64;

    /// <summary>
    /// The trouble after one more failed walk home from <paramref name="dungeon"/>: the same
    /// dungeon keeps its first failure's time, another starts a new count, and outside any
    /// dungeon there is none.
    /// </summary>
    public static DungeonTrouble Track(DungeonTrouble prior, string dungeon, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(dungeon))
        {
            return default;
        }

        return NeedsWayOut(prior, dungeon) ? prior : new DungeonTrouble(dungeon, now);
    }

    /// <summary>
    /// The trouble once the person stands in <paramref name="dungeon"/>: kept in the dungeon
    /// the walk home failed from, gone anywhere else. Out by a pad, a gate or a death, a
    /// person back inside later has not been stuck all that time.
    /// </summary>
    public static DungeonTrouble AfterMove(DungeonTrouble trouble, string dungeon) =>
        NeedsWayOut(trouble, dungeon) ? trouble : default;

    /// <summary>True when home walks have failed inside the dungeon for <see cref="StuckBeforeDoor"/>.</summary>
    public static bool IsStuck(DungeonTrouble trouble, DateTime now) =>
        TimeRules.Passed(trouble.Since, now, StuckBeforeDoor);

    /// <summary>
    /// A way out comes first once a walk home from inside <paramref name="dungeon"/> has
    /// failed. A failure elsewhere, in town or in another dungeon, does not count.
    /// </summary>
    public static bool NeedsWayOut(DungeonTrouble trouble, string dungeon) =>
        trouble.Since != default && !string.IsNullOrWhiteSpace(dungeon) &&
        string.Equals(trouble.Dungeon, dungeon, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The next way out to try, in a player's order: Recall, then a pad that leads out, each
    /// once per try home, then the door once the person is stuck.
    /// </summary>
    public static DungeonWayOut Next(bool canRecall, bool triedRecall, bool hasExitPad, bool triedExitPad, bool stuck)
    {
        if (canRecall && !triedRecall)
        {
            return DungeonWayOut.Recall;
        }

        if (hasExitPad && !triedExitPad)
        {
            return DungeonWayOut.ExitPad;
        }

        return stuck ? DungeonWayOut.Door : DungeonWayOut.None;
    }

    /// <summary>The line a lift to the door writes, easy to count.</summary>
    public static string DoorLine(string name, string dungeon, Point3D from, Point3D to) =>
        $"{name} was stuck in {dungeon} at ({from.X},{from.Y}); stood at its door ({to.X},{to.Y})";
}
