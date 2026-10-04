using System;
using System.Collections.Generic;
using Server;
using Server.Items;

namespace SosariaAI.Navigation;

/// <summary>
/// The armed traps on each live map, read from the world's own trap items (ModernUO's
/// BaseTrap family: spike, saw, gas, fire column, giant spike, flame spurt, stone face and
/// mushroom traps) and read again every <see cref="RefreshMinutes"/> minutes, so a trap
/// placed or removed later is seen. Hidden traps count: players knew where they lay. The
/// same read keeps the teleporter pads of each map (<see cref="PadsFor"/>): a pad a walk
/// crosses by chance carries the walker off like a trap it did not see.
/// Before the world runs, as in a test process, no map has traps. World thread only.
/// </summary>
public static class TrapTiles
{
    public const int RefreshMinutes = 10;

    private static readonly long RefreshMs = (long)TimeSpan.FromMinutes(RefreshMinutes).TotalMilliseconds;
    private static Dictionary<Map, TrapField> _fields = new();
    private static Dictionary<Map, TrapField> _pads = new();
    private static long _readAt;
    private static bool _read;

    /// <summary>The traps of a map; empty for no map, the internal map, or a world not running.</summary>
    public static TrapField For(Map map) => Fresh(map) ? _fields.GetValueOrDefault(map) ?? TrapField.Empty : TrapField.Empty;

    /// <summary>
    /// The active teleporter pads of a map, each hurting only its own tile: a walk that must
    /// not be carried off goes round them. Empty as <see cref="For"/> is.
    /// </summary>
    public static TrapField PadsFor(Map map) => Fresh(map) ? _pads.GetValueOrDefault(map) ?? TrapField.Empty : TrapField.Empty;

    /// <summary>True when the map can hold traps and pads; the world is read again when the last read is old.</summary>
    private static bool Fresh(Map map)
    {
        if (map == null || map == Map.Internal || World.WorldState is WorldState.Initial or WorldState.Loading)
        {
            return false;
        }

        var now = Core.TickCount;

        if (!_read || now - _readAt >= RefreshMs)
        {
            ReadWorld();
            _readAt = now;
            _read = true;
        }

        return true;
    }

    /// <summary>The world's items just changed, as after First Time Setup: the next ask reads them again.</summary>
    public static void Reread() => _read = false;

    /// <summary>How far round its tile a trap of this engine type hurts; a subtype hurts as its base.</summary>
    public static int ReachOf(Type trapType) =>
        IsA<StoneFaceTrapNoDamage>(trapType) ? TrapRules.Harmless
        : IsA<StoneFaceTrap>(trapType) ? TrapRules.StoneFaceReach
        : IsA<FlameSpurtTrap>(trapType) ? TrapRules.FlameSpurtReach
        : IsA<MushroomTrap>(trapType) ? TrapRules.MushroomReach
        : TrapRules.OnTileReach;

    private static bool IsA<T>(Type type) => type != null && typeof(T).IsAssignableFrom(type);

    private static void ReadWorld()
    {
        var traps = new Dictionary<Map, List<TrapZone>>();
        var pads = new Dictionary<Map, List<TrapZone>>();

        foreach (var item in World.Items.Values)
        {
            if (item is not { Deleted: false, Parent: null } || item.Map == null || item.Map == Map.Internal)
            {
                continue;
            }

            if (item is BaseTrap trap)
            {
                Zones(traps, item.Map).Add(new TrapZone(trap.X, trap.Y, trap.Z, ReachOf(trap.GetType())));
            }
            else if (item is Teleporter { Active: true } pad)
            {
                Zones(pads, item.Map).Add(new TrapZone(pad.X, pad.Y, pad.Z, TrapRules.OnTileReach));
            }
        }

        _fields = Fields(traps);
        _pads = Fields(pads);
    }

    private static List<TrapZone> Zones(Dictionary<Map, List<TrapZone>> byMap, Map map)
    {
        if (!byMap.TryGetValue(map, out var list))
        {
            list = [];
            byMap[map] = list;
        }

        return list;
    }

    private static Dictionary<Map, TrapField> Fields(Dictionary<Map, List<TrapZone>> byMap)
    {
        var fields = new Dictionary<Map, TrapField>(byMap.Count);

        foreach (var (map, list) in byMap)
        {
            fields[map] = new TrapField(list);
        }

        return fields;
    }
}
