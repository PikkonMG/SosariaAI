using System;
using System.Runtime.CompilerServices;
using Server;
using SosariaAI.Mobiles;
using EngineMovement = Server.Movement.Movement;

namespace SosariaAI.Navigation;

/// <summary>
/// Finds the surface a character can stand on at a tile. The land average alone calls a
/// bridge, a dock or a raised floor a wall, because the walker stands on the static
/// surface above the land, not on the land. Mobiles never count: the character asking
/// is usually the one standing there.
/// </summary>
public static class Standable
{
    /// <summary>
    /// Furthest below the wanted height a surface still counts as the floor there. This is
    /// a tolerance for a height that is only roughly known, such as a marker's written
    /// height; a step between tiles is judged by <see cref="TryStep"/> instead.
    /// </summary>
    public const int DropBelow = 20;

    /// <summary>Furthest above the wanted height a surface still counts as the floor there.</summary>
    public const int ClimbAbove = 10;

    /// <summary>
    /// The body of a living person on foot. The step probe wears it, so a door leaf, a door
    /// beside a diagonal step and a locked door stop it as they stop the walker.
    /// </summary>
    public const int WalkerBody = 0x190;

    /// <summary>
    /// The body the engine lets through doors. The probe wears it only for a straight step
    /// into a closed door (<see cref="DoorTiles.OpensStraightAhead"/>): the character's own
    /// step opens that door. Worn for every step, it walked walkers through open door leaves
    /// and round doorway corners the engine refuses, and they stalled at bank doors.
    /// </summary>
    public const int DoorOpeningBody = 0x3DB;

    private static Mobile _walkerProbe;
    private static Mobile _doorProbe;

    /// <summary>
    /// Search span either side of the land average when no height is wanted. Wider than
    /// a step, because a bridge deck can sit well above the river bed under it: the
    /// Vesper bridges stand 26 above the water. The nearest surface to the land still
    /// wins, so a floor at ground level is chosen over the roof above it.
    /// </summary>
    public const int GroundSpan = 40;

    /// <summary>Surface nearest to <paramref name="nearZ"/>, from <see cref="DropBelow"/> under it to <see cref="ClimbAbove"/> over it.</summary>
    public static bool TryFind(Map map, int x, int y, int nearZ, out int surfaceZ) =>
        TryFind(map, x, y, nearZ, nearZ - DropBelow, nearZ + ClimbAbove, out surfaceZ);

    /// <summary>Surface nearest to the land average, for callers with no height in mind.</summary>
    public static bool TryFindGround(Map map, int x, int y, out int surfaceZ)
    {
        if (!IsLive(map, x, y))
        {
            surfaceZ = 0;
            return false;
        }

        var average = map.GetAverageZ(x, y);
        return TryFind(map, x, y, average, average - GroundSpan, average + GroundSpan, out surfaceZ);
    }

    /// <summary>
    /// One step as a player who knows the traps takes it: the engine's own movement check
    /// (<see cref="TryEngineStep"/>), and never from a safe tile onto one an armed trap hurts
    /// (<see cref="TrapRules.MayEnter"/>). The escape and kite steps of a fight walk with it.
    /// It reads the map, so call it on the world thread only.
    /// </summary>
    public static bool TryStep(Map map, int fromX, int fromY, int fromZ, int toX, int toY, out int toZ)
    {
        if (!TryEngineStep(map, fromX, fromY, fromZ, toX, toY, out toZ))
        {
            return false;
        }

        var traps = TrapTiles.For(map);
        return traps.Count == 0 || TrapRules.MayEnter(traps.Harms(fromX, fromY, fromZ), traps.Harms(toX, toY, toZ));
    }

    /// <summary>
    /// One step as the engine's own movement check judges it: from a tile at a height onto
    /// a neighbouring tile. True when a living player on foot can make it, and
    /// <paramref name="toZ"/> is then the height it lands at. A closed door straight ahead
    /// does not stop it, as the character's step opens the door; every other door does. The
    /// engine picks the landing floor nearest the walking mobile's height, so a detached
    /// probe mobile is set on the start spot for each step. It reads the map, so call it on
    /// the world thread only.
    /// </summary>
    private static bool TryEngineStep(Map map, int fromX, int fromY, int fromZ, int toX, int toY, out int toZ)
    {
        toZ = fromZ;

        if (!IsLive(map, fromX, fromY) || NavMetric.Chebyshev(new Point3D(toX, toY, fromZ), new Point3D(fromX, fromY, fromZ)) != 1)
        {
            return false;
        }

        var from = new Point3D(fromX, fromY, fromZ);
        var direction = Utility.GetDirection(fromX, fromY, toX, toY);

        return ProbeStep(_walkerProbe ??= CreateStepProbe(WalkerBody), map, from, direction, out toZ) ||
               DoorTiles.OpensStraightAhead(direction, DoorTiles.ClosedDoorAt(map, toX, toY, fromZ) != null) &&
               ProbeStep(_doorProbe ??= CreateStepProbe(DoorOpeningBody), map, from, direction, out toZ);
    }

    private static bool ProbeStep(Mobile probe, Map map, Point3D from, Direction direction, out int toZ)
    {
        LocationOf(probe) = from;
        return EngineMovement.CheckMovement(probe, map, from, direction, out toZ);
    }

