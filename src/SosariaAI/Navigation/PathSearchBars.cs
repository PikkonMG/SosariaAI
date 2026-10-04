using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Navigation;

/// <summary>
/// Roads a traveler will not take. A murderer keeps off every node the town guards cover and
/// takes a public moongate only between two pads the guards do not cover, as GateTravel's
/// moongate rule says: a red that steps out onto a guarded pad dies where it lands. Off
/// Felucca it takes none. When the trip starts under the guards, the nodes near the start
/// stay open, so a red caught in town can still walk out, but a guarded pad never is. A trip
/// that starts on open ground keeps every guarded node barred: a window round every start
/// let a red's road run through any town near where it set out. A red with no road clear of
/// the guards at all crosses them only when it starts under them, or is a ghost, whom the guards
/// do not take (<see cref="MayCrossGuards"/>, <see cref="CrossingGuards"/>): each guarded tile then weighs <see cref="GuardedTileWeight"/>
/// tiles, so the road out takes the least guarded ground. A walked leg between two open nodes
/// that cuts through guarded ground (<see cref="GuardedLegs"/>) is barred and priced the same
/// way as a guarded node. Every living walker keeps off the one-way pads that drop it onto a
/// dungeon floor above its reach (<see cref="Pads"/>, <see cref="FloorDrops"/>): tamers and
/// walkers home took the Despise and Fire pads onto Destard's third level. A walker with no
/// road out but over such a pad takes it (<see cref="OpeningPads"/>): the only road off Deceit's
/// island runs over Fire and Despise's third level. Read by path workers, so it holds only
/// plain data that is never changed once handed out.
/// </summary>
public sealed class PathSearchBars
{
    /// <summary>Nodes this close to a start under the guards stay open: the way out of the town the red stands in.</summary>
    public const int OpenAroundStartTiles = 64;

    /// <summary>
    /// What one tile of a step onto a guarded node weighs on a road that crosses the guards: a
    /// way round up to this many times as long as the guarded stretch wins.
    /// </summary>
    public const int GuardedTileWeight = 20;

    private static readonly ConditionalWeakTable<NavGraph, HashSet<int>> GuardedByGraph = new();

    public PathSearchBars(
        IReadOnlySet<int> nodes,
        Point3D start,
        bool noMoongates,
        bool openAroundStart,
        bool crossesGuards = false,
        IReadOnlySet<long> legs = null,
        IReadOnlySet<long> pads = null,
        bool ghost = false
    )
    {
        Nodes = nodes;
        Start = start;
        NoMoongates = noMoongates;
        OpenAroundStart = openAroundStart;
        CrossesGuards = crossesGuards;
        Legs = legs;
        Pads = pads;
        Ghost = ghost;
    }

    /// <summary>Search indexes of the barred nodes.</summary>
    public IReadOnlySet<int> Nodes { get; }

    /// <summary>Keys (<see cref="EdgeHealthRules.Key"/>) of the walked legs that cross guarded ground, or null.</summary>
    public IReadOnlySet<long> Legs { get; }

    /// <summary>
    /// Keys (<see cref="EdgeHealthRules.Key"/>) of the one-way pads this walker does not take:
    /// each drops it onto a dungeon floor above its reach (<see cref="FloorDrops"/>). Null for none.
    /// </summary>
    public IReadOnlySet<long> Pads { get; }

    /// <summary>True for a murderer's bars, which keep off the guards; false for bars on pads alone.</summary>
    public bool KeepsOffGuards => Nodes != null;

    public Point3D Start { get; }

    public bool NoMoongates { get; }

    /// <summary>True when the trip starts under the guards, so the guarded nodes near the start stay open.</summary>
    public bool OpenAroundStart { get; }

    /// <summary>True when the guarded nodes are open at a price (<see cref="PenaltyTiles"/>), not barred.</summary>
    public bool CrossesGuards { get; }

