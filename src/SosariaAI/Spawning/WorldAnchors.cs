using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using SosariaAI.Skills;

namespace SosariaAI.Spawning;

/// <summary>
/// The places one facet offers its people: the bank towns they live in (with the weights the
/// town folk are spread by), the work sites of each harvest and trade, the public moongates,
/// and the side each town belongs to.
/// </summary>
public sealed class FacetAnchors
{
    private readonly IReadOnlyList<WorkSite> _lumberSites;
    private readonly IReadOnlyList<WorkSite> _mineSites;
    private readonly IReadOnlyList<WorkSite> _fishSites;
    private readonly Func<string, IReadOnlyList<WorkSite>> _craftHomes;

    public FacetAnchors(
        bool isStock,
        IReadOnlyList<WorkSite> towns,
        IReadOnlyList<WorkSite> townPopulation,
        IReadOnlyList<WorkSite> lumberSites,
        IReadOnlyList<WorkSite> mineSites,
        IReadOnlyList<WorkSite> fishSites,
        IReadOnlyList<Point3D> publicGates,
        IReadOnlyList<TownSide> townSides,
        Func<string, IReadOnlyList<WorkSite>> craftHomes
    )
    {
        IsStock = isStock;
        Towns = towns;
        TownPopulation = townPopulation;
        _lumberSites = lumberSites;
        _mineSites = mineSites;
        _fishSites = fishSites;
        PublicGates = publicGates;
        TownSides = townSides;
        _craftHomes = craftHomes;
        TownHomes = new Point3D[towns.Count];

        for (var i = 0; i < towns.Count; i++)
        {
            TownHomes[i] = towns[i].Home;
        }
    }

    /// <summary>True for the Britannia tables; false for places read from a map's own nav catalog.</summary>
    public bool IsStock { get; }

    /// <summary>The bank towns, one site each.</summary>
    public IReadOnlyList<WorkSite> Towns { get; }

    /// <summary>The towns town folk are drawn from, a big town listed more than once.</summary>
    public IReadOnlyList<WorkSite> TownPopulation { get; }

    public IReadOnlyList<Point3D> PublicGates { get; }

    public IReadOnlyList<TownSide> TownSides { get; }

    /// <summary>Each town's home spot, where people wait for each other.</summary>
    public Point3D[] TownHomes { get; }

    /// <summary>
    /// The sites of a work: a harvest's sites, a trade's station towns, else the bank towns.
    /// Empty for a harvest this facet has no site of.
    /// </summary>
    public IReadOnlyList<WorkSite> SitesOf(string skillKind) =>
        skillKind switch
        {
            SkillKinds.Lumberjack => _lumberSites,
            SkillKinds.Mine => _mineSites,
            SkillKinds.Fish => _fishSites,
            _ when _craftHomes(skillKind) is { Count: > 0 } towns => towns,
            _ => Towns
        };

    /// <summary>
    /// The sites a person of this work draws its home from: the weighted town list for town
    /// life, else <see cref="SitesOf"/>, and the bank towns for a harvest this facet has no site of.
    /// </summary>
    public IReadOnlyList<WorkSite> HomeSites(string skillKind)
    {
        if (skillKind == SkillKinds.IdleWander)
        {
            return TownPopulation;
        }

        var sites = SitesOf(skillKind);
        return sites.Count > 0 ? sites : Towns;
    }
}

/// <summary>
/// The one place that answers where a facet's people live, work and travel, and where its
/// reds live. A stock map (<see cref="FacetMaps.IsStock(string)"/>) takes the Britannia tables
/// as they always were. Any other map takes its own nav catalog (<see cref="NavWorld.DestinationsFor"/>):
/// each bank is a town; each town works the nearest wood, rock and shore the nav build found
/// within the leash; the public moongates are the gate items standing on the map; the towns
/// take Order and Chaos in turn. A tester's one-map shard sent every copy to Britain's
/// coordinates on a map with no Britain.
/// </summary>
public static class WorldAnchors
{
    /// <summary>A picked haven reaches this far each way from its spot: the core of a town.</summary>
    public const int HavenRadius = 32;

