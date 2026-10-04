using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// Where a red can go without the guards: on foot on a murderer's roads, which step through
/// the unguarded Felucca moongates and never onto a node the guards cover (see
/// <see cref="PathSearchBars"/>), or by a recall it can cast now to a rune whose landing lies
/// outside the guards. A goal under the guards is no goal at all: a red that walks or recalls
/// into them dies there. A dungeon goal is judged at its door, which is where the walk and the
/// rune both come in. Every red outing (camp, dungeon, hunt ground) is offered only where this
/// says the red can get to: from Buccaneer's Den, an island, 101 delves, 69 hunts and 45 camps
/// in half an hour failed on the way out.
/// </summary>
public static class RedGangReach
{
    /// <summary>
    /// The road nodes a walker reaches from a start node, on its own roads, kept for one camp
    /// slot: a gang's mates set out from the same few nodes, and each set is a full road
    /// search on the world thread.
    /// </summary>
    private static readonly Dictionary<(NavGraph Graph, string Start, bool Murderer, bool OpenAroundStart, IReadOnlySet<long> Pads), HashSet<string>> WalkableByStart = new();

    private static long _walkableSlot = long.MinValue;

    /// <summary>A goal a red reaches: never one under the guards, else by the road or by a rune.</summary>
    public static bool Reaches(bool guardedGoal, bool walkable, bool recallable) => !guardedGoal && (walkable || recallable);

    /// <summary>True when <paramref name="red"/> can get to <paramref name="goal"/> now without meeting the guards.</summary>
    public static bool CanReach(SosariaCharacter red, Point3D goal)
    {
        if (red?.Map == null || red.Map == Map.Internal || goal == Point3D.Zero)
        {
            return false;
        }

        var entry = RuneShelf.LandingFor(red.Map, goal);

        return Reaches(
            RuneShelf.BarredFor(red, entry, red.Map),
            Walks(red, red.Location, entry),
            Recalls(red, goal)
        );
    }

    /// <summary>
    /// Adds to <paramref name="unmet"/> each outing of a red's run whose target the red cannot
    /// get to now (see <see cref="CanReach"/>): a hot-spot camp, the door of its dungeon hall,
    /// its hunt ground. The scorer then never picks it, and the run takes an outing that can
    /// start. A blue's outings are left alone.
    /// </summary>
    public static void AddUnreachableOutings(SosariaCharacter character, List<string> unmet)
    {
        if (character?.Disposition != DispositionKind.Outlaw || unmet == null)
        {
            return;
        }

        for (var i = 0; i < RedGangRunRules.Outings.Length; i++)
        {
            var outing = RedGangRunRules.Outings[i];

            if (!OutingReady(character, outing))
            {
                unmet.Add(outing);
            }
        }
    }

    /// <summary>True when a hot-spot camp lies within the red's reach now, so its run can start (see <see cref="HotSpots.CampFor"/>).</summary>
    public static bool CampInReach(SosariaCharacter red) => HotSpots.CampFor(red, Core.Now) != null;

    private static bool OutingReady(SosariaCharacter red, string outing) =>
        outing switch
        {
            SkillKinds.Conflict => CampInReach(red),
            SkillKinds.Dungeon => red.HuntHomeNow()?.Dungeon is { } hall && CanReach(red, hall.Arrival),
            _ => red.HuntHomeNow()?.Ground is { } ground && CanReach(red, ground.Arrival)
        };

    /// <summary>
    /// True when a recall the red can cast now, or once a mate steps off the landing, carries it
    /// near the goal, however short the trip: a road may not.
    /// </summary>
    public static bool Recalls(SosariaCharacter red, Point3D goal) =>
        RecallRules.CanRecallSoon(red, goal, RecallRules.NoRoadMinTripTiles);

    /// <summary>
    /// True when <paramref name="walker"/> walks from <paramref name="from"/> to the node
    /// nearest <paramref name="goal"/> on its own roads, on the map it stands on: a murderer's
    /// keep off the guards, and nobody's drop onto a floor above its reach
    /// (<see cref="PathSearchBars.ForWalker"/>). True without a graph, when there is nothing to judge by.
    /// </summary>
    public static bool Walks(SosariaCharacter walker, Point3D from, Point3D goal) => Walks(walker, walker?.Map, from, goal);

    /// <summary>
    /// <see cref="Walks(SosariaCharacter, Point3D, Point3D)"/> on <paramref name="map"/>: a
    /// fresh character's kit is packed before it stands anywhere.
    /// </summary>
    public static bool Walks(SosariaCharacter walker, Map map, Point3D from, Point3D goal)
    {
        var graph = NavWorld.GraphFor(map?.Name);
        var walkable = WalkableNodes(walker, graph, map, from);
        return walkable == null || graph.FindNearest(goal) is { } node && walkable.Contains(node.Name);
    }

    /// <summary>
    /// The road nodes a person can walk to from <paramref name="from"/>, on its own roads.
    /// Searched once per start node, bars and camp slot. Null without a graph.
    /// </summary>
    private static HashSet<string> WalkableNodes(SosariaCharacter walker, NavGraph graph, Map map, Point3D from)
    {
        if (walker == null || graph?.FindNearest(from) is not { } start)
        {
            return null;
        }

        var slot = HotSpotRules.SlotOf(Core.Now);

        if (slot != _walkableSlot)
        {
            WalkableByStart.Clear();
            _walkableSlot = slot;
        }

        var bars = PathSearchBars.ForWalker(walker, graph, map, from);
        var key = (graph, start.Name, bars?.KeepsOffGuards == true, bars?.OpenAroundStart == true, bars?.Pads);

        if (WalkableByStart.TryGetValue(key, out var known))
        {
            return known;
        }

        var cost = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var prev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        NavSearch.Explore(graph, start.Name, null, NavSearch.DefaultGateCost, null, null, cost, prev, bars, walkAway: true);
        var walkable = new HashSet<string>(cost.Keys, StringComparer.OrdinalIgnoreCase);
        WalkableByStart[key] = walkable;
        return walkable;
    }
}