    /// <summary>True for a ghost's trip: the guards take no ghost, so it may cross them from open ground too.</summary>
    public bool Ghost { get; }

    /// <summary>True when the search must not step onto this node.</summary>
    public bool BarsNode(int index, Point3D location) => !CrossesGuards && Guards(index, location);

    /// <summary>True when the search must not walk the leg between these two nodes: it crosses guarded ground.</summary>
    public bool BarsLeg(int from, int to, Point3D fromLocation, Point3D toLocation) =>
        !CrossesGuards && GuardsLeg(from, to, fromLocation, toLocation);

    /// <summary>
    /// Extra tiles a step of <paramref name="stepTiles"/> from node <paramref name="from"/> onto
    /// node <paramref name="to"/> costs: on a road that crosses the guards, a step onto a guarded
    /// node, or a walked leg over guarded ground, weighs <see cref="GuardedTileWeight"/> times its length.
    /// </summary>
    public double PenaltyTiles(int from, int to, Point3D fromLocation, Point3D toLocation, double stepTiles, bool gate) =>
        CrossesGuards && (Guards(to, toLocation) || !gate && GuardsLeg(from, to, fromLocation, toLocation))
            ? stepTiles * (GuardedTileWeight - 1)
            : 0;

    /// <summary>
    /// True when a trip with no road clear of the guards may search again across them: a trip
    /// that starts under the guards, whose every way out starts in town, or a ghost's. From open
    /// ground a living red never plans a road into a town. A red ghost does: in the Yew woods
    /// every road out ran through Yew, and 68 red ghosts in two hours stood up with no way back.
    /// Nobody raises it under the guards (<see cref="SosariaAI.Behaviour.ResurrectAid.WantedUnderGuards"/>).
    /// </summary>
    public bool MayCrossGuards => (OpenAroundStart || Ghost) && !CrossesGuards;

    /// <summary>These bars with the guarded nodes open at a price: the road of a red with no road clear of the guards that may cross them.</summary>
    public PathSearchBars CrossingGuards() => new(Nodes, Start, NoMoongates, OpenAroundStart, crossesGuards: true, Legs, Pads, Ghost);

    /// <summary>True when these bars keep off a one-way pad, so a trip they leave with no road may search again over it.</summary>
    public bool MayOpenPads => Pads != null;

    /// <summary>
    /// These bars with every one-way pad open (<see cref="Pads"/>): the road of a walker with
    /// no other way out of where it stands. Null when the pads were all they barred. The weak
    /// in Deceit stood there with no road home at all: its one road off the island crosses
    /// Despise's third level, a floor asking power 389.
    /// </summary>
    public PathSearchBars OpeningPads() =>
        KeepsOffGuards ? new PathSearchBars(Nodes, Start, NoMoongates, OpenAroundStart, CrossesGuards, Legs, pads: null, Ghost) : null;

    /// <summary>True when the guards cover this node and the trip does not start among them near it.</summary>
    private bool Guards(int index, Point3D location) =>
        Nodes?.Contains(index) == true && !NearStartUnderGuards(location);

    /// <summary>True when this leg crosses guarded ground and the trip does not start among the guards near either end.</summary>
    private bool GuardsLeg(int from, int to, Point3D fromLocation, Point3D toLocation) =>
        Legs?.Contains(EdgeHealthRules.Key(from, to)) == true &&
        !NearStartUnderGuards(fromLocation) && !NearStartUnderGuards(toLocation);

    /// <summary>True when the trip starts under the guards and this spot lies in the way out of that town.</summary>
    private bool NearStartUnderGuards(Point3D location) =>
        OpenAroundStart && NavMetric.Chebyshev(location, Start) <= OpenAroundStartTiles;

    /// <summary>True when the search must not take this gate between two nodes.</summary>
    public bool BarsGate(NavGateKind kind, int from, int to) =>
        kind == NavGateKind.Moongate && (NoMoongates || Nodes?.Contains(from) == true || Nodes?.Contains(to) == true) ||
        Pads?.Contains(EdgeHealthRules.Key(from, to)) == true;

