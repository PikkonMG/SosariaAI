using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Configuration;

/// <summary>The name, size and harm rule of one map the server registered.</summary>
public readonly record struct MapShape(string Name, int Width, int Height, bool OpenPvp);

/// <summary>
/// The server's maps behind the facet names. A facet is found by its map's name, never by a
/// map slot: ModernUO's Map.Felucca is slot 0, whatever map the shard put there. A map is
/// stock when its name is one of the six stock names and its size is that map's stock size;
/// the Britannia tables (towns, gates, the Den) fit only a stock map. PvP follows the map's
/// own rules, as the engine does, unless characters.json closes it for that map. Nothing here
/// throws on a map the server does not have.
/// </summary>
public static class FacetMaps
{
    /// <summary>Felucca and Trammel from Mondain's Legacy on: the map files with the widened lost lands.</summary>
    public const int BritanniaWidth = 7168;

    /// <summary>Felucca and Trammel before Mondain's Legacy, as the older clients ship them.</summary>
    public const int BritanniaClassicWidth = 6144;

    public const int BritanniaHeight = 4096;
    public const int IlshenarWidth = 2304;
    public const int IlshenarHeight = 1600;
    public const int MalasWidth = 2560;
    public const int MalasHeight = 2048;
    public const int TokunoSide = 1448;
    public const int TerMurWidth = 1280;
    public const int TerMurHeight = 4096;

    /// <summary>
    /// True when <paramref name="name"/> is a stock map name and the size is that map's stock
    /// size (map-definitions.json of ModernUO; Felucca and Trammel also at the pre-ML width). Pure.
    /// </summary>
    public static bool IsStock(string name, int width, int height) =>
        FacetNames.TryStock(name, out var stock) &&
        stock switch
        {
            FacetNames.Felucca or FacetNames.Trammel =>
                height == BritanniaHeight && width is BritanniaWidth or BritanniaClassicWidth,
            FacetNames.Ilshenar => width == IlshenarWidth && height == IlshenarHeight,
            FacetNames.Malas => width == MalasWidth && height == MalasHeight,
            FacetNames.Tokuno => width == TokunoSide && height == TokunoSide,
            FacetNames.TerMur => width == TerMurWidth && height == TerMurHeight,
            _ => false
        };

    /// <summary>
    /// True when the stock tables fit this facet (a blank facet is <see cref="Default"/>): a stock
    /// name on a map of its stock size, or a stock name the server has not registered, as in a
    /// tool or a test that reads the tables without a world.
    /// </summary>
    public static bool IsStock(string facet)
    {
        var name = OrDefault(facet);

        if (!FacetNames.IsStockName(name))
        {
            return false;
        }

        return !TryFind(name, out var map) || IsStock(map.Name, map.Width, map.Height);
    }

