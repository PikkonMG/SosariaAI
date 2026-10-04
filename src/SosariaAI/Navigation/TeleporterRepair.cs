using System.Collections.Generic;
using Server;
using Server.Items;

namespace SosariaAI.Navigation;

/// <summary>
/// Lays the world's dead teleporter pads level with the floor a step onto them lands on
/// (<see cref="GatePad.FiringZ"/>), so the engine sets them off for walkers and players alike.
/// The server data lays some pads a tile or two under their floor, and no step ever fired
/// them: the pad off the south Jhelom island at (1406,3996), the Shame exits at (5686,385..387).
/// The nav check then dropped their gates, and the places behind them had no way out.
/// World thread only: it reads the map and moves items.
/// </summary>
public static class TeleporterRepair
{
    /// <summary>Lays every dead active pad on <paramref name="map"/> at the height it fires at. Returns how many moved.</summary>
    public static int LevelDeadPads(Map map, TileWalker walker)
    {
        if (map == null || map == Map.Internal || walker == null)
        {
            return 0;
        }

        var dead = new List<(Teleporter Pad, int Z)>();

        foreach (var item in World.Items.Values)
        {
            if (item is Teleporter { Deleted: false, Active: true, Parent: null } pad && pad.Map == map &&
                GatePad.FiringZ(walker, pad.Location, pad.ItemData.Height) is { } z)
            {
                dead.Add((pad, z));
            }
        }

        // Moved after the scan: a pad's move changes the map the scan reads.
        foreach (var (pad, z) in dead)
        {
            pad.Z = z;
        }

        if (dead.Count > 0)
        {
            TrapTiles.Reread();
        }

        return dead.Count;
    }
}