    /// <summary>
    /// The walker for a live map: each step is <see cref="TryEngineStep"/>, the floor near a
    /// height is the surface <see cref="TryFind(Map, int, int, int, out int)"/> finds, or
    /// the height asked on a door tile, and the tiles hurt are the map's armed traps
    /// (<see cref="TrapTiles"/>). World thread only.
    /// </summary>
    public static TileWalker Walker(Map map)
    {
        bool Step(int fromX, int fromY, int fromZ, int toX, int toY, out int toZ) =>
            TryEngineStep(map, fromX, fromY, fromZ, toX, toY, out toZ);

        int? FloorNear(int x, int y, int z)
        {
            if (TryFind(map, x, y, z, out var surface))
            {
                return surface;
            }

            return DoorTiles.HasDoor(map, x, y) ? z : null;
        }

        var traps = TrapTiles.For(map);
        return new TileWalker(FloorNear, Step, traps.Count == 0 ? null : traps.Harms);
    }

    /// <summary>
    /// The live map's walker for a walk inside a dungeon: a teleporter pad counts as a tile
    /// that hurts, so a tile route goes round the pads it does not mean to take. World
    /// thread only.
    /// </summary>
    public static TileWalker PadShyWalker(Map map) => ShunningPads(Walker(map), TrapTiles.PadsFor(map));

    /// <summary>
    /// <paramref name="walker"/> with each pad of <paramref name="pads"/> hurting its tile as
    /// a trap does: the tile router walks round it and crosses it only where no other way
    /// leads on. A goal on a pad is still reached. Pure.
    /// </summary>
    public static TileWalker ShunningPads(TileWalker walker, TrapField pads) =>
        pads == null || pads.Count == 0
            ? walker
            : walker with { Harms = (x, y, z) => walker.IsHarmed(x, y, z) || pads.Harms(x, y, z) };

    /// <summary>
    /// A player mobile the engine can judge steps for. It is never added to the world, so
    /// it is never saved, seen or ticked. Its fields are set directly: the property setters
    /// would start timers and send updates for a mobile that does not exist.
    /// </summary>
    private static Mobile CreateStepProbe(int body)
    {
        var probe = new Mobile(Serial.MinusOne);
        PlayerOf(probe) = true;
        BodyOf(probe) = body;
        return probe;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "m_Location")]
    private static extern ref Point3D LocationOf(Mobile mobile);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "m_Player")]
    private static extern ref bool PlayerOf(Mobile mobile);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "m_Body")]
    private static extern ref Body BodyOf(Mobile mobile);

    private static bool TryFind(Map map, int x, int y, int nearZ, int minZ, int maxZ, out int surfaceZ)
    {
        surfaceZ = nearZ;

        if (!IsLive(map, x, y))
        {
            return false;
        }

        var found = false;
        var bestGap = int.MaxValue;

        if (LandWalkable(map, x, y))
        {
            Consider(map, x, y, map.GetAverageZ(x, y), nearZ, minZ, maxZ, ref found, ref bestGap, ref surfaceZ);
        }

        foreach (var tile in map.Tiles.GetStaticAndMultiTiles(x, y))
        {
            var data = TileData.ItemTable[tile.ID & TileData.MaxItemValue];

            if (data.Surface && !data.Impassable)
            {
                Consider(map, x, y, tile.Z + data.CalcHeight, nearZ, minZ, maxZ, ref found, ref bestGap, ref surfaceZ);
            }
        }

        foreach (var item in map.GetItemsAt(x, y))
        {
            if (item.Movable || item.ItemID > TileData.MaxItemValue)
            {
                continue;
            }

            var data = item.ItemData;

            if (data.Surface && !data.Impassable)
            {
                Consider(map, x, y, item.Z + data.CalcHeight, nearZ, minZ, maxZ, ref found, ref bestGap, ref surfaceZ);
            }
        }

        return found;
    }

    private static void Consider(
        Map map,
        int x,
        int y,
        int candidateZ,
        int nearZ,
        int minZ,
        int maxZ,
        ref bool found,
        ref int bestGap,
        ref int surfaceZ
    )
    {
        if (candidateZ < minZ || candidateZ > maxZ)
        {
            return;
        }

        var gap = Math.Abs(candidateZ - nearZ);

        if (gap >= bestGap)
        {
            return;
        }

        if (!map.CanFit(x, y, candidateZ, PersonBody.Height, checkBlocksFit: false, checkMobiles: false))
        {
            return;
        }

        found = true;
        bestGap = gap;
        surfaceZ = candidateZ;
    }

    /// <summary>True when the land tile itself is ground a person could walk on: not water, rock or void.</summary>
    public static bool LandWalkable(Map map, int x, int y)
    {
        if (!IsLive(map, x, y))
        {
            return false;
        }

        var land = map.Tiles.GetLandTile(x, y);
        return !land.Ignored && (TileData.LandTable[land.ID & TileData.MaxLandValue].Flags & TileFlag.Impassable) == 0;
    }

    private static bool IsLive(Map map, int x, int y) =>
        map != null && map != Map.Internal && x >= 0 && y >= 0 && x < map.Width && y < map.Height;
}
