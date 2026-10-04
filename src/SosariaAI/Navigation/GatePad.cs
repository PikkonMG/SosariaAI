using System;
using System.Collections.Generic;
using Server;
using Server.Items;

namespace SosariaAI.Navigation;

/// <summary>
/// Whether a real gate item stands at a tile. The gate skill must move a character only
/// when the item it claims to use is under the character.
/// </summary>
public static class GatePad
{
    /// <summary>
    /// How far a character looks for the real pad when the planned gate tile is bare.
    /// Dungeon stairs are one-way pads laid in pairs a tile apart. The route graph joins
    /// both ends of each pair both ways, so a plan can name the landing tile of the pad
    /// that leads the other way. Despise does this at (5503,570) and (5504,570).
    /// The overland Despise mouth is [go Entrance] at (1298,1080); the pad sits at
    /// (1296,1080), three tiles west. The stair up out of (6320,22) stands at (6319,19),
    /// four tiles off its landing.
    /// </summary>
    public const int ReachTiles = 4;

    /// <summary>
    /// How far a pad may sit above or below the person reaching for it. A stair pad lies at
    /// the foot or head of its flight: the pad at (5153,808) stands at -25 while the stair
    /// beside it is walked at -13, and the one at (6316,62) at -5 over a landing at -20. The
    /// engine fires a pad for a mover up to fifteen below it.
    /// </summary>
    public const int PadClimbZ = 15;

    /// <summary>A nearby pad counts only when it lands this close to where the plan goes.</summary>
    public const int LandingSlackTiles = 3;

    /// <summary>
    /// A teleporter pad: where it stands, where it sends, if it stays on this map, if a step
    /// from some tile beside it lands on its tile (<see cref="SomeStepReaches"/>), and if some
    /// step onto it sets it off (not <see cref="NeverFires(TileWalker, Point3D, int)"/>).
    /// </summary>
    public readonly record struct Pad(Point3D Location, Point3D Lands, bool SameMap, bool Reachable = true, bool Fires = true);

    /// <summary>
    /// The span of one pick tier: a pad of a lower tier anywhere in reach ranks before a pad
    /// of a higher tier (<see cref="Pick"/>).
    /// </summary>
    private const int PadTierSpan = ReachTiles + 1;

    /// <summary>A pad on the walker's floor that a step lands on.</summary>
    private const int OnFloorTier = 0;

    /// <summary>
    /// A pad a step lands on, off the walker's floor: the walk to a pad up a stair ends at the
    /// foot of the stair. Walkers at (6032,1498,22) stood twenty below the Britain sewer pad
    /// at (6031,1499,42), which is stepped onto from its landing at (6032,1499,31).
    /// </summary>
    private const int OffFloorTier = 1;

    /// <summary>
    /// A pad on the walker's floor that no step lands on. It is still picked when it is the
    /// only one: the walker's floor search can miss a floor the engine walks.
    /// </summary>
    private const int UnreachedTier = 2;

    /// <summary>
    /// A pad no step onto sets off. It is picked only when no live pad lands where the plan
    /// goes, and then the gate skill gives it up at once. The Fire pads at (5792,1415..1417)
    /// all land by the same stair; the dead one at 1415 was picked first, and walkers home
    /// gave the stair up twice though the live pad beside it carried people.
    /// </summary>
    private const int DeadTier = 3;

    /// <summary>
    /// The pad to use, or -1. The live pad under the character, on its floor, wins. Otherwise the
    /// nearest pad in reach that lands where the plan goes, as a player steps one tile onto
    /// the stair. A pad on the walker's floor that a step lands on goes first, then a pad off
    /// its floor that a step lands on, then a pad on its floor that no step lands on: the
    /// Felucca pad at (2400,198) lies on a tile with no floor, beside its twin at (2399,198)
    /// that lands one tile from it. A pad off the floor that no step lands on, or that stands
    /// over or under the walker's own tile, is never used. A pad that never fires goes last
    /// (<see cref="DeadTier"/>).
    /// </summary>
    public static int Pick(Point3D at, Point3D plannedLanding, IReadOnlyList<Pad> pads)
    {
        if (pads == null)
        {
            return -1;
        }

        var best = -1;
        var bestRank = int.MaxValue;

        for (var i = 0; i < pads.Count; i++)
        {
            var pad = pads[i];
            var onFloor = OnPadFloor(at, pad.Location);

            if (onFloor && pad.Fires && pad.Location.X == at.X && pad.Location.Y == at.Y)
            {
                return i;
            }

            var distance = NavMetric.Chebyshev(pad.Location, at);

            // A pad above or below the walker's own tile is on another floor, not under it.
            if (!onFloor && (!pad.Reachable || distance == 0))
            {
                continue;
            }

            var tier = !pad.Fires ? DeadTier : !pad.Reachable ? UnreachedTier : onFloor ? OnFloorTier : OffFloorTier;
            var rank = tier * PadTierSpan + distance;

            if (distance > ReachTiles || rank >= bestRank || !LandsNear(pad, plannedLanding))
            {
                continue;
            }

            best = i;
            bestRank = rank;
        }

        return best;
    }

