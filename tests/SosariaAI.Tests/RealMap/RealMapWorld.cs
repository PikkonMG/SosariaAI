using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Server;
using Server.Json;
using Server.Movement;
using Server.Regions;
using SosariaAI.Admin;
using SosariaAI.Combat;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using SosariaAI.Spawning;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// The real Felucca map in a test process, so nav code runs on the actual tiles instead of
/// guessed ones. It loads only what the walker reads: the client's tile data, the maps of
/// the server's map definitions (tiles load when read), the engine's movement check, and
/// Felucca's dungeon regions (the roof test skips them). World items (doors, decorations,
/// house multis) are not loaded.
///
/// The client folder is the <see cref="DataEnvironmentVariable"/> environment variable
/// when set, else the first <c>dataDirectories</c> entry of the sibling ModernUO's
/// <c>Distribution/Configuration/modernuo.json</c>. Server data (map sizes, regions,
/// teleporters, spawns, locations) and the saved nav graph and catalog are read from that sibling
/// Distribution folder. Nothing is written. When a folder or file is missing,
/// <see cref="Available"/> is false and real-map tests skip.
/// </summary>
internal static class RealMapWorld
{
    public const string DataEnvironmentVariable = "SOSARIAAI_UO_DATA";
    public const string FacetName = "Felucca";

    private const string TileDataFile = "tiledata.mul";
    private const string ServerConfigFile = "Configuration/modernuo.json";
    private const string DataDirectoriesKey = "dataDirectories";
    private const string MapDefinitionsFile = "map-definitions.json";
    public const string RegionsFile = "regions.json";
    public const string DungeonRegionType = "DungeonRegion";
    public const string TownRegionType = "TownRegion";
    private const string MoongateRegion = "Moongates";
    private const string ServerDataFolder = "Data";
    private const string NavFolder = "Configuration/sosariaai/nav";
    private const string TileDataLoadMethod = "Load";
    private const string LiveGraphFile = "felucca.json";
    private const string NavWorldGraphsField = "Graphs";

    /// <summary>The item graphic of ModernUO's teleporter pad; its tile data gives the pad's height.</summary>
    private const int TeleporterItemId = 0x1BC3;

    /// <summary>From this file, tests/SosariaAI.Tests/RealMap, up to the folder holding both repositories.</summary>
    private const string ReposRootFromHere = "../../../..";

    private const string DistributionFromRoot = "ModernUO/Distribution";

    private static readonly Lazy<DataPaths> Paths = new(FindPaths);
    private static readonly Lazy<Map> Loaded = new(Load);

    /// <summary>
    /// True when the client data and the ModernUO Distribution folder are there. Only the
    /// files are checked; nothing loads until a test asks for the map.
    /// </summary>
    public static bool Available => Paths.Value.Missing == null;

    /// <summary>Why the real map cannot load, or null when it can.</summary>
    public static string SkipReason => Paths.Value.Missing;

    /// <summary>The real Felucca, loaded on first use.</summary>
    public static Map Felucca => Loaded.Value;

    /// <summary>ModernUO's <c>Distribution/Data</c>: regions, teleporters, spawns, locations.</summary>
    public static string ServerDataRoot => Paths.Value.ServerData;

    /// <summary>The live nav folder: the saved graph and destination catalog.</summary>
    public static string NavRoot => Paths.Value.Nav;

    /// <summary>The live map's walker. The movement check is set again first, since a unit test may swap it.</summary>
    public static TileWalker Walker()
    {
        var map = Felucca;
        MovementImpl.Configure();
        return Standable.Walker(map);
    }

    /// <summary>The live roof test on the real map.</summary>
    public static bool IsIndoor(int x, int y, int z) => IndoorTiles.IsBuilding(Felucca, x, y, z);

    /// <summary>The live server's saved Felucca nav graph, its roofs marked as the boot marks them.</summary>
    public static NavGraph LiveGraph()
    {
        Walker();
        var graph = NavStore.FromFile(JsonConfig.Deserialize<NavFile>(Path.Combine(NavRoot, LiveGraphFile)), FacetName);
        NavWorld.MarkIndoor(graph, IsIndoor);
        return graph;
    }

    /// <summary>How tall a teleporter pad stands, from the client's tile data.</summary>
    public static int TeleporterPadHeight => TileData.ItemTable[TeleporterItemId].Height;

    /// <summary>The height of each Felucca teleporter pad in the server data, by its tile.</summary>
    public static Dictionary<(int X, int Y), int> DataPads()
    {
        var pads = new Dictionary<(int X, int Y), int>();
        var json = File.ReadAllText(Path.Combine(ServerDataRoot, WorldGenerator.TeleportersFileName));

        foreach (var link in WorldDataSeeds.ParseTeleporters(json).Where(link => FacetName.Equals(link.SrcMap, StringComparison.OrdinalIgnoreCase)))
        {
            pads.TryAdd((link.Sx, link.Sy), link.Sz);
        }

        return pads;
    }

