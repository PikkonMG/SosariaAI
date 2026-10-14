using System.Collections.Generic;
using Server;
using Server.Regions;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;

namespace SosariaAI.Behaviour;

/// <summary>
/// World side of <see cref="DirectionRules"/>: a character tells a player the way to the place
/// the player named (<see cref="SpeechIntent.AskedPlace"/>). Everything comes from the map the
/// character stands on: that map's destination catalog, its public moongates, its dungeon
/// doors. A place on another map is never in that catalog, so no answer points across facets.
/// A dungeon hall is told by its door outside, never by the hall's own spot, which lies far off
/// in the dungeon part of the map. Main thread: it reads regions.
/// </summary>
public static class Directions
{
    /// <summary>The answer line, or null when the place is unknown here or too far to tell; the caller then shrugs.</summary>
    public static string LineFor(SosariaCharacter character, string askedPlace, int seed)
    {
        var map = character?.Map;

        if (map == null || map == Map.Internal)
        {
            return null;
        }

        var moongates = MoongateSeeds.LocationsFor(map.Name);

        if (TargetOf(map, askedPlace, character.Location, moongates) is not { } target)
        {
            return null;
        }

        var answer = DirectionRules.Answer(
            character.Location,
            target,
            moongates,
            character.Region?.IsPartOf<DungeonRegion>() == true
        );

        return DirectionRules.Line(answer, seed);
    }

    /// <summary>Where the asked place is met on this map: a catalog place, or the nearest public moongate; null for neither.</summary>
    private static Point3D? TargetOf(Map map, string askedPlace, Point3D from, IReadOnlyList<Point3D> moongates)
    {
        if (PlaceLookup.Resolve(NavWorld.DestinationsFor(map.Name), askedPlace, from) is { } place)
        {
            return OutsideSpot(map, place.Arrival);
        }

        return moongates.Count > 0 && DirectionRules.NamesMoongate(askedPlace) ? DirectionRules.Nearest(from, moongates) : null;
    }

    /// <summary>Where a place is met from outside: its own spot, or for a spot inside a dungeon that dungeon's door; null when no door is known.</summary>
    private static Point3D? OutsideSpot(Map map, Point3D spot)
    {
        if (Region.Find(spot, map)?.IsPartOf<DungeonRegion>() != true)
        {
            return spot;
        }

        var door = DungeonGround.DoorOutOf(map, spot);
        return door == Point3D.Zero ? null : door;
    }
}