    private static bool LandsNear(Pad pad, Point3D plannedLanding) =>
        pad.SameMap && pad.Lands != Point3D.Zero &&
        NavMetric.Chebyshev(pad.Lands, plannedLanding) <= LandingSlackTiles;

    public static bool IsInReach(Point3D at, Point3D pad) =>
        NavMetric.Chebyshev(at, pad) <= ReachTiles && OnPadFloor(at, pad);

    /// <summary>True when a person at <paramref name="at"/> can step onto a pad at <paramref name="pad"/>: the flight between is short.</summary>
    public static bool OnPadFloor(Point3D at, Point3D pad) => Math.Abs(at.Z - pad.Z) <= PadClimbZ;

    /// <summary>
    /// True when a pad at <paramref name="padZ"/>, <paramref name="padHeight"/> tall, fires for a
    /// person who lands on its tile at <paramref name="standZ"/>. This is the engine's own
    /// move-over test: the pad fires when it lies level with the mover's feet, or when its top
    /// stands above the feet and the mover stands less than <see cref="PadClimbZ"/> below it.
    /// </summary>
    public static bool Fires(int padZ, int padHeight, int standZ) =>
        padZ == standZ || padZ + padHeight > standZ && standZ + PadClimbZ > padZ;

    /// <summary>
    /// True when a walker steps onto the pad's tile from a tile beside it, and every such step
    /// lands where the pad does not fire (<see cref="Fires"/>). The Jhelom pad at (1406,3996)
    /// lies at 5 on a tile whose floor is 6: it carried nobody, and the walkers routed over it
    /// stepped on and off it for hours. False when some step fires the pad, and false when no
    /// step reaches the tile at all: then nothing is known. Pure over the walker.
    /// </summary>
    public static bool NeverFires(TileWalker walker, Point3D pad, int padHeight) =>
        StepsOnto(walker, pad, padHeight, out _) == PadSteps.NoneFire;

    /// <summary>
    /// True when a walker steps onto the pad's tile from some tile beside it. The Felucca pad
    /// at (2400,198) lies on a tile with no floor, and no step lands there. Pure over the walker.
    /// </summary>
    public static bool SomeStepReaches(TileWalker walker, Point3D pad, int padHeight) =>
        StepsOnto(walker, pad, padHeight, out _) != PadSteps.None;

    /// <summary>
    /// The height a pad no step sets off (<see cref="NeverFires(TileWalker, Point3D, int)"/>)
    /// fires at: the highest floor a step onto its tile lands on, where the engine's test holds
    /// for that step and for every step up to <see cref="PadClimbZ"/> below it. Null when some
    /// step already sets the pad off, no step reaches its tile, or that floor lies more than
    /// <see cref="PadClimbZ"/> from the pad. The Jhelom pad at (1406,3996) lies at 5 under a
    /// floor of 6 and fires at 6. Pure over the walker.
    /// </summary>
    public static int? FiringZ(TileWalker walker, Point3D pad, int padHeight) =>
        StepsOnto(walker, pad, padHeight, out var topLanding) == PadSteps.NoneFire &&
        Math.Abs(topLanding - pad.Z) <= PadClimbZ
            ? topLanding
            : null;

    /// <summary>What the steps onto a pad's tile from the tiles beside it do.</summary>
    private enum PadSteps
    {
        /// <summary>No step lands on the tile, or there is no walker.</summary>
        None,

        /// <summary>Steps land on the tile, and the pad fires for none of them.</summary>
        NoneFire,

        /// <summary>Some step lands where the pad fires.</summary>
        SomeFire
    }

