using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>A named meeting point where Order and Chaos patrols gather and clash.</summary>
public readonly record struct FactionSpot(string Name, Point3D Location);

/// <summary>
/// Felucca's faction meeting points: road crossings and gate roads outside the towns of the
/// Second Age, each a road node of the nav graph and at least twenty tiles past the guard line,
/// clear of any bank, healer, moongate and shrine; war bands also ride to the PvP hot spots
/// (see <see cref="HotSpots"/>). Trammel has none: its rules forbid the fight.
/// </summary>
public static class FactionSpots
{
    private static readonly Dictionary<Map, IReadOnlyList<FactionSpot>> WithHotSpots = new();

    private static readonly FactionSpot[] Felucca =
    [
        new("the Britain north crossroads", new Point3D(1471, 1330, 0)),
        new("the Despise road", new Point3D(1358, 1107, 0)),
        new("the west Britain road", new Point3D(1055, 1442, 0)),
        new("the Trinsic road", new Point3D(1594, 2193, 0)),
        new("the west Trinsic gate road", new Point3D(1775, 2762, 0)),
        new("the Cove road", new Point3D(2313, 1156, 0)),
        new("the Minoc south road", new Point3D(2555, 751, 0)),
        new("the Yew crossroads", new Point3D(554, 1293, 0)),
        new("the Vesper west road", new Point3D(2699, 956, 0))
    ];

    public static IReadOnlyList<FactionSpot> For(string facet) =>
        string.Equals(facet, FacetNames.Felucca, StringComparison.OrdinalIgnoreCase) ? Felucca : [];

    /// <summary>The road spots of the map's facet, then its PvP hot spots, where war bands sweep for the other side too.</summary>
    public static IReadOnlyList<FactionSpot> ForMap(Map map)
    {
        if (map == null)
        {
            return [];
        }

        if (WithHotSpots.TryGetValue(map, out var known))
        {
            return known;
        }

        var hot = HotSpots.SweepSpots(map);
        var road = For(map.Name);

        if (hot.Count == 0)
        {
            return road;
        }

        var all = new List<FactionSpot>(road);
        all.AddRange(hot);
        WithHotSpots[map] = all;
        return all;
    }

    /// <summary>
    /// The open spot nearest <paramref name="from"/> that lies inside the person's leash, or
    /// null. Both sides choose among the same open spots, so their patrols meet.
    /// </summary>
    public static FactionSpot? Pick(
        IReadOnlyList<FactionSpot> spots,
        DateTime now,
        Point3D from,
        Point3D homeSpot,
        int leashRadius
    )
    {
        if (spots == null || spots.Count == 0)
        {
            return null;
        }

        FactionSpot? best = null;
        var bestDistance = int.MaxValue;
        var open = FactionRules.ActiveIndexes(FactionRules.SlotOf(now), spots.Count);

        for (var i = 0; i < open.Length; i++)
        {
            var spot = spots[open[i]];

            if (HomeLeash.BeyondLeash(spot.Location, homeSpot, leashRadius))
            {
                continue;
            }

            var distance = NavMetric.Chebyshev(from, spot.Location);

            if (distance < bestDistance)
            {
                best = spot;
                bestDistance = distance;
            }
        }

        return best;
    }
}