    /// <summary>
    /// The map the server registered under this name, in any case. Internal is never one, and a
    /// number is not a name. False for a blank name.
    /// </summary>
    public static bool TryFind(string name, out Map map)
    {
        map = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();
        var maps = Map.Maps;

        for (var i = 0; i < maps.Length; i++)
        {
            var candidate = maps[i];

            if (candidate == null || IsInternal(candidate) ||
                !string.Equals(candidate.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            map = candidate;
            return true;
        }

        return false;
    }

    /// <summary>The facet's map, a blank facet being <see cref="Default"/>. False when the server has no such map.</summary>
    public static bool TryResolve(string facet, out Map map) => TryFind(OrDefault(facet), out map);

    /// <summary>The facet's map, a blank facet being <see cref="Default"/>; null when the server has no such map.</summary>
    public static Map Resolve(string facet) => TryResolve(facet, out var map) ? map : null;

    /// <summary>The maps the server registered at boot, Internal left out, in the order it read them.</summary>
    public static IReadOnlyList<MapShape> Registered()
    {
        var shapes = new List<MapShape>();

        foreach (var map in Map.AllMaps.ToArray())
        {
            if (map != null && !IsInternal(map))
            {
                shapes.Add(new MapShape(map.Name, map.Width, map.Height, RulesAllowHarm(map.Rules)));
            }
        }

        return shapes;
    }

    /// <summary>The engine lets a person harm a person under these rules: Felucca's, not Trammel's.</summary>
    public static bool RulesAllowHarm(MapRules rules) =>
        (rules & (MapRules.HarmfulRestrictions | MapRules.Internal)) == 0;

    /// <summary>
    /// Open PvP on this map: its rules let a person harm a person, and characters.json does not
    /// close it (maps.&lt;facet&gt;.openPvp false). Reds, duels, Order against Chaos, thieves'
    /// lifts and the red alarm live only here. On a map with Trammel's rules the engine refuses
    /// the blow, so no setting opens one.
    /// </summary>
    public static bool OpenPvp(Map map) => OpenPvp(map, SosariaSettings.Characters?.Maps);

    /// <summary><see cref="OpenPvp(Map)"/> with the per-map settings given. Pure but for the map's own rules.</summary>
    public static bool OpenPvp(Map map, IReadOnlyDictionary<string, MapToggle> settings) =>
        map != null && !IsInternal(map) && RulesAllowHarm(map.Rules) && !ClosedBySettings(map.Name, settings);

    /// <summary>
    /// <see cref="OpenPvp(Map)"/> for a facet by name. A stock name the server has not registered
    /// takes the stock rules: only Felucca's allow harm. False for a blank facet: a person bound
    /// to no facet yet keeps out of PvP life, as it did when the test was the name Felucca.
    /// </summary>
    public static bool OpenPvp(string facet)
    {
        if (string.IsNullOrWhiteSpace(facet))
        {
            return false;
        }

        if (TryFind(facet, out var map))
        {
            return OpenPvp(map);
        }

        return FacetNames.Felucca.Equals(facet.Trim(), StringComparison.OrdinalIgnoreCase) &&
               !ClosedBySettings(facet, SosariaSettings.Characters?.Maps);
    }

    /// <summary>
    /// The facet a blank facet means: the first enabled map in characters.json that the server
    /// has, Felucca first when it is one of them; with none enabled, Felucca when the server has
    /// it, else the server's first map; with no map at all, Felucca.
    /// </summary>
    public static string Default => DefaultFrom(SosariaSettings.Characters?.Maps, RegisteredNames());

    /// <summary><see cref="Default"/> from the given settings and registered map names. Pure.</summary>
    public static string DefaultFrom(IReadOnlyDictionary<string, MapToggle> maps, IReadOnlyList<string> registered)
    {
        string firstEnabled = null;

        if (maps != null)
        {
            foreach (var (name, toggle) in maps)
            {
                var index = toggle is { Enabled: true } ? IndexOf(registered, name) : -1;

                if (index < 0)
                {
                    continue;
                }

                if (FacetNames.Felucca.Equals(registered[index], StringComparison.OrdinalIgnoreCase))
                {
                    return registered[index];
                }

                firstEnabled ??= registered[index];
            }
        }

        if (firstEnabled != null)
        {
            return firstEnabled;
        }

        var felucca = IndexOf(registered, FacetNames.Felucca);

        if (felucca >= 0)
        {
            return registered[felucca];
        }

        return registered is { Count: > 0 } ? registered[0] : FacetNames.Felucca;
    }

    /// <summary>The facet itself, or <see cref="Default"/> for a blank one.</summary>
    public static string OrDefault(string facet) => string.IsNullOrWhiteSpace(facet) ? Default : facet;

    /// <summary>True when both name the same facet, in any case.</summary>
    public static bool Same(string facet, string other) =>
        string.Equals(OrDefault(facet).Trim(), OrDefault(other).Trim(), StringComparison.OrdinalIgnoreCase);

    private static List<string> RegisteredNames()
    {
        var names = new List<string>();

        foreach (var map in Map.AllMaps.ToArray())
        {
            if (map != null && !IsInternal(map))
            {
                names.Add(map.Name);
            }
        }

        return names;
    }

    private static int IndexOf(IReadOnlyList<string> names, string name)
    {
        for (var i = 0; names != null && !string.IsNullOrWhiteSpace(name) && i < names.Count; i++)
        {
            if (string.Equals(names[i], name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool ClosedBySettings(string name, IReadOnlyDictionary<string, MapToggle> settings) =>
        settings != null && !string.IsNullOrWhiteSpace(name) && settings.TryGetValue(name.Trim(), out var toggle) &&
        toggle?.OpenPvp == false;

    private static bool IsInternal(Map map) => ReferenceEquals(map, Map.Internal) || (map.Rules & MapRules.Internal) != 0;
}
