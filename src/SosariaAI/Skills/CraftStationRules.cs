using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>What a trade must stand beside to work.</summary>
public enum CraftStation
{
    None,
    AnvilAndForge,
    Heat
}

/// <summary>A shop to work at and the station spot found in it.</summary>
public readonly record struct StationShop(Point3D Shop, Point3D Station);

/// <summary>
/// The tile to work from and how many tiles off it a crafter may stand and still have the
/// whole station inside the engine's reach.
/// </summary>
public readonly record struct StandSpot(Point3D Spot, int Range);

/// <summary>
/// Where a crafter stands to work. The engine looks two tiles out from the crafter for an
/// anvil and a forge, or for a fire or oven. A smith stands between an anvil and a forge that
/// are at most four tiles apart (Skara Brae's forge stands three tiles from its anvil); a cook
/// stands beside the heat. Several crafters share a station, each on a tile of its own within
/// the engine's reach of it. Among the places in reach a crafter takes the nearest one that is
/// not already full of other crafters. Pure. No world objects.
/// </summary>
public static class CraftStationRules
{
    /// <summary>DefBlacksmithy and the heat check both search two tiles around the crafter.</summary>
    public const int EngineReach = 2;

    /// <summary>The engine takes an anvil, forge or heat up to this far above or below the crafter.</summary>
    public const int EngineHeight = 16;

    /// <summary>The walk to a station ends within one tile of the chosen spot.</summary>
    public const int StandRange = 1;

    /// <summary>The walk to a free work tile ends on the tile itself.</summary>
    public const int OnTheTile = 0;

    /// <summary>
    /// How far round a stand spot the anvils, forges and heat are read for its work tiles: a
    /// tile lies within <see cref="EngineReach"/> of the stand, and what it works within
    /// <see cref="EngineReach"/> of the tile.
    /// </summary>
    public const int WorkTileSurvey = EngineReach * 2;

    /// <summary>Walks a crafter makes to free work tiles of a station before it gives the station up.</summary>
    public const int MaxStandTries = 3;

    /// <summary>
    /// How far from a shop's street marker a station is looked for. The Britain smithy's forge
    /// stands eleven tiles in from the blacksmith's marker, up on the raised floor at z 30.
    /// </summary>
    public const int SearchRadius = 16;

    /// <summary>A station-less crafter stands this close to the vendor of its shop, across the counter.</summary>
    public const int CounterStand = 2;

    /// <summary>People within this many tiles of a station are working it.</summary>
    public const int CrowdRadius = 2;

    /// <summary>A station holding this many people is full; the next crafter goes elsewhere.</summary>
    public const int FullCrowd = 4;

    /// <summary>
    /// Each person already at a station makes it look this many tiles further off, so the
    /// crafters of a town spread over its smithies and shops instead of piling on one.
    /// </summary>
    public const int CrowdPenaltyTiles = 40;

    /// <summary>Two stations whose stand spots lie this close together are one station.</summary>
    public const int SameStationTiles = 1;

    private const int Halve = 2;
    private const int NoIndex = -1;

    /// <summary>
    /// The stand spot of the anvil and forge pair nearest <paramref name="from"/>, or null
    /// when no anvil has a forge close enough to work both at once.
    /// </summary>
    public static StandSpot? PairSpot(IReadOnlyList<Point3D> anvils, IReadOnlyList<Point3D> forges, Point3D from)
    {
        var spots = PairSpots(anvils, forges);
        return NearestIndex(spots.Count, index => spots[index].Spot, from) is var i and >= 0 ? spots[i] : null;
    }

    /// <summary>
    /// The stand spot of every anvil and forge pair on one floor at most twice
    /// <see cref="EngineReach"/> apart, one per station: the tile between them, with the range
    /// round it that keeps both within the engine's reach, <see cref="StandRange"/> at most.
    /// </summary>
    public static List<StandSpot> PairSpots(IReadOnlyList<Point3D> anvils, IReadOnlyList<Point3D> forges)
    {
        var spots = new List<StandSpot>();

        for (var a = 0; a < (anvils?.Count ?? 0); a++)
        {
            for (var f = 0; f < (forges?.Count ?? 0); f++)
            {
                var anvil = anvils[a];
                var forge = forges[f];

                if (NavMetric.Chebyshev(anvil, forge) > EngineReach * Halve || !NavMetric.SameFloor(anvil, forge))
                {
                    continue;
                }

                var spot = new Point3D((anvil.X + forge.X) / Halve, (anvil.Y + forge.Y) / Halve, anvil.Z);
                var slack = EngineReach - Math.Max(NavMetric.Chebyshev(spot, anvil), NavMetric.Chebyshev(spot, forge));

                if (slack >= 0 && !spots.Exists(kept => SameStation(kept.Spot, spot)))
                {
                    spots.Add(new StandSpot(spot, Math.Min(slack, StandRange)));
                }
            }
        }

        return spots;
    }