    /// <summary>A map keeps its only bank for the blues: a haven bank is picked from two banks on.</summary>
    public const int MinHavenBanks = 2;

    private const int HavenSpan = HavenRadius * 2 + 1;

    private static readonly ILogger logger = SosariaLog.For(typeof(WorldAnchors));
    private static readonly object BuiltLock = new();

    private static readonly Dictionary<string, (DestinationCatalog Source, RedHaven Haven, FacetAnchors Anchors)> Built =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly WorkSite[] NoSites = [];
    private static readonly Rectangle2D[] NoAreas = [];

    /// <summary>The Britannia tables.</summary>
    public static FacetAnchors Stock { get; } = new(
        isStock: true,
        WorkSites.TownSites,
        WorkSites.TownPopulation,
        WorkSites.LumberSites,
        WorkSites.MineSites,
        WorkSites.FishSites,
        WorkSites.PublicGates,
        SideHomes.BritanniaTowns,
        CraftCareerRules.HomesFor
    );

    /// <summary>A custom map before its catalog is read: no place at all.</summary>
    private static readonly FacetAnchors Bare = FromCatalog(null, [], null, 0);

    /// <summary>The places of a facet, a blank facet being <see cref="FacetMaps.Default"/>.</summary>
    public static FacetAnchors For(string facet)
    {
        var name = FacetMaps.OrDefault(facet);

        if (FacetMaps.IsStock(name))
        {
            return Stock;
        }

        var catalog = NavWorld.DestinationsFor(name);

        if (catalog == null)
        {
            return Bare;
        }

        var haven = FacetMaps.Same(PkRules.Haven.Facet, name) ? PkRules.Haven : null;

        lock (BuiltLock)
        {
            if (Built.TryGetValue(name, out var built) && ReferenceEquals(built.Source, catalog) &&
                ReferenceEquals(built.Haven, haven))
            {
                return built.Anchors;
            }

            var anchors = FromCatalog(catalog, MoongateSeeds.LocationsFor(name), haven, HomeLeash.ConfiguredRadius());
            Built[name] = (catalog, haven, anchors);
            logger.Information(
                "{Facet} places: {Towns} bank towns, {Lumber} woods, {Mines} mines, {Shores} shores, {Gates} public moongates",
                name,
                anchors.Towns.Count,
                anchors.SitesOf(SkillKinds.Lumberjack).Count,
                anchors.SitesOf(SkillKinds.Mine).Count,
                anchors.SitesOf(SkillKinds.Fish).Count,
                anchors.PublicGates.Count
            );
            return anchors;
        }
    }

    /// <summary>The places of the facet a person's unique id is qualified with (<see cref="FacetIds.FacetOf"/>).</summary>
    public static FacetAnchors ForId(string uniqueId) => For(FacetIds.FacetOf(uniqueId));

    /// <summary>
    /// A custom map's places from its catalog: every bank outside the reds' haven is a town (all
    /// of them when the haven holds every bank), each town works the nearest place of each
    /// harvest within <paramref name="workReach"/>, and a trade homes in any town. Pure.
    /// </summary>
    public static FacetAnchors FromCatalog(
        DestinationCatalog catalog,
        IReadOnlyList<Point3D> gates,
        RedHaven haven,
        int workReach
    )
    {
        IReadOnlyList<WorkSite> towns = TownsOf(catalog, haven);

        return new FacetAnchors(
            isStock: false,
            towns,
            towns,
            NearTowns(catalog, towns, ResourceKind.Lumber, SkillKinds.Lumberjack, workReach),
            NearTowns(catalog, towns, ResourceKind.Mine, SkillKinds.Mine, workReach),
            NearTowns(catalog, towns, ResourceKind.Fish, SkillKinds.Fish, workReach),
            gates ?? [],
            SideHomes.InTurn(towns),
            kind => CraftCareerRules.IsCareer(kind) ? towns : NoSites
        );
    }

