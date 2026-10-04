using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Navigation;

/// <summary>One armed trap: the tile it lies on, its height, and how far round it hurts (<see cref="TrapRules"/>).</summary>
public readonly record struct TrapZone(int X, int Y, int Z, int Reach);

/// <summary>
/// The armed traps of one map, looked up by tile: which tiles a trap hurts, and which lie
/// beside a trap. Built once from a list of traps; the lookups only read. Pure.
/// </summary>
public sealed class TrapField
{
    private readonly Dictionary<Point2D, List<int>> _hurtHeights = new();
    private readonly Dictionary<Point2D, List<int>> _trapHeights = new();
    private readonly List<TrapZone> _zones = [];

    /// <param name="zones">The traps; one with a negative reach never hurts and is left out.</param>
    public TrapField(IEnumerable<TrapZone> zones)
    {
        foreach (var zone in zones ?? [])
        {
            if (zone.Reach < TrapRules.OnTileReach)
            {
                continue;
            }

            _zones.Add(zone);
            AddHeight(_trapHeights, zone.X, zone.Y, zone.Z);

            for (var dx = -zone.Reach; dx <= zone.Reach; dx++)
            {
                for (var dy = -zone.Reach; dy <= zone.Reach; dy++)
                {
                    AddHeight(_hurtHeights, zone.X + dx, zone.Y + dy, zone.Z);
                }
            }
        }
    }

    public static TrapField Empty { get; } = new([]);

    /// <summary>How many traps that hurt the field holds.</summary>
    public int Count => _zones.Count;

    /// <summary>True when an armed trap hurts a person standing at the tile at that height.</summary>
    public bool Harms(int x, int y, int z) => Covers(_hurtHeights, x, y, z);

    /// <summary>True when a trap lies on the tile or on one of the eight beside it, within reach of that height.</summary>
    public bool BesideTrap(int x, int y, int z)
    {
        foreach (var (dx, dy) in TileGrid.Neighbours)
        {
            if (Covers(_trapHeights, x + dx, y + dy, z))
            {
                return true;
            }
        }

        return Covers(_trapHeights, x, y, z);
    }

    /// <summary>
    /// True when a trap hurts some tile of the box round two spots, grown by
    /// <paramref name="margin"/>: a walk between them may meet it.
    /// </summary>
    public bool AnyNear(Point3D a, Point3D b, int margin)
    {
        var reach = margin + TrapRules.MaxReach;
        var minX = Math.Min(a.X, b.X) - reach;
        var maxX = Math.Max(a.X, b.X) + reach;
        var minY = Math.Min(a.Y, b.Y) - reach;
        var maxY = Math.Max(a.Y, b.Y) + reach;

        for (var i = 0; i < _zones.Count; i++)
        {
            var zone = _zones[i];

            if (zone.X >= minX && zone.X <= maxX && zone.Y >= minY && zone.Y <= maxY)
            {
                return true;
            }
        }

        return false;
    }

    private static void AddHeight(Dictionary<Point2D, List<int>> heights, int x, int y, int z)
    {
        var tile = new Point2D(x, y);

        if (!heights.TryGetValue(tile, out var list))
        {
            list = [];
            heights[tile] = list;
        }

        list.Add(z);
    }

    private static bool Covers(Dictionary<Point2D, List<int>> heights, int x, int y, int z)
    {
        if (!heights.TryGetValue(new Point2D(x, y), out var list))
        {
            return false;
        }

        for (var i = 0; i < list.Count; i++)
        {
            if (TrapRules.ReachesHeight(list[i], z))
            {
                return true;
            }
        }

        return false;
    }
}
