using System.Collections.Generic;
using Server;

namespace SosariaAI.Behaviour;

/// <summary>A place a person could come back on, on a dungeon floor: whether it fits there, and the monsters about.</summary>
public readonly record struct ReturnSpot(Point3D Location, bool Standable, int Hostiles);

/// <summary>
/// The engine holds every player on Map.Internal when the world loads after a restart. A
/// person that stood in a dungeon when the world was saved comes back there, on the same
/// floor, as a player's client puts it back where it stood; a person coming back to a crowd
/// of monsters would die before it moved, so the spot is the save-time tile when it is quiet,
/// else the nearest quiet room on that floor, else the standable spot with the fewest
/// monsters about. Pure. No world objects.
/// </summary>
public static class DungeonReturnRules
{
    /// <summary>A spot is quiet when no monster stands this close.</summary>
    public const int QuietTiles = 8;

    /// <summary>The nearest rooms of the floor looked at, besides the save-time tile.</summary>
    public const int MaxRoomLooks = 12;

    public const int NoSpot = -1;

    /// <summary>
    /// The index of the spot to come back on, from spots in order of preference (the
    /// save-time tile first, then rooms nearest first), or <see cref="NoSpot"/> when none is
    /// standable.
    /// </summary>
    public static int Pick(IReadOnlyList<ReturnSpot> spots)
    {
        var best = NoSpot;

        for (var i = 0; i < (spots?.Count ?? 0); i++)
        {
            if (!spots[i].Standable)
            {
                continue;
            }

            if (spots[i].Hostiles == 0)
            {
                return i;
            }

            if (best == NoSpot || spots[i].Hostiles < spots[best].Hostiles)
            {
                best = i;
            }
        }

        return best;
    }

    /// <summary>The line a return to a dungeon after a restart writes, easy to count.</summary>
    public static string LoggedBackInLine(string name, string dungeon, int level) =>
        $"{name} logged back in inside {dungeon} level {level}";
}