    /// <summary>
    /// The reds' haven for this boot, on the first enabled map in characters.json that carries
    /// reds: its maps.&lt;facet&gt;.redHaven when that names a region or a rectangle of the map;
    /// else Buccaneer's Den on a stock map; else the place <see cref="PickHaven"/> takes from the
    /// map's nav catalog. Buccaneer's Den when no map carries reds. Main thread: it reads regions.
    /// </summary>
    public static RedHaven HavenFor(CharactersConfiguration config)
    {
        if (config?.Maps == null)
        {
            return PkRules.BucsDen;
        }

        foreach (var (name, toggle) in config.Maps)
        {
            if (toggle is not { Enabled: true } ||
                !FacetMaps.TryFind(name, out var map) ||
                !FacetMaps.OpenPvp(map, config.Maps) ||
                OutlawRules.RedCount(toggle.PkEnabled, toggle.PkCount, toggle.Count, CharactersFile.DefaultOpenPvpPkEnabled) <= 0)
            {
                continue;
            }

            return HavenOn(map, toggle.RedHaven);
        }

        return PkRules.BucsDen;
    }

    /// <summary>
    /// The reds' haven a custom map's catalog offers: with two banks or more, the unguarded bank
    /// farthest from the other banks; else the unguarded dungeon entrance farthest from the
    /// banks; else the bank farthest from the others, guarded or not; null with none of them.
    /// The haven is the square of <see cref="HavenRadius"/> round that spot. Pure but for
    /// <paramref name="guarded"/>.
    /// </summary>
    public static RedHaven PickHaven(string facet, DestinationCatalog catalog, Func<Point3D, bool> guarded)
    {
        var banks = new List<Point3D>();
        var mouths = new List<Point3D>();

        for (var i = 0; i < (catalog?.All.Count ?? 0); i++)
        {
            var place = catalog.All[i];

            if (place.ParsedKind == DestinationKind.Bank)
            {
                banks.Add(place.Arrival);
            }
            else if (PkGangRules.IsDungeonMouth(place.Kind, place.Name, onSurface: true))
            {
                mouths.Add(place.Arrival);
            }
        }

        var spot = banks.Count >= MinHavenBanks ? MostRemote(Unguarded(banks, guarded), banks) : null;
        spot ??= MostRemote(Unguarded(mouths, guarded), banks);
        spot ??= MostRemote(banks, banks);

        return spot is { } center
            ? new RedHaven(facet, [new Rectangle2D(center.X - HavenRadius, center.Y - HavenRadius, HavenSpan, HavenSpan)], center)
            : null;
    }

    private static RedHaven HavenOn(Map map, AreaDefinition setting)
    {
        if (setting != null)
        {
            if (Configured(map, setting) is { } configured)
            {
                logger.Information("{Facet} reds live in the haven characters.json names", map.Name);
                return configured;
            }

            logger.Warning("{Facet} redHaven names no region and no rectangle of the map; a haven is picked instead", map.Name);
        }

        if (FacetMaps.IsStock(map.Name, map.Width, map.Height))
        {
            return PkRules.BucsDen with { Facet = map.Name };
        }

        if (PickHaven(map.Name, NavWorld.DestinationsFor(map.Name), at => GuardCall.IsGuardedPlace(at, map)) is { } picked)
        {
            logger.Information("{Facet} reds live round {Spot}; set maps.{Map}.redHaven to choose the place", map.Name, picked.Home, map.Name);
            return picked;
        }

        logger.Warning(
            "{Facet} has no bank and no dungeon entrance in its nav catalog, so its reds have no haven; set maps.{Map}.redHaven",
            map.Name,
            map.Name
        );
        return new RedHaven(map.Name, NoAreas, Point3D.Zero);
    }

