using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Admin;

/// <summary>A staff travel button: the name shown and the tile it lands on.</summary>
public readonly record struct TravelSpot(string Label, Point3D At);

/// <summary>
/// Button ids, travel lists and text for the staff panel. Pure. No world objects.
/// </summary>
public static class PanelRules
{
    /// <summary>Tiles around the staff member that "near" covers for the counts.</summary>
    public const int NearRange = 20;

    /// <summary>Travel buttons per section; the panel must fit a 800x600 client.</summary>
    public const int MaxSpotsPerSection = 32;

    /// <summary>Tiles searched around a travel spot for one a staff member can land on.</summary>
    public const int LandingRadius = 8;

    public const int ButtonRefresh = 1;
    public const int ButtonWriteStatus = 2;
    public const int ButtonFirstTimeSetup = 3;

    /// <summary>A travel button id is its section times this plus its place in the list.</summary>
    public const int SectionSize = 1000;

    public const int TownSection = 1;
    public const int DungeonSection = 2;

    public static int SpotButton(int section, int index) => section * SectionSize + index;

    public static bool TryReadSpot(int buttonId, out int section, out int index)
    {
        section = buttonId / SectionSize;
        index = buttonId % SectionSize;
        return section is TownSection or DungeonSection && index < MaxSpotsPerSection;
    }

    /// <summary>A travel spot at each place's door.</summary>
    public static List<TravelSpot> PlaceSpots(IEnumerable<Place> places)
    {
        var spots = new List<TravelSpot>();

        foreach (var place in places ?? [])
        {
            spots.Add(new TravelSpot(place.Name, place.Door));
        }

        return spots;
    }

    /// <summary>
    /// Named spots with a real tile, one per name, sorted by name, at most
    /// <paramref name="limit"/>. The first spot seen for a name wins.
    /// </summary>
    public static List<TravelSpot> Spots(IEnumerable<TravelSpot> candidates, int limit)
    {
        var byName = new SortedDictionary<string, TravelSpot>(StringComparer.OrdinalIgnoreCase);

        foreach (var spot in candidates)
        {
            if (!string.IsNullOrWhiteSpace(spot.Label) && spot.At != Point3D.Zero)
            {
                byName.TryAdd(spot.Label.Trim(), spot with { Label = spot.Label.Trim() });
            }
        }

        var list = new List<TravelSpot>(byName.Values);

        if (list.Count > limit)
        {
            list.RemoveRange(limit, list.Count - limit);
        }

        return list;
    }

    /// <summary>
    /// Where a staff member sent to <paramref name="spot"/> lands: the nearest tile, ring by
    /// ring out to <see cref="LandingRadius"/>, whose floor the walker stands on, that is
    /// not a step-off tile (a gate pad, a ladder or a stair), and from which one step leads
    /// onto a tile that is not one either. The floor is looked for near the spot's own
    /// height first and then near <paramref name="groundZ"/>, because a marker's height is
    /// often a guess: the Britain sewer entrance reads 0, and its floor stands at 24 beside
    /// a ladder over rock. Null when no tile in reach passes.
    /// </summary>
    /// <param name="groundZ">The land height at the spot.</param>
    /// <param name="walker">How a walker stands and steps.</param>
    /// <param name="stepOff">True for a tile a person must not be left on, at a floor height.</param>
    public static Point3D? Landing(Point3D spot, int groundZ, TileWalker walker, Func<int, int, int, bool> stepOff)
    {
        if (walker == null || stepOff == null)
        {
            return null;
        }

        int[] heights = groundZ == spot.Z ? [spot.Z] : [spot.Z, groundZ];

        foreach (var height in heights)
        {
            for (var radius = 0; radius <= LandingRadius; radius++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var x = spot.X + dx;
                        var y = spot.Y + dy;

                        if (NavMetric.Chebyshev(spot, new Point2D(x, y)) != radius)
                        {
                            continue;
                        }

                        if (walker.FloorNear(x, y, height) is { } floor &&
                            !stepOff(x, y, floor) &&
                            CanWalkAway(x, y, floor, walker, stepOff))
                        {
                            return new Point3D(x, y, floor);
                        }
                    }
                }
            }
        }

        return null;
    }

    private static bool CanWalkAway(int x, int y, int z, TileWalker walker, Func<int, int, int, bool> stepOff)
    {
        foreach (var (dx, dy) in TileGrid.Neighbours)
        {
            if (walker.Step(x, y, z, x + dx, y + dy, out var toZ) && !stepOff(x + dx, y + dy, toZ))
            {
                return true;
            }
        }

        return false;
    }

    public static string WhereLine(string location, string facet, int live) =>
        $"You are at {location} on {facet}. {live} live.";

    public static string MovedTo(string label) => $"Moved to {label}.";

    public static string FacetLine(string facet, int live) => $"{facet}: {live} live";

    public static string StateLine(FleetCounts counts) =>
        $"Reds {counts.Reds}, ghosts {counts.Ghosts}, in combat {counts.InCombat}, " +
        $"in dungeons {counts.InDungeons}, supplies low {counts.SuppliesLow}, parties {counts.Parties}";

    public static string NearLine(int live) => $"Within {NearRange} tiles: {live} live";

    public static string WatchdogLine(int stuck, int roadMoves, int rescores) =>
        $"Watchdog: {stuck} stuck now, {roadMoves} moved to a road, {rescores} plans dropped";
}
