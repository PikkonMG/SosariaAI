using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

public readonly record struct ResSource(string Kind, Point3D Location, int Range);

/// <summary>The ankh or healer a ghost has the shortest trip to, and where it stands at an ankh.</summary>
public static class GhostSeek
{
    public const string KindHealer = "healer";
    public const string KindShrine = "shrine";

    /// <summary>
    /// The tile nearest an ankh, within <paramref name="range"/>, where a ghost can stand and
    /// stay: <paramref name="standAt"/> gives the standing point of a tile, or null when a
    /// person does not fit there or a teleporter pad lies on it. Null when no tile serves:
    /// the Spirituality ankh at (1592,2489) is ringed by the pads that send a visitor off its
    /// island, and a ghost walked there again and again and was never raised.
    /// </summary>
    public static Point3D? RaiseSpot(Point3D ankh, int range, Func<int, int, Point3D?> standAt)
    {
        if (standAt == null)
        {
            return null;
        }

        for (var ring = 0; ring <= range; ring++)
        {
            for (var dx = -ring; dx <= ring; dx++)
            {
                for (var dy = -ring; dy <= ring; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == ring && standAt(ankh.X + dx, ankh.Y + dy) is { } spot)
                    {
                        return spot;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Sites the road search does not reach that a look may still prove over the tiles, the
    /// nearest first. Each proof is a tile search on the world thread, so only a few try.
    /// </summary>
    public const int TileProofTries = 2;

    /// <summary>
    /// The source with the shortest trip, in the tiles <paramref name="tripTiles"/> gives, null
    /// for a site with no way. The nearest by straight distance can sit on an island no road
    /// reaches (one criminal ghost asked for the Honesty shrine 175 times), or at the end of a
    /// long way round: red ghosts at Magincia took the Honesty ankh and walked the Magincia and
    /// Yew gates, Despise, Destard and Deceit to it for ten minutes, while the Justice shrine lay
    /// a short walk from the Yew gate. Sites are asked nearest first, so the few tile proofs a
    /// look may run (<see cref="MayTryTiles"/>) go to the nearest; of two equal trips the nearer
    /// site wins.
    /// </summary>
    public static ResSource? ShortestTrip(Point3D from, IReadOnlyList<ResSource> sources, Func<ResSource, double?> tripTiles)
    {
        if (sources == null || sources.Count == 0 || tripTiles == null)
        {
            return null;
        }

        var order = new List<ResSource>(sources);
        order.Sort((left, right) =>
            NavMetric.Chebyshev(from, left.Location).CompareTo(NavMetric.Chebyshev(from, right.Location)));

        ResSource? best = null;
        var bestTiles = double.PositiveInfinity;

        for (var i = 0; i < order.Count; i++)
        {
            if (tripTiles(order[i]) is { } tiles && tiles < bestTiles)
            {
                best = order[i];
                bestTiles = tiles;
            }
        }

        return best;
    }

    /// <summary>The tiles walked from <paramref name="from"/> along the waypoints of a tile route, in order.</summary>
    public static double RouteTiles(Point3D from, IReadOnlyList<Point3D> route)
    {
        if (route == null)
        {
            return 0;
        }

        var tiles = 0.0;
        var at = from;

        for (var i = 0; i < route.Count; i++)
        {
            tiles += NavMetric.Distance(at, route[i]);
            at = route[i];
        }

        return tiles;
    }

    /// <summary>
    /// True when a site the road search does not reach may be tried over the tiles: fewer than
    /// <see cref="TileProofTries"/> tried this look, and within one tile walk
    /// (<see cref="TileRoute.MaxTripTiles"/>). A trip tries its tile walk before the roads, so a
    /// site with no road node near it can still lie a short walk away.
    /// </summary>
    public static bool MayTryTiles(int triesSoFar, int distanceTiles) =>
        triesSoFar < TileProofTries && distanceTiles <= TileRoute.MaxTripTiles;
}