    /// <summary>What the steps onto the pad's tile do; <paramref name="topLanding"/> is the highest floor a step lands on.</summary>
    private static PadSteps StepsOnto(TileWalker walker, Point3D pad, int padHeight, out int topLanding)
    {
        topLanding = int.MinValue;

        if (walker == null)
        {
            return PadSteps.None;
        }

        var steppedOn = false;
        ReadOnlySpan<int> fromHeights = [pad.Z, pad.Z + PadClimbZ, pad.Z - PadClimbZ];

        foreach (var (dx, dy) in TileGrid.Neighbours)
        {
            var x = pad.X + dx;
            var y = pad.Y + dy;

            foreach (var height in fromHeights)
            {
                if (walker.FloorNear(x, y, height) is not { } floor || !walker.Step(x, y, floor, pad.X, pad.Y, out var z))
                {
                    continue;
                }

                topLanding = Math.Max(topLanding, z);

                if (Fires(pad.Z, padHeight, z))
                {
                    return PadSteps.SomeFire;
                }

                steppedOn = true;
            }
        }

        return steppedOn ? PadSteps.NoneFire : PadSteps.None;
    }

    /// <summary>
    /// The tile a walker at <paramref name="at"/>, beside a pad, steps onto next on its way onto
    /// the pad: the pad's own tile when that step holds, else a tile beside the walker from
    /// which the step onto the pad holds. The engine refuses a diagonal step past a blocked
    /// corner: the walkers set down at (2400,199) by the pad up out of the dungeon stand
    /// diagonal to the pad down at (2399,198), with no floor at (2400,198), and must step
    /// west first. Steps keep off armed traps (<see cref="TileWalker.SafeStep"/>). Null when
    /// neither holds. Pure over the walker.
    /// </summary>
    public static Point3D? EntryStep(TileWalker walker, Point3D at, Point3D pad)
    {
        if (walker == null)
        {
            return null;
        }

        if (walker.SafeStep(at.X, at.Y, at.Z, pad.X, pad.Y, out _))
        {
            return pad;
        }

        foreach (var (dx, dy) in TileGrid.Neighbours)
        {
            var x = at.X + dx;
            var y = at.Y + dy;

            if (NavMetric.Chebyshev(new Point3D(x, y, pad.Z), pad) != 1)
            {
                continue;
            }

            if (walker.SafeStep(at.X, at.Y, at.Z, x, y, out var z) && walker.SafeStep(x, y, z, pad.X, pad.Y, out _))
            {
                return new Point3D(x, y, z);
            }
        }

        return null;
    }

    /// <summary>
    /// The tile a walker standing on a pad that did not fire steps off onto, so that it can
    /// step back on: the first tile beside it with no pad of its own (<paramref name="padAt"/>)
    /// whose step there and back both hold. A step onto a pad beside it would carry the walker
    /// off by that pad. The pad up out of Covetous sets walkers down on the pad in at
    /// (2420,883), whose first open side is the pad at (2421,883): the fixed turn of steps off
    /// took that pad, went on stepping from the far side, and 11 trips ended there. Null when
    /// no tile serves. Pure over the walker.
    /// </summary>
    public static Point3D? StepOffTile(TileWalker walker, Point3D at, Func<int, int, int, bool> padAt)
    {
        if (walker == null || padAt == null)
        {
            return null;
        }

        foreach (var (dx, dy) in TileGrid.Neighbours)
        {
            var x = at.X + dx;
            var y = at.Y + dy;

            if (walker.SafeStep(at.X, at.Y, at.Z, x, y, out var z) && !padAt(x, y, z) &&
                walker.SafeStep(x, y, z, at.X, at.Y, out _))
            {
                return new Point3D(x, y, z);
            }
        }

        return null;
    }

    /// <summary>
    /// True when active teleporter pads stand on the tile and none of them fires for a walker
    /// who steps onto it (<see cref="NeverFires(TileWalker, Point3D, int)"/>). False on a tile
    /// with no active pad: the gate skill looks for the pad in reach. World thread only.
    /// </summary>
    public static bool NeverFires(Map map, TileWalker walker, int x, int y)
    {
        if (map == null || map == Map.Internal)
        {
            return false;
        }

        var dead = false;

        foreach (var pad in map.GetItemsAt<Teleporter>(x, y))
        {
            if (pad is not { Deleted: false, Active: true })
            {
                continue;
            }

            if (!NeverFires(walker, pad.Location, pad.ItemData.Height))
            {
                return false;
            }

            dead = true;
        }

        return dead;
    }

    public static bool IsUnder(Map map, Point3D at, NavGateKind kind)
    {
        if (map == null || map == Map.Internal)
        {
            return false;
        }

        return kind switch
        {
            NavGateKind.Teleporter => HasItem<Teleporter>(map, at),
            NavGateKind.Moongate => HasItem<PublicMoongate>(map, at),
            _ => false
        };
    }

    private static bool HasItem<T>(Map map, Point3D at) where T : Item
    {
        foreach (var item in map.GetItemsAt<T>(at.X, at.Y))
        {
            if (NavMetric.SameFloor(item, at))
            {
                return true;
            }
        }

        return false;
    }
}
