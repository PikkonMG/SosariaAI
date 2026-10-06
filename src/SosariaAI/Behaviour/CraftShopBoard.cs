using System.Collections.Generic;
using Server;
using SosariaAI.Mobiles;

namespace SosariaAI.Behaviour;

/// <summary>
/// The crafters standing at their stations with their shop open: the board a fighter reads
/// when it looks for a GM piece, or for a crafter to take its order. A crafter is on it while its
/// station shop is open (<see cref="Skills.StationStall"/>). World thread only.
/// </summary>
public static class CraftShopBoard
{
    private static readonly Dictionary<Serial, SosariaCharacter> Listed = new();

    public static void Note(SosariaCharacter crafter) => Listed[crafter.Serial] = crafter;

    public static void Forget(SosariaCharacter crafter) => Listed.Remove(crafter.Serial);

    /// <summary>
    /// The listed crafters on <paramref name="map"/> within <paramref name="range"/> of
    /// <paramref name="at"/>. The dead and the deleted drop off.
    /// </summary>
    public static List<SosariaCharacter> Near(Map map, Point3D at, int range)
    {
        var found = new List<SosariaCharacter>();
        var gone = new List<Serial>();

        foreach (var (serial, crafter) in Listed)
        {
            if (crafter is not { Deleted: false, Alive: true })
            {
                gone.Add(serial);
            }
            else if (crafter.Map == map && crafter.InRange(at, range))
            {
                found.Add(crafter);
            }
        }

        foreach (var serial in gone)
        {
            Listed.Remove(serial);
        }

        return found;
    }
}
