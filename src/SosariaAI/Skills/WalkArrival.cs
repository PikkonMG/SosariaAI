using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// The one arrival rule every walk uses, and the standalone-travel test for the saved
/// last-walk result. InRange ignores height, so the floor counts only on a trip's final
/// goal: middle legs of a stair route sit a floor above or below on purpose. A floor
/// check on every graph node left walkers beside a node the engine's PathFollower called
/// reached (one tile, under 16 high), never stepping, until the stall guard gave up.
/// </summary>
public static class WalkArrival
{
    public static bool IsWalk(string skill) => skill is SkillKinds.GoTo;

    public static bool Arrived(Point3D from, IPoint3D goal, int range, bool checkFloor) =>
        InReach(from, goal, range) && (!checkFloor || NavMetric.SameFloor(from, goal));

    /// <summary>
    /// In reach of the final goal on the map but on another floor. The PathFollower will
    /// not step here, so a floor step search climbs or drops the rest.
    /// </summary>
    public static bool NeedsFloorSteps(Point3D from, IPoint3D goal, int range, bool checkFloor) =>
        checkFloor && InReach(from, goal, range) && !NavMetric.SameFloor(from, goal);

    /// <summary>
    /// The range for the walk to a tile route's last tile. The route already ends inside
    /// <paramref name="range"/> of the goal, so that walk keeps only what is left of the
    /// range, with the one tile of slack every other leg has. Given the whole range again,
    /// a walk stopped up to twice the range out: cooks sent six tiles from the Britain inn
    /// stood twelve off, beyond every oven.
    /// </summary>
    public static int TileRouteEndRange(Point3D lastTile, Point3D goal, int range) =>
        Math.Min(range, Math.Max(CharactersFile.DefaultGoToRange, range - NavMetric.Chebyshev(lastTile, goal)));

    private static bool InReach(Point3D from, IPoint3D goal, int range) =>
        Utility.InRange(from.X, from.Y, goal.X, goal.Y, range);
}
