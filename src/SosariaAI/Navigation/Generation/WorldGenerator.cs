using System;
using System.Collections.Generic;
using System.IO;
using Server;
using SosariaAI.Combat;
using SosariaAI.Configuration;

namespace SosariaAI.Navigation.Generation;

public static class WorldGenerator
{
    private const string LocationsFolder = "Locations";
    private const string SpawnsFolder = "Spawns";
    public const string TeleportersFileName = "teleporters.json";
    private const string RegionsFileName = "regions.json";
    private const string CemeteryToken = "cemetery";
    private const string TownsGroup = "Towns";
    private const int DifficultyScanRange = 48;
    private const int ResourceSpacing = 32;
    private const char GroupSeparator = '/';

    /// <summary>
    /// Kinds that must get a node of their own. A trip to a shrine, a bank, a healer or a
    /// dungeon ends at that exact spot, so its node is placed at its own tile when the
    /// cluster around it stands nowhere.
    /// </summary>
    private static readonly HashSet<string> AnchorKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(DestinationKind.Shrine),
        nameof(DestinationKind.Bank),
        nameof(DestinationKind.Healer),
        nameof(DestinationKind.Dungeon)
    };

    /// <summary>
    /// Builds the walking graph of a facet. Every node stands on the floor the walker
    /// finds at its tile, never on a tile an armed trap hurts, and every walking edge is
    /// walked both ways with the walker's trap-aware step rule before it is kept, so the
    /// graph goes round the traps. No walker, no graph: nothing can be walked.
    /// </summary>
    /// <param name="teleports">The facet's own teleporters: both ends on <paramref name="facet"/>.</param>
    /// <param name="walker">How a walker stands and steps; <see cref="Standable.Walker"/> on a live map.</param>
    /// <param name="groundZ">The ground height at a tile, tried when no seed height stands.</param>
    public static NavGraph BuildGraph(
        string facet,
        IReadOnlyList<GraphSeed> seeds,
        IReadOnlyList<TeleporterLink> teleports,
        TileWalker walker,
        Func<int, int, int> groundZ = null,
        Func<int, int, int, bool> isIndoor = null
    )
    {
        facet ??= string.Empty;
        var nodes = new List<NavNode>();

        if (walker == null)
        {
            return new NavGraph(facet, nodes);
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var clusters = SeedMerge.Merge(seeds ?? []);

        for (var i = 0; i < clusters.Count; i++)
        {
            PlaceCluster(clusters[i], facet, walker.SafeFloorNear, groundZ, isIndoor, nodes, names);
        }

        GraphConnect.Connect(nodes, walker, isIndoor);

        // Teleporters must be linked before the ground is walked. A dungeon interior is a
        // separate island of walkable tiles: nothing reaches it on foot. Once its gate is
        // attached, the walk can start from inside the dungeon and pick up everything in
        // there, which is where most stranded seeds sit.
        TeleporterLinking.Link(nodes, teleports);
        MoongateSeeds.Link(nodes, facet);
        // Pad nodes were placed after the seeds were joined. Give them their street edges.
        GraphConnect.Join(nodes, walker, isIndoor);
        // The roof cuts come before the ground walk, so the roads it lays rejoin the pieces
        // they split off. The catalog is built on this graph, so it must be the final one.
        IndoorRoute.Repair(nodes, isIndoor, walker);
        return new NavGraph(facet, nodes);
    }

    /// <summary>
    /// Adds the node of one seed cluster. When the averaged point stands nowhere, each
    /// anchor seed in the cluster is placed at its own tile instead.
    /// </summary>
    private static void PlaceCluster(
        SeedCluster cluster,
        string facet,
        Func<int, int, int, int?> surfaceAt,
        Func<int, int, int> groundZ,
        Func<int, int, int, bool> isIndoor,
        List<NavNode> nodes,
        HashSet<string> names
    )
    {
        if (SeedMerge.Place(cluster, surfaceAt, groundZ) is { } point)
        {
            AddSeedNode(point, cluster.Head.Kind, facet, isIndoor, nodes, names);
            return;
        }

        for (var i = 0; i < cluster.Members.Count; i++)
        {
            var member = cluster.Members[i];

            if (AnchorKinds.Contains(member.Kind ?? string.Empty) &&
                SeedMerge.Place(SeedCluster.Of(member), surfaceAt, groundZ) is { } own)
            {
                AddSeedNode(own, member.Kind, facet, isIndoor, nodes, names);
            }
        }
    }

    private static void AddSeedNode(
        (int X, int Y, int Z) point,
        string kind,
        string facet,
        Func<int, int, int, bool> isIndoor,
        List<NavNode> nodes,
        HashSet<string> names
    )
    {
        var name = NodeName(facet, point.X, point.Y);

        if (!names.Add(name))
        {
            return;
        }

        nodes.Add(
            new NavNode
            {
                Name = name,
                X = point.X,
                Y = point.Y,
                Z = point.Z,
                ArrivalRange = ArrivalRangeFor(kind),
                Indoor = isIndoor != null && isIndoor(point.X, point.Y, point.Z),
                Connects = []
            }
        );
    }

    /// <param name="locations">The facet's own locations (<see cref="LocationsFor"/>).</param>
    /// <param name="regions">The facet's own regions.</param>
    /// <param name="spawners">The facet's own spawners (<see cref="SpawnersFor"/>).</param>
    /// <param name="walker">How a walker stands and steps; <see cref="Standable.Walker"/> on a live map.</param>
    /// <param name="isEnemy">
    /// Which spawned creatures a fighter hunts. A place's difficulty counts only those: the
    /// ettins' ground read 38 once the rabbits and birds round it were counted as its foes.
    /// Null counts every creature.
    /// </param>
    public static DestinationCatalog BuildDestinations(
        NavGraph graph,
        IReadOnlyList<NamedSeed> locations,
        IReadOnlyList<RegionSeed> regions,
        IReadOnlyList<SpawnerSeed> spawners,
        TileWalker walker,
        Func<string, HostileStats?> creatureLookup = null,
        Func<int, int, int, ResourceHit> resourceProbe = null,
        Func<int, int, int, bool> isIndoor = null,
        Func<string, bool> isEnemy = null
    )
    {
        var drafts = new List<DestinationDraft>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        spawners ??= [];

        AddLocationDrafts(drafts, names, locations, spawners, creatureLookup, isEnemy);
        AddRegionDrafts(drafts, names, regions, spawners, creatureLookup, isEnemy);
        AddSpawnerDrafts(drafts, names, spawners, creatureLookup, isEnemy);
        AddResourceDrafts(drafts, names, graph, resourceProbe);
        return CatalogBuilder.Build(drafts, graph, walker, isIndoor);
    }

    /// <param name="spawnSets">The era's spawn sets, such as "shared" for The Second Age (<see cref="SpawnFolders"/>).</param>
    public static bool TryGenerate(
        string facet,
        string dataRoot,
        IReadOnlyList<string> spawnSets,
        TileWalker walker,
        out NavGraph graph,
        out DestinationCatalog catalog,
        Func<string, HostileStats?> creatureLookup = null,
        Func<int, int, int, ResourceHit> resourceProbe = null,
        Func<int, int, int> groundZ = null,
        Func<int, int, int, bool> isIndoor = null,
        Func<string, bool> isEnemy = null
    )
    {
        graph = null;
        catalog = null;

        if (string.IsNullOrWhiteSpace(facet) || string.IsNullOrWhiteSpace(dataRoot))
        {
            return false;
        }

        // A failure here must reach the caller's log, which names the facet left without a graph.
        var locations = LocationsFor(dataRoot, facet);
        var teleporters = LoadTeleporters(Path.Combine(dataRoot, TeleportersFileName));
        var regions = FilterRegions(
            WorldDataSeeds.ParseRegions(ReadFile(Path.Combine(dataRoot, RegionsFileName))),
            facet
        );
        var spawners = SpawnersFor(dataRoot, facet, spawnSets);
        var seeds = CollectSeeds(locations, regions, spawners);
        seeds.AddRange(TownStreetSeeds.Collect(regions, NodeHeight.Stands(walker?.FloorNear), groundZ, isIndoor));
        var links = SameFacetTeleports(teleporters, facet);
        graph = BuildGraph(facet, seeds, links, walker, groundZ, isIndoor);
        catalog = BuildDestinations(
            graph,
            locations,
            regions,
            spawners,
            walker,
            creatureLookup,
            resourceProbe,
            isIndoor,
            isEnemy
        );
        return graph is { NodeCount: > 0 };
    }

    /// <summary>
    /// Teleporter pads are not seeds. A seed is merged and snapped, and a pad that moved
    /// off its tile teleports a character from bare ground. <see cref="TeleporterLinking"/>
    /// places every pad node on its exact tile after the seeds are connected.
    /// </summary>
    private static List<GraphSeed> CollectSeeds(
        IReadOnlyList<NamedSeed> locations,
        IReadOnlyList<RegionSeed> regions,
        IReadOnlyList<SpawnerSeed> spawners
    )
    {
        var seeds = new List<GraphSeed>();

        for (var i = 0; i < locations.Count; i++)
        {
            var location = locations[i];
            seeds.Add(
                new GraphSeed(
                    location.Name,
                    location.X,
                    location.Y,
                    location.Z,
                    KindFromGroup.From(location.Group, location.Name).ToString()
                )
            );
        }

        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];

            if (!WorldDataSeeds.IsTown(region.Type) && !WorldDataSeeds.IsDungeon(region.Type))
            {
                continue;
            }

            seeds.Add(
                new GraphSeed(
                    region.Name,
                    region.X,
                    region.Y,
                    region.Z,
                    KindFromGroup.From(region.Type, region.Name).ToString()
                )
            );
        }

        for (var i = 0; i < spawners.Count; i++)
        {
            var spawner = spawners[i];
            seeds.Add(
                new GraphSeed(
                    SpawnerSeedName(spawner),
                    spawner.X,
                    spawner.Y,
                    spawner.Z,
                    SpawnerKind(spawner).ToString()
                )
            );
        }

        return seeds;
    }

    private static void AddLocationDrafts(
        List<DestinationDraft> drafts,
        HashSet<string> names,
        IReadOnlyList<NamedSeed> locations,
        IReadOnlyList<SpawnerSeed> spawners,
        Func<string, HostileStats?> creatureLookup,
        Func<string, bool> isEnemy
    )
    {
        if (locations == null)
        {
            return;
        }

        for (var i = 0; i < locations.Count; i++)
        {
            var location = locations[i];
            var kind = KindFromGroup.From(location.Group, location.Name);
            var role = RoleOf(kind, location.Group, location.Name);
            OverrideCemetery(location.Name, ref kind, ref role);
            AddDraft(
                drafts,
                names,
                LocationDisplayName(location),
                kind,
                role,
                location.X,
                location.Y,
                location.Z,
                spawners,
                creatureLookup,
                isEnemy
            );
        }
    }

    private static void AddRegionDrafts(
        List<DestinationDraft> drafts,
        HashSet<string> names,
        IReadOnlyList<RegionSeed> regions,
        IReadOnlyList<SpawnerSeed> spawners,
        Func<string, HostileStats?> creatureLookup,
        Func<string, bool> isEnemy
    )
    {
        if (regions == null)
        {
            return;
        }

        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            var isDungeon = WorldDataSeeds.IsDungeon(region.Type);
            var isTown = WorldDataSeeds.IsTown(region.Type);

            if (!isDungeon && !isTown)
            {
                continue;
            }

            var kind = isDungeon
                ? DestinationKind.Dungeon
                : KindFromGroup.From(region.Type, region.Name);
            var role = isDungeon ? region.Name : RoleOf(kind, region.Type, region.Name);
            AddDraft(
                drafts,
                names,
                region.Name,
                kind,
                role,
                region.X,
                region.Y,
                region.Z,
                spawners,
                creatureLookup,
                isEnemy
            );
        }
    }

    private static void AddSpawnerDrafts(
        List<DestinationDraft> drafts,
        HashSet<string> names,
        IReadOnlyList<SpawnerSeed> spawners,
        Func<string, HostileStats?> creatureLookup,
        Func<string, bool> isEnemy
    )
    {
        for (var i = 0; i < spawners.Count; i++)
        {
            var spawner = spawners[i];
            var kind = SpawnerKind(spawner);

            if (kind is not (DestinationKind.Vendor or DestinationKind.Healer or DestinationKind.Bank or DestinationKind.Hunt))
            {
                continue;
            }

            // A shop spawner lists every keeper it hosts (Shipwright, Mapmaker). One
            // draft per named role keeps vendor:Mapmaker resolvable instead of hiding
            // behind whichever creature the file lists first.
            var emitted = false;

            if (spawner.CreatureNames != null)
            {
                for (var j = 0; j < spawner.CreatureNames.Count; j++)
                {
                    var creature = spawner.CreatureNames[j];

                    if (string.IsNullOrWhiteSpace(creature) ||
                        KindFromGroup.From(null, creature) != kind)
                    {
                        continue;
                    }

                    AddDraft(
                        drafts,
                        names,
                        $"{creature} {spawner.X}-{spawner.Y}",
                        kind,
                        RoleOf(kind, null, creature),
                        spawner.X,
                        spawner.Y,
                        spawner.Z,
                        spawners,
                        creatureLookup,
                        isEnemy
                    );
                    emitted = true;
                }
            }

            if (!emitted)
            {
                AddDraft(
                    drafts,
                    names,
                    SpawnerDisplayName(spawner, kind),
                    kind,
                    RoleOf(kind, null, FirstCreature(spawner.CreatureNames)),
                    spawner.X,
                    spawner.Y,
                    spawner.Z,
                    spawners,
                    creatureLookup,
                    isEnemy
                );
            }
        }
    }

    private static void AddDraft(
        List<DestinationDraft> drafts,
        HashSet<string> names,
        string name,
        DestinationKind kind,
        string role,
        int x,
        int y,
        int z,
        IReadOnlyList<SpawnerSeed> spawners,
        Func<string, HostileStats?> creatureLookup,
        Func<string, bool> isEnemy
    )
    {
        // Height is judged later, against the node a walk plan can actually end at.
        // Judging it here against the land average dropped the Britain stables: their
        // floor and their keeper both stand 30 up, over land that reads 0.
        if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
        {
            return;
        }

        drafts.Add(
            new DestinationDraft
            {
                Name = name,
                Kind = kind.ToString(),
                Role = role,
                X = x,
                Y = y,
                Z = z,
                Difficulty = NeedsDifficulty(kind) ? ScoreAt(spawners, x, y, creatureLookup, isEnemy) : null
            }
        );
    }

    private static void AddResourceDrafts(
        List<DestinationDraft> drafts,
        HashSet<string> names,
        NavGraph graph,
        Func<int, int, int, ResourceHit> resourceProbe
    )
    {
        if (resourceProbe == null || graph == null)
        {
            return;
        }

        foreach (var node in graph.Nodes)
        {
            var role = ResourceSampler.Classify(node.X, node.Y, node.Z, resourceProbe);

            if (string.IsNullOrWhiteSpace(role) || HasNearbyResource(drafts, node, role))
            {
                continue;
            }

            AddDraft(
                drafts,
                names,
                $"{role} {node.X}-{node.Y}",
                DestinationKind.Resource,
                role,
                node.X,
                node.Y,
                node.Z,
                [],
                null,
                null
            );
        }
    }

    private static bool HasNearbyResource(List<DestinationDraft> drafts, NavNode node, string role)
    {
        for (var i = 0; i < drafts.Count; i++)
        {
            var draft = drafts[i];

            if (!string.Equals(draft.Kind, nameof(DestinationKind.Resource), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(draft.Role, role, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (NavMetric.Chebyshev(new Point3D(draft.X, draft.Y, draft.Z), node.Location) <= ResourceSpacing)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A dungeon's difficulty: its three strongest foes within <see cref="DifficultyScanRange"/>.
    /// A hunt spot has none here: the boot rates it by the ground or the floor it stands on
    /// (<see cref="GroundRating"/>), which needs the world's dungeon regions.
    /// </summary>
    private static int ScoreAt(
        IReadOnlyList<SpawnerSeed> spawners,
        int x,
        int y,
        Func<string, HostileStats?> creatureLookup,
        Func<string, bool> isEnemy
    )
    {
        var creatures = new List<string>();

        for (var i = 0; i < spawners.Count; i++)
        {
            var spawner = spawners[i];

            if (NavMetric.Chebyshev(new Point3D(x, y, 0), new Point3D(spawner.X, spawner.Y, 0)) >
                DifficultyScanRange)
            {
                continue;
            }

            var entries = spawner.CreatureNames;

            if (entries == null)
            {
                continue;
            }

            for (var j = 0; j < entries.Count; j++)
            {
                var creature = entries[j];

                if (!string.IsNullOrWhiteSpace(creature) && !SpawnerSeeds.IsVendorName(creature) &&
                    isEnemy?.Invoke(creature) != false)
                {
                    creatures.Add(creature);
                }
            }
        }

        return AreaDifficulty.AreaScore(creatures, creatureLookup);
    }

    private static DestinationKind SpawnerKind(SpawnerSeed spawner)
    {
        var kind = DestinationKind.Hunt;
        var names = spawner.CreatureNames;

        if (names == null)
        {
            return kind;
        }

        for (var i = 0; i < names.Count; i++)
        {
            var next = KindFromGroup.From(null, names[i]);

            if (next == DestinationKind.Bank)
            {
                return DestinationKind.Bank;
            }

            if (next == DestinationKind.Healer)
            {
                kind = DestinationKind.Healer;
            }
            else if (next == DestinationKind.Vendor && kind == DestinationKind.Hunt)
            {
                kind = DestinationKind.Vendor;
            }
        }

        return kind;
    }

    private static string RoleOf(DestinationKind kind, string group, string name)
    {
        if (kind == DestinationKind.Resource)
        {
            return ResourceKind.FromGroup($"{group} {name}");
        }

        if (kind == DestinationKind.Dungeon)
        {
            var leaf = GroupLeaf(group);
            return string.IsNullOrWhiteSpace(leaf) ? name : leaf;
        }

        if (kind == DestinationKind.Vendor)
        {
            var role = KindFromGroup.VendorRoleOf(group) ?? KindFromGroup.VendorRoleOf(name);

            if (role != null)
            {
                return role;
            }
        }

        // An inn is a healer kind that doubles as a rest spot. Naming the role lets the
        // tavern alias reach inns whose own name says neither inn nor tavern.
        if (kind == DestinationKind.Healer && ContainsToken(group, KindFromGroup.InnRole))
        {
            return KindFromGroup.InnRole;
        }

        if (kind == DestinationKind.Hunt && ContainsToken(name, DestinationCatalog.GraveyardRole))
        {
            return DestinationCatalog.GraveyardRole;
        }

        return name;
    }

    private static void OverrideCemetery(string name, ref DestinationKind kind, ref string role)
    {
        if (!ContainsToken(name, CemeteryToken) && !ContainsToken(name, DestinationCatalog.GraveyardRole))
        {
            return;
        }

        kind = DestinationKind.Hunt;
        role = DestinationCatalog.GraveyardRole;
    }

    private static string LocationDisplayName(NamedSeed seed)
    {
        var leaf = GroupLeaf(seed.Group);

        // A single-segment group is a bare category, not a place path. Prepending it
        // only renames the place after its own shelf: "Shrines Chaos".
        if (string.IsNullOrWhiteSpace(leaf) ||
            leaf.Equals(seed.Group, StringComparison.OrdinalIgnoreCase) ||
            leaf.Equals(TownsGroup, StringComparison.OrdinalIgnoreCase) ||
            seed.Name.Contains(leaf, StringComparison.OrdinalIgnoreCase))
        {
            return seed.Name;
        }

        return leaf + " " + seed.Name;
    }

    private static string GroupLeaf(string group)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            return null;
        }

        var index = group.LastIndexOf(GroupSeparator);
        return index < 0 ? group : group[(index + 1)..];
    }

    private static string SpawnerDisplayName(SpawnerSeed spawner, DestinationKind kind)
    {
        var label = FirstCreature(spawner.CreatureNames) ?? kind.ToString();
        return $"{label} {spawner.X}-{spawner.Y}";
    }

    private static string SpawnerSeedName(SpawnerSeed spawner) =>
        FirstCreature(spawner.CreatureNames) ?? $"spawner-{spawner.X}-{spawner.Y}";

    private static string FirstCreature(IReadOnlyList<string> names)
    {
        if (names == null)
        {
            return null;
        }

        for (var i = 0; i < names.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(names[i]))
            {
                return names[i];
            }
        }

        return null;
    }

    private static IReadOnlyList<NamedSeed> FilterLocations(IReadOnlyList<NamedSeed> locations, string facet)
    {
        if (locations == null || locations.Count == 0)
        {
            return [];
        }

        var filtered = new List<NamedSeed>(locations.Count);

        for (var i = 0; i < locations.Count; i++)
        {
            var location = locations[i];

            if (string.IsNullOrWhiteSpace(location.Facet) || WorldDataSeeds.OnFacet(location.Facet, facet))
            {
                filtered.Add(location);
            }
        }

        return filtered;
    }

    private static IReadOnlyList<RegionSeed> FilterRegions(IReadOnlyList<RegionSeed> regions, string facet)
    {
        if (regions == null || regions.Count == 0)
        {
            return [];
        }

        var filtered = new List<RegionSeed>();

        for (var i = 0; i < regions.Count; i++)
        {
            if (WorldDataSeeds.OnFacet(regions[i].Map, facet))
            {
                filtered.Add(regions[i]);
            }
        }

        return filtered;
    }

    private static IReadOnlyList<SpawnerSeed> FilterSpawners(IReadOnlyList<SpawnerSeed> spawners, string facet)
    {
        if (spawners == null || spawners.Count == 0)
        {
            return [];
        }

        var filtered = new List<SpawnerSeed>();

        for (var i = 0; i < spawners.Count; i++)
        {
            if (WorldDataSeeds.OnFacet(spawners[i].Map, facet))
            {
                filtered.Add(spawners[i]);
            }
        }

        return filtered;
    }

    /// <summary>
    /// The facet's spawn folders of the era's spawn sets, such as Spawns/shared/felucca for
    /// The Second Age: the spawns the world setup builds for that era, and no later ones.
    /// </summary>
    public static List<string> SpawnFolders(string dataRoot, string facet, IReadOnlyList<string> spawnSets)
    {
        var folders = new List<string>();

        if (string.IsNullOrWhiteSpace(dataRoot) || string.IsNullOrWhiteSpace(facet))
        {
            return folders;
        }

        foreach (var set in spawnSets ?? [])
        {
            folders.Add(Path.Combine(dataRoot, SpawnsFolder, set, facet.ToLowerInvariant()));
        }

        return folders;
    }

    /// <summary>The spawners of one facet in the server's spawn files of the given sets.</summary>
    public static IReadOnlyList<SpawnerSeed> SpawnersFor(string dataRoot, string facet, IReadOnlyList<string> spawnSets) =>
        FilterSpawners(LoadSpawners(dataRoot, facet, spawnSets), facet);

    /// <summary>The named locations of one facet in the server's location file.</summary>
    public static IReadOnlyList<NamedSeed> LocationsFor(string dataRoot, string facet) =>
        FilterLocations(LocationSeeds.LoadFile(LocationsPath(dataRoot, facet)), facet);

    private static IReadOnlyList<SpawnerSeed> LoadSpawners(string dataRoot, string facet, IReadOnlyList<string> spawnSets)
    {
        var spawners = new List<SpawnerSeed>();

        foreach (var folder in SpawnFolders(dataRoot, facet, spawnSets))
        {
            spawners.AddRange(SpawnerSeeds.LoadDirectory(folder));
        }

        return spawners;
    }

    private static IReadOnlyList<TeleporterLink> LoadTeleporters(string path)
    {
        var json = ReadFile(path);
        return string.IsNullOrWhiteSpace(json) ? [] : WorldDataSeeds.ParseTeleporters(json);
    }

    private static List<TeleporterLink> SameFacetTeleports(IReadOnlyList<TeleporterLink> teleporters, string facet)
    {
        var links = new List<TeleporterLink>();

        for (var i = 0; i < teleporters.Count; i++)
        {
            var link = teleporters[i];

            if (WorldDataSeeds.OnFacet(link.SrcMap, facet) && WorldDataSeeds.OnFacet(link.DstMap, facet))
            {
                links.Add(link);
            }
        }

        return links;
    }

    private static string LocationsPath(string dataRoot, string facet)
    {
        var direct = Path.Combine(dataRoot, LocationsFolder, facet + ConfigFile.FileExtension);

        if (File.Exists(direct))
        {
            return direct;
        }

        return Path.Combine(dataRoot, LocationsFolder, facet.ToLowerInvariant() + ConfigFile.FileExtension);
    }

    private static string ReadFile(string path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? File.ReadAllText(path) : string.Empty;

    private static string NodeName(string facet, int x, int y) => $"{facet}-{x}-{y}";

    private static int ArrivalRangeFor(string kind) =>
        string.Equals(kind, nameof(DestinationKind.Bank), StringComparison.OrdinalIgnoreCase)
            ? NavLimits.BankArrivalRange
            : NavLimits.DefaultArrivalRange;

    private static bool NeedsDifficulty(DestinationKind kind) => kind == DestinationKind.Dungeon;

    private static bool ContainsToken(string value, string token) =>
        !string.IsNullOrWhiteSpace(value) &&
        !string.IsNullOrWhiteSpace(token) &&
        value.Contains(token, StringComparison.OrdinalIgnoreCase);
}