    /// <summary>The heat nearest <paramref name="from"/> to stand beside, or null when there is none.</summary>
    public static StandSpot? NearestSpot(IReadOnlyList<Point3D> stations, Point3D from) =>
        NearestIndex(stations?.Count ?? 0, index => stations[index], from) is var i and >= 0
            ? new StandSpot(stations[i], StandRange)
            : null;

    /// <summary>
    /// The place to work: the nearest shop once each person already standing at a station
    /// counts <see cref="CrowdPenaltyTiles"/> tiles against it, skipping a full station.
    /// <paramref name="crowds"/> holds the people at each shop's station, in the same order.
    /// Null when there is no shop or every one is full.
    /// </summary>
    public static StationShop? PickPlace(IReadOnlyList<StationShop> shops, IReadOnlyList<int> crowds, Point3D from)
    {
        StationShop? best = null;
        var bestCost = int.MaxValue;

        for (var i = 0; i < (shops?.Count ?? 0); i++)
        {
            var crowd = crowds != null && i < crowds.Count ? Math.Max(0, crowds[i]) : 0;

            if (crowd >= FullCrowd)
            {
                continue;
            }

            var cost = NavMetric.Chebyshev(from, shops[i].Shop) + crowd * CrowdPenaltyTiles;

            if (cost < bestCost)
            {
                bestCost = cost;
                best = shops[i];
            }
        }

        return best;
    }

    /// <summary>
    /// The shops with one entry per station: two catalog shops at the same smithy, or an anvil
    /// and the blacksmith shop beside it, are one place to work.
    /// </summary>
    public static List<StationShop> Distinct(IReadOnlyList<StationShop> shops)
    {
        var kept = new List<StationShop>();

        for (var i = 0; i < (shops?.Count ?? 0); i++)
        {
            if (!kept.Exists(shop => SameStation(shop.Station, shops[i].Station)))
            {
                kept.Add(shops[i]);
            }
        }

        return kept;
    }

    /// <summary>
    /// True when the engine lets a crafter at <paramref name="at"/> work: an anvil and a forge
    /// (the two lists) within <see cref="EngineReach"/> tiles and <see cref="EngineHeight"/> of
    /// its height for a smith, heat (the first list) for a cook, anywhere for a counter trade.
    /// </summary>
    public static bool CanWorkAt(Point3D at, IReadOnlyList<Point3D> first, IReadOnlyList<Point3D> second, CraftStation station) =>
        station switch
        {
            CraftStation.AnvilAndForge => WithinReach(at, first) && WithinReach(at, second),
            CraftStation.Heat => WithinReach(at, first),
            _ => true
        };

    /// <summary>
    /// Every tile round <paramref name="stand"/>, on its floor, a crafter can work the station
    /// from, nearest the stand first. An anvil with its forge beside it is worked from a score
    /// of tiles, so four smiths share it; the stand spot alone held one, and the next smith
    /// found "no free place at the AnvilAndForge". Tiles holding the anvil, forge or heat are
    /// left out.
    /// </summary>
    public static List<Point3D> WorkTiles(StandSpot stand, IReadOnlyList<Point3D> first, IReadOnlyList<Point3D> second, CraftStation station)
    {
        var tiles = new List<Point3D>();
        var spot = stand.Spot;

        for (var dx = -EngineReach; dx <= EngineReach; dx++)
        {
            for (var dy = -EngineReach; dy <= EngineReach; dy++)
            {
                var tile = new Point3D(spot.X + dx, spot.Y + dy, spot.Z);

                if (!HoldsPiece(tile, first) && !HoldsPiece(tile, second) && CanWorkAt(tile, first, second, station))
                {
                    tiles.Add(tile);
                }
            }
        }

        tiles.Sort((a, b) => NavMetric.Chebyshev(spot, a).CompareTo(NavMetric.Chebyshev(spot, b)) is var byReach and not 0
            ? byReach
            : a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        return tiles;
    }

    /// <summary>The tile nearest <paramref name="from"/>, or null when there is none.</summary>
    public static Point3D? NearestTile(IReadOnlyList<Point3D> tiles, Point3D from) =>
        NearestIndex(tiles?.Count ?? 0, index => tiles[index], from) is var i and >= 0 ? tiles[i] : null;

    private static bool WithinReach(Point3D at, IReadOnlyList<Point3D> pieces)
    {
        for (var i = 0; i < (pieces?.Count ?? 0); i++)
        {
            if (NavMetric.Chebyshev(at, pieces[i]) <= EngineReach && Math.Abs(at.Z - pieces[i].Z) <= EngineHeight)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HoldsPiece(Point3D tile, IReadOnlyList<Point3D> pieces)
    {
        for (var i = 0; i < (pieces?.Count ?? 0); i++)
        {
            if (pieces[i].X == tile.X && pieces[i].Y == tile.Y)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameStation(Point3D a, Point3D b) =>
        NavMetric.Chebyshev(a, b) <= SameStationTiles && NavMetric.SameFloor(a, b);

    private static int NearestIndex(int count, Func<int, Point3D> at, Point3D from)
    {
        var best = NoIndex;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < count; i++)
        {
            var distance = NavMetric.Chebyshev(from, at(i));

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }
}