    /// <summary>
    /// Drops the gates of the pads the server data lays that no step sets off, and the pads into
    /// the places they sealed, as the boot does with the world's own pads (<see cref="DeadPads"/>).
    /// A dead pad the boot lays level with its floor (<see cref="TeleporterRepair"/>) fires and keeps its gate.
    /// </summary>
    public static (int Dead, int IntoSealed) DropDeadDataPads(IReadOnlyCollection<NavNode> nodes, TileWalker walker)
    {
        var pads = DataPads();
        return DeadPads.Drop(
            nodes,
            node => pads.TryGetValue((node.X, node.Y), out var padZ) &&
                    GatePad.NeverFires(walker, LaidLevel(walker, new Point3D(node.X, node.Y, padZ)), TeleporterPadHeight)
        );
    }

    /// <summary>A data pad where the boot leaves it: laid level with its floor when no step sets it off (<see cref="GatePad.FiringZ"/>).</summary>
    public static Point3D LaidLevel(TileWalker walker, Point3D pad) =>
        GatePad.FiringZ(walker, pad, TeleporterPadHeight) is { } z ? new Point3D(pad.X, pad.Y, z) : pad;

    /// <summary>Runs with the live Felucca graph registered, as the boot has it, and takes it away after.</summary>
    public static void WithLiveGraph(Action test)
    {
        var graphs = (Dictionary<string, NavGraph>)typeof(NavWorld)
            .GetField(NavWorldGraphsField, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        graphs[FacetName] = LiveGraph();
        HomeSpotRules.ResetRoutableCache();

        try
        {
            test();
        }
        finally
        {
            graphs.Remove(FacetName);
            HomeSpotRules.ResetRoutableCache();
        }
    }

    private static DataPaths FindPaths()
    {
        var distribution = Path.GetFullPath(Path.Combine(SourceFolder(), ReposRootFromHere, DistributionFromRoot));
        var serverData = Path.Combine(distribution, ServerDataFolder);
        var clientData = Environment.GetEnvironmentVariable(DataEnvironmentVariable) ??
                         FirstDataDirectory(Path.Combine(distribution, ServerConfigFile));
        var missing = clientData == null || !File.Exists(Path.Combine(clientData, TileDataFile))
            ? $"No UO client data: set {DataEnvironmentVariable} or {ServerConfigFile} {DataDirectoriesKey}."
            : !File.Exists(Path.Combine(serverData, MapDefinitionsFile))
                ? $"No ModernUO Distribution/Data at {serverData}."
                : null;
        return new DataPaths(clientData, serverData, Path.Combine(distribution, NavFolder), missing);
    }

    private static Map Load()
    {
        var paths = Paths.Value;

        if (paths.Missing != null)
        {
            throw new InvalidOperationException(paths.Missing);
        }

        Core.ApplicationAssembly ??= Assembly.GetExecutingAssembly();
        ServerConfiguration.Load(true);
        ServerConfiguration.DataDirectories.Add(paths.ClientData);
        TestMap.EnsureInternal();

        // TileData's static constructor skips the load under xUnit, so load it directly.
        if (TileData.MaxItemValue == 0)
        {
            typeof(TileData).GetMethod(TileDataLoadMethod, BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, null);
        }

        var felucca = RegisterMaps(Path.Combine(paths.ServerData, MapDefinitionsFile));
        RegisterDungeons(felucca, Path.Combine(paths.ServerData, RegionsFile));
        return felucca;
    }

    /// <summary>
    /// Registers every map the definitions list, as the server does, and returns Felucca.
    /// Content reads other maps in its static data (the public moongate list reads Trammel),
    /// and a map's tiles load only when read.
    /// </summary>
    private static Map RegisterMaps(string definitionsPath)
    {
        using var definitions = JsonDocument.Parse(File.ReadAllText(definitionsPath));
        Map felucca = null;

        foreach (var definition in definitions.RootElement.EnumerateArray())
        {
            var index = definition.GetProperty("index").GetInt32();
            var name = definition.GetProperty("name").GetString();

            if (Map.Maps[index] == null)
            {
                var map = new Map(
                    definition.GetProperty("id").GetInt32(),
                    index,
                    definition.GetProperty("fileIndex").GetInt32(),
                    definition.GetProperty("width").GetInt32(),
                    definition.GetProperty("height").GetInt32(),
                    definition.GetProperty("season").GetInt32(),
                    name,
                    Enum.Parse<MapRules>(definition.GetProperty("rules").GetString()!)
                );
                Map.Maps[index] = map;
                Map.AllMaps.Add(map);
            }

            if (FacetName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                felucca = Map.Maps[index];
            }
        }

        return felucca ?? throw new InvalidOperationException($"No {FacetName} in {definitionsPath}.");
    }

    /// <summary>Felucca's dungeon regions with their areas and entrances, as the server loads them.</summary>
    private static void RegisterDungeons(Map felucca, string regionsPath)
    {
        foreach (var region in ReadRegions(regionsPath, DungeonRegionType))
        {
            new DungeonRegion(region.Name, felucca, region.Priority, [.. region.Areas]) { Entrance = region.Entrance }
                .Register();
        }
    }

    /// <summary>
    /// The place book the live world builds, from the server's own region, teleporter and
    /// Second Age spawn files: the dungeon regions and the top-level towns.
    /// </summary>
    public static PlaceBook PlaceBook()
    {
        var regionsPath = Path.Combine(ServerDataRoot, RegionsFile);
        var towns = ReadRegions(regionsPath, TownRegionType);
        var townNames = towns.Select(town => town.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var regions = ReadRegions(regionsPath, DungeonRegionType)
            .Select(dungeon => new PlaceRegion(dungeon.Name, false, dungeon.Areas, dungeon.Entrance, dungeon.GoLocation))
            .Concat(
                towns
                    .Where(town => town.Parent == null || !townNames.Contains(town.Parent))
                    .Select(town => new PlaceRegion(town.Name, true, town.Areas, town.Entrance, town.GoLocation))
            )
            .ToList();
        return PlaceData.Read(regions, ServerDataRoot, FacetName, WorldSetupRules.T2ASpawnSets, Expansion.T2A);
    }

    /// <summary>
    /// The server's guarded towns on Felucca, each with its own guard switch, and the shared
    /// moongate region with its guards off, as <see cref="SosariaAI.Behaviour.MoongateGuards"/> leaves it at boot.
    /// </summary>
    public static List<Region> RegisterGuards(Map map)
    {
        var path = Path.Combine(ServerDataRoot, RegionsFile);
        var regions = new List<Region>();

        foreach (var type in new[] { TownRegionType, nameof(GuardedRegion) })
        {
            foreach (var data in ReadRegions(path, type))
            {
                var region = new GuardedRegion(data.Name, map, data.Priority, [.. data.Areas])
                {
                    GuardsDisabled = data.Name == MoongateRegion || PkRules.InBuccaneersDen(data.GoLocation.X, data.GoLocation.Y)
                };
                region.Register();
                regions.Add(region);
            }
        }

        return regions;
    }

    /// <summary>Felucca's named regions of one type in the server's region file: name, parent, areas and spots.</summary>
    public static List<RegionData> ReadRegions(string regionsPath, string type)
    {
        using var regions = JsonDocument.Parse(File.ReadAllText(regionsPath));
        var found = new List<RegionData>();

        foreach (var region in regions.RootElement.EnumerateArray())
        {
            if (region.GetProperty("$type").GetString() != type ||
                !FacetName.Equals(region.GetProperty("Map").GetString(), StringComparison.OrdinalIgnoreCase) ||
                !region.TryGetProperty("Name", out var name))
            {
                continue;
            }

            var areas = new List<Rectangle3D>();

            foreach (var area in region.GetProperty("Area").EnumerateArray())
            {
                areas.Add(
                    new Rectangle3D(
                        new Point3D(area.GetProperty("x1").GetInt32(), area.GetProperty("y1").GetInt32(), Region.MinZ),
                        new Point3D(area.GetProperty("x2").GetInt32(), area.GetProperty("y2").GetInt32(), Region.MaxZ)
                    )
                );
            }

            found.Add(
                new RegionData(
                    name.GetString(),
                    region.TryGetProperty("Parent", out var parent) ? parent.GetProperty("Name").GetString() : null,
                    region.TryGetProperty("Priority", out var priority) ? priority.GetInt32() : Region.DefaultPriority,
                    areas,
                    PointOf(region, "Entrance"),
                    PointOf(region, "GoLocation")
                )
            );
        }

        return found;
    }

    private static Point3D PointOf(JsonElement region, string property) =>
        region.TryGetProperty(property, out var point)
            ? new Point3D(point.GetProperty("x").GetInt32(), point.GetProperty("y").GetInt32(), point.GetProperty("z").GetInt32())
            : Point3D.Zero;

    private static string FirstDataDirectory(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return null;
        }

        using var config = JsonDocument.Parse(File.ReadAllText(configPath));

        if (!config.RootElement.TryGetProperty(DataDirectoriesKey, out var directories))
        {
            return null;
        }

        foreach (var directory in directories.EnumerateArray())
        {
            return directory.GetString();
        }

        return null;
    }

    private static string SourceFolder([CallerFilePath] string path = "") => Path.GetDirectoryName(path);

    /// <summary>One region of the server's region file.</summary>
    public sealed record RegionData(string Name, string Parent, int Priority, List<Rectangle3D> Areas, Point3D Entrance, Point3D GoLocation);

    private sealed record DataPaths(string ClientData, string ServerData, string Nav, string Missing);
}
