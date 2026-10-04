using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Server;
using Server.Engines.Harvest;
using Server.Logging;
using SosariaAI.Admin;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Navigation.Generation;
using SosariaAI.Logging;

namespace SosariaAI.Navigation;

public static class NavWorld
{
    private const string DataFolder = "Data";
    private const string ModernUOFolder = "ModernUO";
    private const string DistributionFolder = "Distribution";
    private const string EmptyGraphReason = "generation failed or produced no nodes";
    private const string NoMapReason = "no such map";
    public const int LeftoverPiecesToLog = 5;

    private static readonly ILogger logger = SosariaLog.For(typeof(NavWorld));
    private static readonly ILogger console = SosariaLog.Console(typeof(NavWorld));
    private static readonly Dictionary<string, NavGraph> Graphs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, DestinationCatalog> Catalogs = new(StringComparer.OrdinalIgnoreCase);
    private static bool _configured;

    /// <summary>
    /// Builds every facet's graph again from the world as it stands. False, and nothing
    /// changes, while the world waits for First Time Setup (<see cref="NavBootRules"/>).
    /// </summary>
    public static bool Rebuild()
    {
        if (WorldSetup.Waiting)
        {
            console.Information(NavBootRules.RebuildWaitsLine);
            return false;
        }

        Configure(forceRebuild: true, worldWaits: false, reload: false);
        return true;
    }

    /// <summary>
    /// Called from SosariaSettings after characters.json is loaded. Not named
    /// Configure: ModernUO invokes every public static Configure, and that
    /// would build each facet twice.
    /// </summary>
    internal static void Load() => Configure(forceRebuild: false, WorldSetup.AwaitsSetup(), reload: false);

    /// <summary>
    /// First Time Setup just placed the world's items. The saved graphs load again and are
    /// checked against those items (floors, teleporter pads, the dead pads laid level first:
    /// <see cref="TeleporterRepair"/>), which takes about a second; a
    /// facet with no saved graph or catalog is built, and with <c>nav.rebuildOnBoot</c> every
    /// facet is built (<see cref="NavBootRules"/>). The trap and pad read from the empty world is dropped first.
    /// </summary>
    public static void CheckAfterSetup()
    {
        TrapTiles.Reread();
        Configure(forceRebuild: false, worldWaits: false, reload: true);
    }

