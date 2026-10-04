using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Logging;
using SosariaAI.Admin;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Navigation.Generation;

namespace SosariaAI.Navigation;

/// <summary>
/// The dungeon floors of each facet, built once from the navigation graph, the world's own
/// dungeon regions, its location list (the "Level N" marks) and the spawners of the era's spawn
/// sets, the first time anyone asks. A rebuilt graph builds its floors again. The same spawn
/// rates the catalog's hunt spots at boot (<see cref="Rate"/>). Each build writes one line per
/// dungeon to the activity log with the power each floor asks (<see cref="FloorsLine"/>). The
/// server's data files are only read. Main thread only: it reads regions and makes a few of each
/// creature to read its stats.
/// </summary>
public static class DungeonAtlas
{
    private static readonly ILogger logger = SosariaLog.For(typeof(DungeonAtlas));

    private sealed record Atlas(NavGraph Graph, DungeonMap Map, IReadOnlyList<SpawnPoint> Spawn, Func<Point3D, string> PlaceAt);

    private static readonly Dictionary<string, Atlas> Built = new(StringComparer.OrdinalIgnoreCase);

    public static DungeonMap For(string facet) => AtlasOf(facet)?.Map ?? DungeonMap.Empty;

    /// <summary>
    /// Rates the hunt spots of a facet's catalog in place, by the floor or the ground they stand
    /// on (<see cref="GroundRating.Rate"/>). A saved catalog holds the numbers of whatever rule
    /// saved it, so every boot rates it again. Needs the facet's graph installed.
    /// </summary>
    public static void Rate(string facet, DestinationCatalog catalog)
    {
        if (AtlasOf(facet) is { } atlas)
        {
            GroundRating.Rate(catalog, atlas.Map, atlas.Spawn, atlas.PlaceAt, CreatureStatsLookup.TryLive);
        }
    }

    private static Atlas AtlasOf(string facet)
    {
        var graph = NavWorld.GraphFor(facet);

        if (graph == null)
        {
            return null;
        }

        var key = facet ?? string.Empty;

        if (Built.TryGetValue(key, out var built) && ReferenceEquals(built.Graph, graph))
        {
            return built;
        }

        var name = string.IsNullOrWhiteSpace(facet) ? FacetNames.Felucca : facet;
        var placeAt = DungeonGround.RegionNames(Map.Parse(name));
        var dataRoot = NavWorld.ResolveDataRoot();
        var spawn = GroundRating.Points(
            WorldGenerator.SpawnersFor(dataRoot, name, WorldSetupRules.SpawnSets(EraBands.Current())),
            CreatureStatsLookup.IsLandEnemy,
            placeAt
        );
        var map = DungeonMap.Build(
            graph,
            node => placeAt(node.Location),
            DungeonMap.MarksIn(WorldGenerator.LocationsFor(dataRoot, name)),
            spawn,
            CreatureStatsLookup.TryLive
        );
        var atlas = new Atlas(graph, map, spawn, placeAt);
        Built[key] = atlas;

        foreach (var dungeon in map.Floors.GroupBy(floor => floor.Dungeon, StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key))
        {
            logger.Information("{Line}", FloorsLine(dungeon.Key, dungeon));
        }

        return atlas;
    }

    /// <summary>
    /// "Floors of Deceit: level 1 asks power 104, level 2 asks power 208": each floor with spawn,
    /// shallowest first, and the power a lone fighter needs there (<see cref="HuntGround.PowerBar"/>). Pure.
    /// </summary>
    public static string FloorsLine(string dungeon, IEnumerable<DungeonFloor> floors) =>
        $"Floors of {dungeon}: " + string.Join(
            ", ",
            floors.Where(floor => floor.Difficulty > 0)
                .OrderBy(floor => floor.Level)
                .ThenBy(floor => floor.Difficulty)
                .Select(floor => $"level {floor.Level} asks power {HuntGround.PowerBar(floor.Difficulty)}")
        );
}