    // A region of the map by name, or a rectangle with a size.
    private static RedHaven Configured(Map map, AreaDefinition setting)
    {
        if (!setting.IsNamed)
        {
            var box = setting.ToRectangle();
            return box.Width > 0 && box.Height > 0 ? new RedHaven(map.Name, [box], CenterOf(box)) : null;
        }

        if (!map.Regions.TryGetValue(setting.Name.Trim(), out var region) || region?.Area is not { Length: > 0 } parts)
        {
            return null;
        }

        var areas = new Rectangle2D[parts.Length];

        for (var i = 0; i < parts.Length; i++)
        {
            areas[i] = new Rectangle2D(parts[i].Start.X, parts[i].Start.Y, parts[i].Width, parts[i].Height);
        }

        return new RedHaven(map.Name, areas, region.GoLocation != Point3D.Zero ? region.GoLocation : CenterOf(areas[0]));
    }

    private static Point3D CenterOf(Rectangle2D box) => new(box.X + box.Width / 2, box.Y + box.Height / 2, 0);

    private static List<WorkSite> TownsOf(DestinationCatalog catalog, RedHaven haven)
    {
        var all = new List<WorkSite>();
        var outside = new List<WorkSite>();

        for (var i = 0; i < (catalog?.All.Count ?? 0); i++)
        {
            var place = catalog.All[i];

            if (place.ParsedKind != DestinationKind.Bank)
            {
                continue;
            }

            var town = WorkSites.Town(place.Arrival, place.Name);
            all.Add(town);

            if (haven == null || !haven.Contains(place.Arrival.X, place.Arrival.Y))
            {
                outside.Add(town);
            }
        }

        return outside.Count > 0 ? outside : all;
    }

    // Each town's nearest place of the harvest within the reach, worked from the town.
    private static List<WorkSite> NearTowns(
        DestinationCatalog catalog,
        IReadOnlyList<WorkSite> towns,
        string role,
        string skillKind,
        int reach
    )
    {
        var places = new List<Point3D>();

        for (var i = 0; i < (catalog?.All.Count ?? 0); i++)
        {
            var place = catalog.All[i];

            if (place.ParsedKind == DestinationKind.Resource && string.Equals(place.Role, role, StringComparison.OrdinalIgnoreCase))
            {
                places.Add(place.Arrival);
            }
        }

        var sites = new List<WorkSite>();

        for (var t = 0; t < towns.Count; t++)
        {
            var home = towns[t].Home;
            Point3D? nearest = null;
            var best = reach;

            for (var p = 0; p < places.Count; p++)
            {
                var distance = NavMetric.Chebyshev(home, places[p]);

                if (distance <= best)
                {
                    best = distance;
                    nearest = places[p];
                }
            }

            if (nearest is { } spot)
            {
                sites.Add(new WorkSite($"{towns[t].Name} {role}", home, WorkSites.WorldSite(skillKind, spot).Harvest));
            }
        }

        return sites;
    }

    private static List<Point3D> Unguarded(List<Point3D> spots, Func<Point3D, bool> guarded)
    {
        var open = new List<Point3D>();

        for (var i = 0; i < spots.Count; i++)
        {
            if (guarded?.Invoke(spots[i]) != true)
            {
                open.Add(spots[i]);
            }
        }

        return open;
    }

    // The spot whose nearest other bank lies farthest off; the first spot when no other bank stands.
    private static Point3D? MostRemote(List<Point3D> spots, List<Point3D> banks)
    {
        Point3D? best = null;
        var bestDistance = -1;

        for (var i = 0; i < spots.Count; i++)
        {
            var nearest = int.MaxValue;

            for (var b = 0; b < banks.Count; b++)
            {
                if (banks[b] != spots[i])
                {
                    nearest = Math.Min(nearest, NavMetric.Chebyshev(spots[i], banks[b]));
                }
            }

            if (nearest > bestDistance)
            {
                bestDistance = nearest;
                best = spots[i];
            }
        }

        return best;
    }
}
