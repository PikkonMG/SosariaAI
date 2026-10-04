using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Social;

/// <summary>
/// Shard-wide heat by place: murders, deaths and red sightings warm the square they happened
/// in, and the heat halves every 45 minutes. A hot place gets fewer visits. The personal
/// <see cref="Navigation.DangerSpots"/> stay what one person ran from; this is what the whole
/// shard talks about. Not saved: a restart starts cold. Pure: no world objects.
/// </summary>
public sealed class DangerMap
{
    public const int CellTiles = 64;
    public const double HalfLifeMinutes = 45;
    public static readonly TimeSpan HalfLife = TimeSpan.FromMinutes(HalfLifeMinutes);

    public const double MurderHeat = 3.0;
    public const double DeathHeat = 1.0;
    public const double SightingHeat = 0.5;

    /// <summary>The fewest visits a place keeps, however hot it runs.</summary>
    public const double MinVisitFactor = 0.2;

    public const double FullVisits = 1.0;

    /// <summary>What is left after one half-life.</summary>
    public const double HalvedShare = 0.5;

    /// <summary>Heat below this is forgotten when the map is pruned.</summary>
    public const double ColdHeat = 0.05;

    /// <summary>The map prunes cold squares once it holds this many.</summary>
    public const int PruneAt = 4096;

    public static DangerMap Shared { get; } = new();

    private readonly Dictionary<(string Facet, int X, int Y), (double Heat, DateTime At)> _cells = new();

    public void Note(string facet, Point3D at, double heat, DateTime now)
    {
        if (heat <= 0 || string.IsNullOrWhiteSpace(facet))
        {
            return;
        }

        var key = Key(facet, at);
        var current = _cells.TryGetValue(key, out var cell) ? Decayed(cell.Heat, now - cell.At) : 0;
        _cells[key] = (current + heat, now);

        if (_cells.Count >= PruneAt)
        {
            Prune(now);
        }
    }

    /// <summary>The heat of the square holding <paramref name="at"/> at this moment.</summary>
    public double Heat(string facet, Point3D at, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(facet) || !_cells.TryGetValue(Key(facet, at), out var cell))
        {
            return 0;
        }

        return Decayed(cell.Heat, now - cell.At);
    }

    /// <summary>How much of its usual share a visit to this place keeps: 1 when cold, down to the floor.</summary>
    public double VisitFactor(string facet, Point3D at, DateTime now) => VisitFactorFor(Heat(facet, at, now));

    public static double VisitFactorFor(double heat) =>
        heat <= 0 ? FullVisits : Math.Max(MinVisitFactor, FullVisits / (FullVisits + heat));

    public static double Decayed(double heat, TimeSpan age) =>
        age <= TimeSpan.Zero ? heat : heat * Math.Pow(HalvedShare, age / HalfLife);

    /// <summary>The heat a journal event adds, or zero for news that is not danger.</summary>
    public static double HeatOf(string eventType) =>
        eventType switch
        {
            ShardEventType.Pk => MurderHeat,
            ShardEventType.Death => DeathHeat,
            ShardEventType.Red => SightingHeat,
            _ => 0
        };

    public static string ScreamLine(string place) =>
        string.IsNullOrWhiteSpace(place) ? "RED!!" : $"RED AT {place.ToUpperInvariant()}!!";

    private static (string Facet, int X, int Y) Key(string facet, Point3D at) =>
        (facet.ToLowerInvariant(), at.X / CellTiles, at.Y / CellTiles);

    private void Prune(DateTime now)
    {
        var cold = new List<(string, int, int)>();

        foreach (var (key, cell) in _cells)
        {
            if (Decayed(cell.Heat, now - cell.At) < ColdHeat)
            {
                cold.Add(key);
            }
        }

        for (var i = 0; i < cold.Count; i++)
        {
            _cells.Remove(cold[i]);
        }
    }
}
