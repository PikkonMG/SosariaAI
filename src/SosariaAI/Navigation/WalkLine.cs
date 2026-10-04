using System;
using Server;

namespace SosariaAI.Navigation;

/// <summary>
/// The straight walk from one tile to another, one step at a time with the walker's own
/// step rule. Every tile on the line is stepped, because a one-tile wall slips between
/// sparser samples, and the height is carried from step to step. A line never steps onto
/// a tile an armed trap hurts (<see cref="TileWalker.SafeStep"/>), so a graph edge or a
/// reach check built on it goes round the traps; the tile router finds the way round.
/// </summary>
public static class WalkLine
{
    public const int Step = 1;

    /// <summary>
    /// Walks the tiles of the straight line from one tile to another, one step at a time,
    /// with the walker's own trap-aware step rule. The height is carried from step to step,
    /// so the walk climbs a slope and stops at a cliff or a trap. Every tile reached must also pass
    /// <paramref name="keep"/> when one is given.
    /// </summary>
    /// <param name="toZ">The height the walk ends at on the last tile.</param>
    public static bool TryWalk(
        TileWalker walker,
        int ax,
        int ay,
        int az,
        int bx,
        int by,
        Func<int, int, int, bool> keep,
        out int toZ
    )
    {
        toZ = az;

        if (walker == null)
        {
            return false;
        }

        var dist = NavMetric.Chebyshev(new Point3D(ax, ay, az), new Point3D(bx, by, az));
        var x = ax;
        var y = ay;

        for (var step = Step; step <= dist; step += Step)
        {
            var nextX = Lerp(ax, bx, step, dist);
            var nextY = Lerp(ay, by, step, dist);

            if (!walker.SafeStep(x, y, toZ, nextX, nextY, out var nextZ) || keep?.Invoke(nextX, nextY, nextZ) == false)
            {
                return false;
            }

            x = nextX;
            y = nextY;
            toZ = nextZ;
        }

        return true;
    }

    /// <summary>
    /// True when the straight walk from <paramref name="from"/> ends on the floor
    /// <paramref name="to"/> stands on. A walk that arrives on the floor above or below
    /// the goal has not reached it.
    /// </summary>
    public static bool Reaches(TileWalker walker, Point3D from, Point3D to, Func<int, int, int, bool> keep = null) =>
        TryWalk(walker, from.X, from.Y, from.Z, to.X, to.Y, keep, out var endZ) && NavMetric.SameFloor(endZ, to.Z);

    /// <summary>
    /// The keep test for a line between two spots: a line between two outdoor spots must
    /// not cut through a building on the way. Null when either end is indoors or there is
    /// no roof test.
    /// </summary>
    public static Func<int, int, int, bool> OutdoorKeep(bool fromIndoor, bool toIndoor, Func<int, int, int, bool> isIndoor) =>
        fromIndoor || toIndoor ? null : Outdoors(isIndoor);

    /// <summary>A keep test that refuses every tile under a roof. Null when there is no roof test.</summary>
    public static Func<int, int, int, bool> Outdoors(Func<int, int, int, bool> isIndoor) =>
        isIndoor == null ? null : (x, y, z) => !isIndoor(x, y, z);

    /// <summary>The tile <paramref name="step"/> tiles along a line <paramref name="dist"/> tiles long.</summary>
    internal static int Lerp(int from, int to, int step, int dist) =>
        dist == 0 ? from : from + (to - from) * step / dist;
}