    /// <summary>
    /// The bars of a murderer's trip from <paramref name="start"/>, alive or a ghost, or null for
    /// anyone else. Guards call no ghost a candidate, but a red ghost in town is raised in town:
    /// a red ghost walked into Yew, a passer-by raised it there, and the guards killed it a
    /// second later, so nobody raises a red there now. A <paramref name="ghost"/> with no road
    /// clear of the guards crosses them (<see cref="MayCrossGuards"/>), and one with no ankh even
    /// so stands up on its own (<see cref="SosariaAI.Behaviour.GhostRules.NoWayFallbackAfter"/>). World thread only: the guarded
    /// nodes and legs are read from the map regions once per graph, with the legs walks learned since.
    /// </summary>
    public static PathSearchBars For(NavGraph graph, Map map, Point3D start, bool murderer, bool ghost = false) =>
        For(graph, map, start, murderer, pads: null, ghost);

    /// <summary>
    /// The bars of <paramref name="walker"/>'s trip from <paramref name="start"/>: a murderer's
    /// (<see cref="For(NavGraph, Map, Point3D, bool, bool)"/>), and for the living the one-way pads onto
    /// the floors above its reach, read by the power it takes a place on with
    /// (<see cref="DungeonGround.FightingPower"/>), as the crawl and the hall pick read it
    /// (<see cref="FloorDrops"/>). A ghost takes no harm there and keeps every pad. Null when
    /// nothing is barred. World thread only.
    /// </summary>
    public static PathSearchBars ForWalker(SosariaCharacter walker, NavGraph graph, Map map, Point3D start)
    {
        var murderer = PkRules.IsRed(walker?.Kills ?? 0);

        if (walker == null || walker.IsGhost || DungeonAtlas.For(graph?.Facet) is not { } floors || !floors.IsOf(graph))
        {
            return For(graph, map, start, murderer, walker?.IsGhost == true);
        }

        var power = DungeonGround.FightingPower(walker);

        return murderer
            ? For(graph, map, start, murderer: true, FloorDrops.AboveReach(floors, power), ghost: false)
            : FloorDrops.BarsAboveReach(floors, power);
    }

    private static PathSearchBars For(NavGraph graph, Map map, Point3D start, bool murderer, IReadOnlySet<long> pads, bool ghost) =>
        !murderer || graph == null || map == null || map == Map.Internal
            ? null
            : new PathSearchBars(
                GuardedNodes(graph, map),
                start,
                noMoongates: map != Map.Felucca,
                openAroundStart: GuardCall.IsGuardedPlace(start, map),
                legs: GuardedLegs.For(graph, map),
                pads: pads,
                ghost: ghost
            );

    /// <summary>
    /// Reads the guarded nodes and legs of Felucca's graph, where reds walk, whenever the graph
    /// loads (<see cref="NavWorld"/>). The legs are read tile by tile, near a million tiles, and
    /// the first red plan of the day paid for it on the world thread mid-play. Returns the
    /// guarded node and leg counts for the log.
    /// </summary>
    public static (int Nodes, int Legs) WarmFelucca()
    {
        var graph = NavWorld.GraphFor(FacetNames.Felucca);

        return graph == null ? default : (GuardedNodes(graph, Map.Felucca).Count, GuardedLegs.For(graph, Map.Felucca).Count);
    }

    private static HashSet<int> GuardedNodes(NavGraph graph, Map map)
    {
        if (GuardedByGraph.TryGetValue(graph, out var known))
        {
            return known;
        }

        var guarded = new HashSet<int>();

        foreach (var node in graph.Nodes)
        {
            if (node.Index >= 0 && GuardCall.IsGuardedPlace(node.Location, map))
            {
                guarded.Add(node.Index);
            }
        }

        GuardedByGraph.AddOrUpdate(graph, guarded);
        return guarded;
    }
}
