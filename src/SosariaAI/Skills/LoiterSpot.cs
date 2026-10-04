using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Spawning;

namespace SosariaAI.Skills;

/// <summary>
/// Picks the spot a person stands about at: the one it wanted when there is room, else its
/// home corner, else the nearest of the town's shops, inns and docks with room. Counts the
/// people already there. World thread only.
/// </summary>
public static class LoiterSpot
{
    public static Point3D Choose(SosariaCharacter person, Point3D wanted)
    {
        var map = person?.Map;

        if (map == null || map == Map.Internal || wanted == Point3D.Zero)
        {
            return wanted;
        }

        var catalog = NavWorld.DestinationsFor(person.HomeFacet);
        var candidates = Candidates(catalog, NavWorld.GraphFor(person.HomeFacet), wanted, person.HomeCorner);
        var looks = new List<LoiterSpotLook>(candidates.Count);

        for (var i = 0; i < candidates.Count; i++)
        {
            looks.Add(Look(person, map, catalog, candidates[i]));

            if (!LoiterSpotRules.IsFull(looks[i]))
            {
                return candidates[i];
            }
        }

        var pick = LoiterSpotRules.Pick(looks);
        return pick == LoiterSpotRules.NoSpot ? wanted : candidates[pick];
    }

    /// <summary>
    /// The spots in order of preference: the wanted one, the home corner when it is in the
    /// same town, then the town's places nearest first. A spot in a harvest box is left out.
    /// </summary>
    private static List<Point3D> Candidates(DestinationCatalog catalog, NavGraph graph, Point3D wanted, Point3D homeCorner)
    {
        var candidates = new List<Point3D> { wanted };

        if (homeCorner != Point3D.Zero && homeCorner != wanted &&
            NavMetric.Chebyshev(homeCorner, wanted) <= HomeSpotRules.TownPlaceRadius)
        {
            candidates.Add(homeCorner);
        }

        var places = HomeSpotRules.TownPlaces(catalog, graph, BankPlaza.BankFor(catalog, wanted));
        places.Sort((a, b) => NavMetric.Chebyshev(wanted, a).CompareTo(NavMetric.Chebyshev(wanted, b)));

        for (var i = 0; i < places.Count; i++)
        {
            if (!WorkSites.IsWorkSpot(places[i]) && !candidates.Contains(places[i]))
            {
                candidates.Add(places[i]);
            }
        }

        return candidates;
    }

    private static LoiterSpotLook Look(SosariaCharacter person, Map map, DestinationCatalog catalog, Point3D spot)
    {
        var bank = BankPlaza.BankFor(catalog, spot);

        if (BankPlaza.Contains(spot, bank))
        {
            return new LoiterSpotLook(spot, Count(person, map, bank, BankPlaza.Range, idlersOnly: true), true);
        }

        return new LoiterSpotLook(spot, Count(person, map, spot, LoiterSpotRules.SpotRadius, idlersOnly: false), false);
    }

    private static int Count(SosariaCharacter person, Map map, Point3D at, int range, bool idlersOnly)
    {
        var count = 0;

        foreach (var mobile in map.GetMobilesInRange(at, range))
        {
            if (mobile != person && mobile is SosariaCharacter { Alive: true } other &&
                (!idlersOnly || IsIdler(other)))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>A person standing about on a step of its own, not banking, trading or in the crowd.</summary>
    private static bool IsIdler(SosariaCharacter other) =>
        other.Routine?.CurrentSkill?.Name is SkillKinds.Arrive or SkillKinds.Loiter or SkillKinds.IdleWander ||
        PracticeRules.IsPractice(other.Routine?.CurrentSkill?.Name);
}