    private static void Configure(bool forceRebuild, bool worldWaits, bool reload)
    {
        if (_configured && !forceRebuild && !reload)
        {
            return;
        }

        _configured = true;
        Graphs.Clear();
        Catalogs.Clear();
        var characters = SosariaSettings.Characters;
        var rebuild = NavBootRules.Rebuilds(forceRebuild, characters.Nav is { RebuildOnBoot: true }, worldWaits);
        var dataRoot = ResolveDataRoot();

        if (worldWaits)
        {
            console.Information(NavBootRules.WaitsForSetupLine);
        }

        foreach (var pair in characters.Maps)
        {
            if (pair.Value is not { Enabled: true } || string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            ConfigureFacet(pair.Key, rebuild, worldWaits, dataRoot);
        }

        WarmRedBars();
    }

    /// <summary>
    /// Reads the guarded roads reds keep off on the graph just loaded (<see cref="PathSearchBars.WarmFelucca"/>),
    /// at boot and after every check or rebuild: a new graph starts cold, and the first red plan
    /// on it paid the read on the world thread.
    /// </summary>
    private static void WarmRedBars()
    {
        var (nodes, legs) = PathSearchBars.WarmFelucca();
        logger.Information("Felucca roads: {Nodes} nodes and {Legs} legs lie under the guards; reds keep off them", nodes, legs);
    }

    /// <summary>
    /// The graph of this facet, or null when it has none. A blank facet means Felucca.
    /// Another facet's graph is never a substitute: its coordinates are on another map.
    /// </summary>
    public static NavGraph GraphFor(string facet) => Graphs.GetValueOrDefault(FacetOrDefault(facet));

    /// <summary>
    /// Marks the nodes under a roof. Cheap: one tile test per node and no ground walk.
    /// The graph's component cache is dropped, because search no longer passes through
    /// the nodes just marked.
    /// </summary>
    public static void MarkIndoor(NavGraph graph, Func<int, int, int, bool> isIndoor)
    {
        if (graph == null)
        {
            return;
        }

        IndoorRoute.Mark(new List<NavNode>(graph.Nodes), isIndoor);
        graph.InvalidateComponents();
    }

    public static DestinationCatalog DestinationsFor(string facet) =>
        Catalogs.GetValueOrDefault(FacetOrDefault(facet));

    private static string FacetOrDefault(string facet) =>
        string.IsNullOrWhiteSpace(facet) ? FacetNames.Felucca : facet;

    /// <param name="worldWaits">
    /// The world waits for First Time Setup: a saved graph loads as it is, since the checks
    /// against the world's items would judge it on bare ground, and none is generated.
    /// </param>
    private static void ConfigureFacet(
        string facet,
        bool rebuild,
        bool worldWaits,
        string dataRoot
    )
    {
        var timer = Stopwatch.StartNew();
        NavGraph graph = null;
        DestinationCatalog catalog = null;
        var loaded = false;

        if (!Map.TryParse(facet, null, out var map) || map == null)
        {
            logger.Warning("Nav {Facet}: {Reason}", facet, NoMapReason);
            return;
        }

        // Reads the map, so the build runs on the world thread.
        var walker = Standable.Walker(map);

        // A dead pad laid level fires, so the pad check below keeps its gate.
        if (!worldWaits && TeleporterRepair.LevelDeadPads(map, walker) is var leveled and > 0)
        {
            logger.Warning("Nav {Facet}: laid {Count} teleporter pads level with their floor so a step sets them off", facet, leveled);
        }

        if (!rebuild &&
            NavStore.TryLoad(facet, out graph) &&
            NavStore.TryLoadDestinations(facet, out catalog))
        {
            loaded = true;
        }
        else if (!worldWaits)
        {
            try
            {
                if (!WorldGenerator.TryGenerate(
                        facet,
                        dataRoot,
                        WorldSetupRules.SpawnSets(EraBands.Current()),
                        walker,
                        out graph,
                        out catalog,
                        CreatureStatsLookup.TryLive,
                        (x, y, z) => LiveResourceHit(facet, x, y, z),
                        LiveGroundZ(facet),
                        LiveIndoor(facet),
                        CreatureStatsLookup.IsLandEnemy
                    ) ||
                    graph is not { NodeCount: > 0 })
                {
                    logger.Warning("Nav {Facet}: {Reason}", facet, EmptyGraphReason);
                    graph = null;
                }
            }
            catch (Exception e)
            {
                logger.Warning("Nav {Facet}: {Reason}", facet, e.Message);
                graph = null;
            }
        }

        if (graph == null)
        {
            timer.Stop();
            LogSummary(facet, null, timer.ElapsedMilliseconds);
            return;
        }

        catalog ??= new DestinationCatalog([]);

        if (loaded)
        {
            MarkIndoor(graph, LiveIndoor(facet));

            if (!worldWaits)
            {
                graph = CheckedAgainstTheWorld(graph, facet, map, walker);
            }
        }
        else
        {
            // The generator already cut the roof shortcuts and rejoined the pieces.
            graph = WithLivePads(Settled(graph, walker.FloorNear, out _), map, walker, out _);
        }

        Graphs[facet] = graph;

        // Hunt spots are rated by the floor or the ground they stand on, a saved catalog too.
        DungeonAtlas.Rate(facet, catalog);

        if (!loaded)
        {
            NavStore.Save(graph);
            NavStore.SaveDestinations(facet, catalog);
        }

        Catalogs[facet] = LivePlaces(map, catalog);
        timer.Stop();
        LogSummary(facet, graph, timer.ElapsedMilliseconds);
    }

    /// <summary>
    /// A saved graph checked against the world as it stands: its nodes' floors and the
    /// world's teleporter pads. Saved again when anything changed.
    /// </summary>
    private static NavGraph CheckedAgainstTheWorld(NavGraph graph, string facet, Map map, TileWalker walker)
    {
        // A saved node can sit a floor off the surface at its tile, and a walk to it
        // then never ends. Moving it onto the floor needs no rebuild.
        graph = Settled(graph, walker.FloorNear, out var moved);

        if (moved > 0)
        {
            logger.Warning("Nav {Facet}: moved {Count} saved nodes onto their floor", facet, moved);
        }

        graph = WithLivePads(graph, map, walker, out var padChanges);

        if (moved > 0 || padChanges > 0)
        {
            NavStore.Save(graph);
        }

        return graph;
    }

    /// <summary>
    /// The graph with every node on the floor the walker finds at its tile, nearest the
    /// height the node has. A node off its floor is a goal the game pathfinder files on
    /// another layer and never reaches.
    /// </summary>
    private static NavGraph Settled(NavGraph graph, Func<int, int, int, int?> surface, out int moved)
    {
        moved = NodeHeight.Settle(graph.Nodes, surface);
        return moved > 0 ? new NavGraph(graph.Facet, graph.Nodes) : graph;
    }

    /// <summary>
    /// The graph without the teleporter gates whose pad no walker's step sets off
    /// (<see cref="GatePad.NeverFires(Map, TileWalker, int, int)"/>), nor the pads into a place
    /// such a pad was the only way out of (<see cref="DeadPads"/>), nor the walking links that
    /// step onto a live pad on the way, and with a walk off the pads for every landing
    /// (<see cref="PadCrossings"/>). The pads are the world's own items, so a saved graph is
    /// checked too and needs no rebuild.
    /// </summary>
    private static NavGraph WithLivePads(NavGraph graph, Map map, TileWalker walker, out int changed)
    {
        var (dead, intoSealed) = DeadPads.Drop(graph.Nodes, node => GatePad.NeverFires(map, walker, node.X, node.Y));

        if (dead + intoSealed > 0)
        {
            logger.Warning(
                "Nav {Facet}: dropped {Dead} teleporter gates whose pad never fires and {Sealed} into places they left with no way out",
                graph.Facet,
                dead,
                intoSealed
            );
        }

        var nodes = new List<NavNode>(graph.Nodes);
        var (crossing, relinked) = PadCrossings.Drop(nodes, walker, LiveIndoor(graph.Facet));

        if (crossing + relinked > 0)
        {
            logger.Warning(
                "Nav {Facet}: dropped {Crossing} walking links that step onto a teleporter pad and relinked {Relinked} nodes they left",
                graph.Facet,
                crossing,
                relinked
            );
        }

        changed = dead + intoSealed + crossing + relinked;
        return changed == 0 ? graph : new NavGraph(graph.Facet, nodes);
    }

    private static Func<int, int, int, bool> LiveIndoor(string facet) =>
        (x, y, z) =>
        {
            try
            {
                return Map.TryParse(facet, null, out var map) && IndoorTiles.IsBuilding(map, x, y, z);
            }
            catch
            {
                return false;
            }
        };

    private static Func<int, int, int> LiveGroundZ(string facet) =>
        (x, y) =>
        {
            try
            {
                return Map.TryParse(facet, null, out var map) && Standable.TryFindGround(map, x, y, out var z) ? z : 0;
            }
            catch
            {
                return 0;
            }
        };

    private static ResourceHit LiveResourceHit(string facet, int x, int y, int z)
    {
        try
        {
            if (!Map.TryParse(facet, null, out var map) || map == null)
            {
                return ResourceHit.None;
            }

            var land = map.Tiles.GetLandTile(x, y);

            if (IsOre(land.ID, isLand: true))
            {
                return ResourceHit.Ore;
            }

            foreach (var tile in map.Tiles.GetStaticTiles(x, y))
            {
                if (!NavMetric.SameFloor(tile.Z, z))
                {
                    continue;
                }

                if (IsTree(tile.ID))
                {
                    return ResourceHit.Tree;
                }

                if (IsOre(tile.ID, isLand: false))
                {
                    return ResourceHit.Ore;
                }
            }
        }
        catch
        {
            return ResourceHit.None;
        }

        return ResourceHit.None;
    }

    private static bool IsTree(int tileId) => MatchesHarvest(Lumberjacking.System, tileId, isLand: false);

    private static bool IsOre(int tileId, bool isLand) => MatchesHarvest(Mining.System, tileId, isLand);

    private static bool MatchesHarvest(HarvestSystem system, int tileId, bool isLand)
    {
        var definitions = system?.Definitions;

        if (definitions == null)
        {
            return false;
        }

        for (var i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].Validate(tileId, isLand))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The catalog without the places this era's world does not have
    /// (<see cref="WorldPlaces"/>). A saved catalog is filtered too, so no rebuild is needed.
    /// </summary>
    private static DestinationCatalog LivePlaces(Map map, DestinationCatalog catalog)
    {
        var book = WorldPlaces.For(map);
        var kept = book.Filter(catalog);

        if (!ReferenceEquals(kept, catalog))
        {
            logger.Information(
                "Nav {Facet}: {Count} destinations left out in {Places} places this world does not have",
                map.Name,
                catalog.All.Count - kept.All.Count,
                book.Dropped.Count
            );
        }

        return kept;
    }

    /// <summary>The server's data folder: regions, teleporters, spawns and locations.</summary>
    public static string ResolveDataRoot()
    {
        var baseDir = Core.BaseDirectory ?? string.Empty;
        var direct = Path.Combine(baseDir, DataFolder);

        if (Directory.Exists(direct))
        {
            return direct;
        }

        var distribution = Path.Combine(baseDir, ModernUOFolder, DistributionFolder, DataFolder);
        return Directory.Exists(distribution) ? distribution : direct;
    }

    private static void LogSummary(string facet, NavGraph graph, long elapsedMs)
    {
        var components = graph == null ? 0 : CountComponents(graph);
        var largestShare = graph == null ? 0 : LargestComponentShare(graph);
        console.Information(
            "Nav {Facet}: {Nodes} nodes, {Edges} edges, {Components} components ({Largest:P0} in the largest) in {Elapsed} ms",
            facet,
            graph?.NodeCount ?? 0,
            graph == null ? 0 : CountEdges(graph),
            components,
            largestShare,
            elapsedMs
        );

        if (graph == null)
        {
            return;
        }

        var leftovers = LeftoverPieces(graph, LeftoverPiecesToLog);

        for (var i = 0; i < leftovers.Count; i++)
        {
            var piece = leftovers[i];
            logger.Information(
                "Nav {Facet} leftover: {Count} nodes around ({X},{Y},{Z})",
                facet,
                piece.Count,
                piece.Sample.X,
                piece.Sample.Y,
                piece.Sample.Z
            );
        }

        var notes = graph.CountValidationNotes();

        if (notes > 0)
        {
            logger.Information("Nav {Facet}: {Count} validation notes", facet, notes);
        }
    }

    public static IReadOnlyList<(int Count, Point3D Sample)> LeftoverPieces(NavGraph graph, int take)
    {
        if (graph == null || graph.NodeCount == 0 || take <= 0)
        {
            return [];
        }

        var pieces = new Dictionary<int, (int Count, Point3D Sample)>();

        foreach (var node in graph.Nodes)
        {
            var id = graph.ComponentOf(node.Name);

            if (pieces.TryGetValue(id, out var existing))
            {
                pieces[id] = (existing.Count + 1, existing.Sample);
            }
            else
            {
                pieces[id] = (1, node.Location);
            }
        }

        if (pieces.Count <= 1)
        {
            return [];
        }

        var ordered = new List<(int Count, Point3D Sample)>(pieces.Count);

        foreach (var piece in pieces.Values)
        {
            ordered.Add(piece);
        }

        ordered.Sort(static (a, b) => b.Count.CompareTo(a.Count));

        var resultCount = Math.Min(take, ordered.Count - 1);
        var result = new List<(int Count, Point3D Sample)>(resultCount);

        for (var i = 1; i <= resultCount; i++)
        {
            result.Add(ordered[i]);
        }

        return result;
    }

    private static int CountEdges(NavGraph graph)
    {
        var connects = 0;

        foreach (var node in graph.Nodes)
        {
            connects += node.Connects?.Count ?? 0;
        }

        return connects / 2;
    }

    private static int CountComponents(NavGraph graph)
    {
        var ids = new HashSet<int>();

        foreach (var node in graph.Nodes)
        {
            ids.Add(graph.ComponentOf(node.Name));
        }

        return ids.Count;
    }

    private static double LargestComponentShare(NavGraph graph)
    {
        if (graph.NodeCount == 0)
        {
            return 0;
        }

        var sizes = new Dictionary<int, int>();

        foreach (var node in graph.Nodes)
        {
            var id = graph.ComponentOf(node.Name);
            sizes[id] = sizes.GetValueOrDefault(id) + 1;
        }

        var largest = 0;

        foreach (var size in sizes.Values)
        {
            if (size > largest)
            {
                largest = size;
            }
        }

        return (double)largest / graph.NodeCount;
    }
}
