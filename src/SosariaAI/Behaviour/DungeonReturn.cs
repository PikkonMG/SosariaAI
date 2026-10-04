using System.Collections.Generic;
using Server;
using Server.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>World side of <see cref="DungeonReturnRules"/>: the floor under the save-time tile and the monsters about.</summary>
public static class DungeonReturn
{
    /// <summary>
    /// Where a person that stood at <paramref name="savedAt"/> when the world was saved comes
    /// back into the world on <paramref name="map"/> after a restart, and the floor, when that
    /// tile lies on a dungeon floor. Null outside a dungeon, or when no spot on the floor fits
    /// a person.
    /// </summary>
    public static (Point3D Spot, DungeonFloor Floor)? SpotFor(Map map, Point3D savedAt)
    {
        if (map == null || map == Map.Internal || savedAt == Point3D.Zero ||
            DungeonGround.RegionNames(map)(savedAt) == null ||
            DungeonAtlas.For(map.Name).FloorAt(savedAt) is not { } floor)
        {
            return null;
        }

        var places = new List<Point3D> { savedAt };
        var rooms = new List<NavNode>(DungeonAtlas.For(map.Name).Rooms(floor.Id));
        rooms.Sort((a, b) => NavMetric.Chebyshev(savedAt, a.Location).CompareTo(NavMetric.Chebyshev(savedAt, b.Location)));

        for (var i = 0; i < rooms.Count && i < DungeonReturnRules.MaxRoomLooks; i++)
        {
            places.Add(rooms[i].Location);
        }

        var spots = new List<ReturnSpot>(places.Count);

        for (var i = 0; i < places.Count; i++)
        {
            spots.Add(new ReturnSpot(places[i], map.CanSpawnMobile(places[i]), HostilesNear(map, places[i], DungeonReturnRules.QuietTiles)));
        }

        var pick = DungeonReturnRules.Pick(spots);
        return pick == DungeonReturnRules.NoSpot ? null : (places[pick], floor);
    }

    /// <summary>Wild monsters that fight, alive within <paramref name="range"/> of a spot: no pet, no summon.</summary>
    public static int HostilesNear(Map map, Point3D at, int range)
    {
        var count = 0;

        foreach (var creature in map.GetMobilesInRange<BaseCreature>(at, range))
        {
            if (creature is { Deleted: false, Alive: true, Controlled: false, Summoned: false } &&
                creature.FightMode != FightMode.None)
            {
                count++;
            }
        }

        return count;
    }
}
