using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Which ankhs and healers a ghost can get to from where it stands, judged before it sets out
/// the way its walk (<see cref="TravelSkill"/>) will go: over the road nodes and gates its own
/// bars leave it (<see cref="PathSearchBars.For"/>; a red ghost keeps off the guards, and crosses
/// them by the least guarded way when no way clear of them leads out), each one-way pad only
/// its own way, from a start it can walk to, to the node the trip will aim at; or, for the
/// nearest few within a tile walk, over the tiles. Each site it can get to gets the tiles of
/// that trip (<see cref="TripTiles"/>), so a look takes the shortest trip, not the nearest site
/// (<see cref="GhostSeek.ShortestTrip"/>). One road search per start serves every site of a
/// look, and a site is judged only when the look asks for it, nearest first.
/// A graph piece shared through exit pads is no road: 572 ghost walks to the Spirituality ankh
/// platform found none, and ghosts on the Deceit island tried the same six healers for half an
/// hour. World thread: it reads the map's tiles.
/// </summary>
public sealed class GhostReach
{
    private readonly NavGraph _graph;
    private readonly Point3D _from;
    private readonly TileWalker _walker;
    private readonly Func<int, int, int, bool> _isIndoor;
    private readonly Roads _roads;
    private readonly Roads? _crossingRoads;
    private int _tileTries;

    private GhostReach(
        NavGraph graph,
        Point3D from,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor,
        Roads roads,
        Roads? crossingRoads
    )
    {
        _graph = graph;
        _from = from;
        _walker = walker;
        _isIndoor = isIndoor;
        _roads = roads;
        _crossingRoads = crossingRoads;
    }

    /// <summary>The bars of one road search and the trips it found (<see cref="Traveler.WalkReach"/>).</summary>
    private readonly record struct Roads(PathSearchBars Bars, Dictionary<string, double> Trips);

    /// <summary>The reach of <paramref name="ghost"/> as it stands now.</summary>
    public static GhostReach For(SosariaCharacter ghost) =>
        For(ghost.Map, ghost.HomeFacet, ghost.Location, PkRules.IsRed(ghost.Kills));

    /// <summary>The reach of a walker at <paramref name="from"/> on <paramref name="map"/>, over the road graph of <paramref name="facet"/>.</summary>
    public static GhostReach For(Map map, string facet, Point3D from, bool murderer)
    {
        var graph = NavWorld.GraphFor(facet);
        var walker = Standable.Walker(map);
        Func<int, int, int, bool> isIndoor = (x, y, z) => IndoorTiles.IsBuilding(map, x, y, z);

        if (graph == null)
        {
            return new GhostReach(null, from, walker, isIndoor, default, null);
        }

        var bars = PathSearchBars.For(graph, map, from, murderer, ghost: true);

        // A red ghost with no road clear of the guards re-plans across them, as its walk does
        // (TravelSkill.WayPastTheGuards). Without these roads a red ghost whose every way out
        // crosses guarded ground, in town or in the Yew woods, would find no ankh.
        Roads? crossing = bars is { MayCrossGuards: true } ? RoadsOf(graph, from, walker, isIndoor, bars.CrossingGuards()) : null;
        return new GhostReach(graph, from, walker, isIndoor, RoadsOf(graph, from, walker, isIndoor, bars), crossing);
    }

    private static Roads RoadsOf(
        NavGraph graph,
        Point3D from,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor,
        PathSearchBars bars
    ) =>
        new(bars, Traveler.WalkReach(graph, from, walker, isIndoor, bars));

    /// <summary>
    /// The tiles of the ghost's trip to <paramref name="site"/>, or null when it cannot get
    /// there. Over the roads: the trip to the goal node its walk aims at
    /// (<see cref="Traveler.WalkTrip"/>) and on to the site, on the roads clear of the guards,
    /// else, for a red under them, across them. Else over the tiles: the tile route's length.
    /// Without a graph there is nothing to judge a way by: the walk is left to find out, and the
    /// trip is the straight distance.
    /// </summary>
    public double? TripTiles(ResSource site)
    {
        if (_graph == null)
        {
            return NavMetric.Chebyshev(_from, site.Location);
        }

        if ((RoadTrip(site, _roads) ?? (_crossingRoads is { } crossing ? RoadTrip(site, crossing) : null)) is { } road)
        {
            return road;
        }

        if (!GhostSeek.MayTryTiles(_tileTries, NavMetric.Chebyshev(_from, site.Location)))
        {
            return null;
        }

        _tileTries++;
        var route = TileRoute.Find(_from, site.Location, _walker, _isIndoor, site.Range);
        return route.Count > 0 ? GhostSeek.RouteTiles(_from, route) : null;
    }

    /// <summary>The trip over <paramref name="roads"/> to the goal node the walk aims at, and on to the site; null when those roads do not get there.</summary>
    private double? RoadTrip(ResSource site, Roads roads)
    {
        if (!AnyGoalNodeReached(site.Location, roads) ||
            Traveler.PreferredGoal(_graph, _from, site.Location) is not { } goal ||
            Traveler.WalkTrip(_graph, roads.Trips, goal, roads.Bars) is not { } roadTiles)
        {
            return null;
        }

        return roadTiles + NavMetric.Distance(goal.Location, site.Location);
    }

    /// <summary>
    /// A cheap first test: the trip's goal node is one of these, so with none of them reached
    /// the site is not. A look past two hundred healers with no road to any stays short.
    /// </summary>
    private bool AnyGoalNodeReached(Point3D site, Roads roads)
    {
        var goals = Traveler.GoalNodes(_graph, site);

        for (var i = 0; i < goals.Count; i++)
        {
            if (Traveler.WalkReaches(_graph, roads.Trips, goals[i], roads.Bars))
            {
                return true;
            }
        }

        return false;
    }
}
