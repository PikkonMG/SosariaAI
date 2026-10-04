using Server;

namespace SosariaAI.Navigation;

/// <summary>
/// Shop markers often sit on a roof (Britain's blacksmith is Z 20). A roof is no floor a
/// customer walks to, so a trip aimed at it logs "no route" and the step fails. Drop the
/// goal to the ground floor when the marker is a roof. A height with no floor near it at
/// all names no floor either: a Trinsic home written at the moongate's -20 stood over
/// ground at 0, and every walk there spent its whole search budget looking for -20. The
/// other way round is no fix: when the goal's own tile is walled or furnished at ground
/// level, the only "floor" left is the roof over it, and the goal keeps its height. The
/// Moonglow bank spot (4471,1156) sits under its counter; lifted onto the slate roof at 20,
/// it made 87 stalls in ten minutes. A goal that names such a roof itself is no place a
/// street walk reaches (<see cref="IsRoofOverBlockedTile"/>): a person stands beside it
/// (<see cref="StreetSpot"/>).
/// </summary>
public static class ArrivalHeight
{
    /// <summary>Height over the ground from which a marker counts as a roof, not the shop floor.</summary>
    public const int RoofRise = 10;

    public static bool IsRoofAbove(int goalZ, int groundZ) =>
        goalZ - groundZ >= RoofRise;

    /// <param name="floorAtGoal">True when a surface stands near the goal's own height.</param>
    public static Point3D PreferGround(Point3D goal, int groundZ, bool floorAtGoal)
    {
        if (goal == Point3D.Zero)
        {
            return goal;
        }

        return !floorAtGoal || IsRoofAbove(goal.Z, groundZ) ? new Point3D(goal.X, goal.Y, groundZ) : goal;
    }

    /// <summary>
    /// True when the ground found stands a roof's height over land a person could walk on:
    /// the land is blocked by a wall, a counter or a stall, and the surface is the roof.
    /// Water under a bridge is no such land; the bridge deck is the floor there.
    /// </summary>
    public static bool IsRoofOverBlockedLand(int groundZ, int landZ, bool landWalkable) =>
        landWalkable && IsRoofAbove(groundZ, landZ);

    public static Point3D PreferGround(Map map, Point3D goal)
    {
        if (goal == Point3D.Zero || map == null || map == Map.Internal ||
            !Standable.TryFindGround(map, goal.X, goal.Y, out var ground))
        {
            return goal;
        }

        var floorAtGoal = Standable.TryFind(map, goal.X, goal.Y, goal.Z, out _);

        return !floorAtGoal && GroundIsRoofOverBlockedLand(map, goal.X, goal.Y, ground)
            ? goal
            : PreferGround(goal, ground, floorAtGoal);
    }

    /// <summary>
    /// True when <paramref name="goal"/> names the roof over a walled or furnished tile: the
    /// land there is ground a person could walk on, but a wall, a counter or a stall fills
    /// it, and the only floor is the roof a roof's height above. No street walk reaches it;
    /// a person sent there stands beside it. Two Yew bank crowd seats sat on the bank's
    /// south wall at 20 and made 89 stalls in an evening, every walker one tile short.
    /// </summary>
    public static bool IsRoofOverBlockedTile(Map map, IPoint3D goal) =>
        goal != null && map != null && map != Map.Internal &&
        Standable.TryFindGround(map, goal.X, goal.Y, out var ground) &&
        NavMetric.SameFloor(goal.Z, ground) &&
        GroundIsRoofOverBlockedLand(map, goal.X, goal.Y, ground);

    /// <summary>
    /// Where a person stands for <paramref name="spot"/> at street level: the spot itself,
    /// unless it names the roof over a walled tile (<see cref="IsRoofOverBlockedTile"/>);
    /// then the first tile beside it whose ground is no such roof, on that ground. The spot
    /// itself when no tile beside it stands either.
    /// </summary>
    public static Point3D StreetSpot(Map map, Point3D spot)
    {
        if (!IsRoofOverBlockedTile(map, spot))
        {
            return spot;
        }

        foreach (var (dx, dy) in TileGrid.Neighbours)
        {
            var x = spot.X + dx;
            var y = spot.Y + dy;

            if (Standable.TryFindGround(map, x, y, out var ground) && !GroundIsRoofOverBlockedLand(map, x, y, ground))
            {
                return new Point3D(x, y, ground);
            }
        }

        return spot;
    }

    private static bool GroundIsRoofOverBlockedLand(Map map, int x, int y, int groundZ) =>
        IsRoofOverBlockedLand(groundZ, map.GetAverageZ(x, y), Standable.LandWalkable(map, x, y));
}
