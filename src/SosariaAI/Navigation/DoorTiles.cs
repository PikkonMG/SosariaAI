using System;
using Server;
using Server.Items;
using SosariaAI.Mobiles;

namespace SosariaAI.Navigation;

/// <summary>
/// Doors as a walker meets them. Closed doors fail the stand test. A character opens a
/// closed door straight ahead as it walks, so a route may use the tile. An open door is
/// no way through: its leaf stands on the tile beside the doorway and blocks a person
/// there, and a diagonal step past a door leaf or a closed door is refused by the engine's
/// corner rule. A person who stands in a doorway holds the door open and blocks the way,
/// so a standing person steps out of it (<see cref="StepOff"/>).
/// </summary>
public static class DoorTiles
{
    /// <summary>No step out of a doorway: every way is shut, taken or another doorway.</summary>
    public const int NoStep = -1;

    /// <summary>How far an open door's leaf stands from its doorway: one tile, as the engine's door offsets.</summary>
    public const int LeafReach = 1;

    private const int DirectionCount = 8;
    private const int DiagonalBit = 1;

    public static bool HasDoor(Map map, int x, int y)
    {
        if (map == null || map == Map.Internal)
        {
            return false;
        }

        foreach (var _ in map.GetItemsAt<BaseDoor>(x, y))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// The closed door a person standing at height <paramref name="z"/> opens on the tile, or
    /// null: an open door is already open, a locked one stays shut, and a door wholly above
    /// or below the person is not in its way. The same test the character's step uses.
    /// </summary>
    public static BaseDoor ClosedDoorAt(Map map, int x, int y, int z)
    {
        if (map == null || map == Map.Internal)
        {
            return null;
        }

        foreach (var door in map.GetItemsAt<BaseDoor>(x, y))
        {
            if (!door.Open && !(door.Locked && door.UseLocks()) &&
                door.Z + door.ItemData.Height > z && z + PersonBody.Height > door.Z)
            {
                return door;
            }
        }

        return null;
    }

    /// <summary>
    /// True when a step the engine refuses goes through all the same once the walker opens the
    /// door: a closed door stands on the tile ahead and the step is straight, not diagonal. A
    /// diagonal step into a doorway cuts the frame's corner, which the engine never allows. Pure.
    /// </summary>
    public static bool OpensStraightAhead(Direction direction, bool closedDoorAhead) =>
        closedDoorAhead && ((int)(direction & Direction.Mask) & DiagonalBit) == 0;

    /// <summary>
    /// True when the tile is a doorway: a door's frame, where the door stands shut or from
    /// which its open leaf swung aside.
    /// </summary>
    public static bool IsDoorway(Map map, int x, int y)
    {
        if (map == null || map == Map.Internal)
        {
            return false;
        }

        foreach (var door in map.GetItemsInRange<BaseDoor>(x, y, LeafReach))
        {
            var frameX = door.Open ? door.X - door.Offset.X : door.X;
            var frameY = door.Open ? door.Y - door.Offset.Y : door.Y;

            if (frameX == x && frameY == y)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The direction a person standing in a doorway steps out: the first, counting round from
    /// <paramref name="start"/>, to a clear tile. <see cref="NoStep"/> when none is clear. Pure.
    /// </summary>
    /// <param name="clear">One flag per direction, in the engine's direction order: a free tile off the doorway.</param>
    /// <param name="start">The direction tried first, such as the person's facing.</param>
    public static int StepOff(ReadOnlySpan<bool> clear, int start)
    {
        for (var turn = 0; turn < DirectionCount; turn++)
        {
            var direction = ((start + turn) % DirectionCount + DirectionCount) % DirectionCount;

            if (direction < clear.Length && clear[direction])
            {
                return direction;
            }
        }

        return NoStep;
    }
}
