using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Regions;
using SosariaAI.Admin;
using SosariaAI.Behaviour;
using SosariaAI.Logging;

namespace SosariaAI.Navigation;

/// <summary>
/// The towns and dungeons of each live map that belong in this era's world
/// (<see cref="PlaceRules"/>), judged once per map from the engine's own town and dungeon
/// regions, its teleporter data and the era's spawn files. The staff panel, the catalog the
/// characters pick from, the dungeon names of a delve or a party call, and gossip all read
/// it. Main thread only: it reads regions.
/// </summary>
public static class WorldPlaces
{
    private static readonly ILogger logger = SosariaLog.For(typeof(WorldPlaces));
    private static readonly Dictionary<Map, PlaceBook> Books = new();

    public static PlaceBook For(Map map)
    {
        if (map == null || map == Map.Internal)
        {
            return PlaceBook.Empty;
        }

        if (!Books.TryGetValue(map, out var book))
        {
            book = PlaceData.Read(
                Regions(map),
                NavWorld.ResolveDataRoot(),
                map.Name,
                WorldSetupRules.SpawnSets(EraBands.Current()),
                EraRules.Current()
            );
            Books[map] = book;

            foreach (var dropped in book.Dropped)
            {
                logger.Information("Places {Facet}: {Region} is not in this world: a later era, no working way in, or no live spawns", map.Name, dropped.Name);
            }
        }

        return book;
    }

    /// <summary>
    /// The dungeon regions of the map and its top-level towns; a town's fields and quarters
    /// belong to their town.
    /// </summary>
    private static List<PlaceRegion> Regions(Map map)
    {
        var regions = new List<PlaceRegion>();

        foreach (var region in Region.Regions)
        {
            if (region.Map != map || string.IsNullOrWhiteSpace(region.Name))
            {
                continue;
            }

            if (region is DungeonRegion dungeon)
            {
                regions.Add(new PlaceRegion(dungeon.Name, false, dungeon.Area, dungeon.Entrance, dungeon.GoLocation));
            }
            else if (region is TownRegion town && town.Parent is not TownRegion)
            {
                regions.Add(new PlaceRegion(town.Name, true, town.Area, town.Entrance, town.GoLocation));
            }
        }

        return regions;
    }
}
