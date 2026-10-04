using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// Leisure targets stay near home: named landmarks and towns, never dungeon
/// interiors or raw item names.
/// </summary>
public static class LeisureRules
{
    public static bool IsPlaceOfInterest(string kind) =>
        kind is SightseeRules.KindShrine or SightseeRules.KindBank or SightseeRules.KindTown;

    public static bool IsDungeonInterior(string kind, string name)
    {
        if (string.Equals(kind, SightseeRules.KindDungeon, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, SightseeRules.KindHunt, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.Contains("entrance", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("area ", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("level ", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("orc fort", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("caves", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRawItemName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        var space = name.LastIndexOf(' ');

        if (space < 0)
        {
            return false;
        }

        var tail = name[(space + 1)..];
        var digits = 0;
        var dash = 0;

        for (var i = 0; i < tail.Length; i++)
        {
            if (char.IsDigit(tail[i]))
            {
                digits++;
            }
            else if (tail[i] == '-')
            {
                dash++;
            }
            else
            {
                return false;
            }
        }

        return digits >= 3 && dash >= 1;
    }

    public static bool MayPick(
        string name,
        string kind,
        Point3D location,
        Point3D homeSpot,
        int radius,
        bool sameFacet,
        bool canRoute
    )
    {
        if (!sameFacet || !canRoute)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(name) || IsRawItemName(name) || IsDungeonInterior(kind, name))
        {
            return false;
        }

        if (!IsPlaceOfInterest(kind) && !string.Equals(kind, SightseeRules.KindHealer, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var reach = radius > 0 ? radius : CareerSettings.DefaultLeisureRadius;
        return NavMetric.Chebyshev(location, homeSpot) <= reach;
    }
}
