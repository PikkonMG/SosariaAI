using Server;

namespace SosariaAI.Mobiles;

/// <summary>
/// The bodies on a tile a person steps to. A character shoves past any body
/// (<see cref="SosariaCharacter.CheckShove"/>), so a body never stops its walk, but a person
/// who steps aside to stand somewhere picks a tile nobody stands on, as a player does. A
/// ghost, a dead bonded pet and a hidden staff member take no room. World thread only.
/// </summary>
public static class StepBodies
{
    /// <summary>A body takes the room of a person landing on its tile within this height, as the engine's step.</summary>
    public const int MeetBand = 15;

    /// <summary>True when a body standing at <paramref name="bodyZ"/> takes the room of a person landing at <paramref name="z"/>.</summary>
    public static bool MeetsHeight(int bodyZ, int z) => bodyZ + MeetBand > z && z + MeetBand > bodyZ;

    /// <summary>True when a body other than <paramref name="person"/> stands on the tile at that height.</summary>
    public static bool Occupied(Map map, Mobile person, int x, int y, int z)
    {
        if (map == null || map == Map.Internal)
        {
            return false;
        }

        foreach (var body in map.GetMobilesAt(x, y))
        {
            if (body != person && TakesRoom(body) && MeetsHeight(body.Z, z))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TakesRoom(Mobile body) =>
        body.Alive && !body.IsDeadBondedPet && !(body.Hidden && body.AccessLevel > AccessLevel.Player);
}
