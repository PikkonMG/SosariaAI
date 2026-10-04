using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// One PvP hot spot: its spoken name, what it is, the thing at its middle, the unguarded
/// standing spots round it where a gang camps and a sweep rides to, how much the reds favour
/// it, and how many PvP houses stand on its outskirts.
/// </summary>
public sealed record HotSpot(string Name, HotSpotKind Kind, Point3D Center, IReadOnlyList<Point3D> Camps, int Weight, int Houses);

/// <summary>
/// The hot spots of a facet with open PvP (see <see cref="HotSpotRules"/>), read once from the
/// world: each public moongate with open ground round it but the Den's own (the engine's own
/// guard regions decide which ground is open), the busy dungeon doors and the hot graveyard
/// from the destination catalog, and the reds' own town. Every camp stands where the guards
/// never come, so a red camps it without dying.
/// </summary>
public static class HotSpots
{
    // Direction order of the engine: North, Right, East, Down, South, Left, West, Up.
    private static readonly int[] StepX = [0, 1, 1, 1, 0, -1, -1, -1];
    private static readonly int[] StepY = [-1, -1, 0, 1, 1, 1, 0, -1];

    private static readonly ILogger logger = SosariaLog.For(typeof(HotSpots));
    private static readonly Dictionary<Map, IReadOnlyList<HotSpot>> ByMap = new();
    private static readonly Dictionary<Map, IReadOnlyList<FactionSpot>> SweepsByMap = new();
    private static readonly Dictionary<Map, IReadOnlyList<Point3D>> OpenGatesByMap = new();

    /// <summary>A sweep rides for the spot nearest a red sighting this close to it.</summary>
    public const int SightingTiles = 60;

    /// <summary>The hot spots of this map; none where the rules forbid PvP, or before the catalog is read.</summary>
    public static IReadOnlyList<HotSpot> For(Map map)
    {
        if (map == null || map == Map.Internal || !WorldPlay.OpenPvp(map))
        {
            return [];
        }

        if (ByMap.TryGetValue(map, out var known))
        {
            return known;
        }

        if (NavWorld.DestinationsFor(map.Name) is not { } catalog)
        {
            return [];
        }

        var spots = Build(map, catalog);
        ByMap[map] = spots;

        if (SosariaSettings.LogActivity)
        {
            var names = new string[spots.Count];

            for (var i = 0; i < spots.Count; i++)
            {
                names[i] = spots[i].Name;
            }

            logger.Information("{Count} PvP hot spots on {Map}: {Names}", spots.Count, map.Name, string.Join(", ", names));
        }

        return spots;
    }

    /// <summary>The hot spots as meeting points for a war band or a sweep: each spot at its first camp.</summary>
    public static IReadOnlyList<FactionSpot> SweepSpots(Map map)
    {
        if (map != null && SweepsByMap.TryGetValue(map, out var known))
        {
            return known;
        }

        var spots = For(map);

        if (spots.Count == 0)
        {
            return [];
        }

        var sweeps = new FactionSpot[spots.Count];

        for (var i = 0; i < spots.Count; i++)
        {
            sweeps[i] = new FactionSpot(spots[i].Name, spots[i].Camps[0]);
        }

        SweepsByMap[map] = sweeps;
        return sweeps;
    }

    /// <summary>
    /// The camp a red's run takes now: a weighted draw over the spots out of the Den within its
    /// reach (see <see cref="HotSpotRules.InReach"/>), the same for its whole gang in this slot
    /// when the mates stand on the same ground. Null when no spot is in reach. The trip counts
    /// the moongates a red may take (<see cref="OpenGates"/>): from the Den the Yew gate is one
    /// hop away, but 1,900 tiles in a straight line, and a red with no rune never camped it.
    /// </summary>
    public static (HotSpot Spot, Point3D Camp)? CampFor(SosariaCharacter red, DateTime now)
    {
        if (red == null)
        {
            return null;
        }

        var crew = CrewKey(red);
        var reach = new List<(HotSpot Spot, Point3D Camp)>();
        var weights = new List<int>();
        var gates = OpenGates(red.Map);

        foreach (var spot in For(red.Map))
        {
            if (!HotSpotRules.IsRunCamp(spot.Kind))
            {
                continue;
            }

            var camp = spot.Camps[HotSpotRules.CampIndex(crew, spot.Camps.Count)];

            if (HotSpotRules.InReach(
                    RedGangReach.Walks(red, red.Location, camp),
                    NavMetric.ByMoongate(red.Location, spot.Center, gates),
                    RedGangReach.Recalls(red, camp)
                ))
            {
                reach.Add((spot, camp));
                weights.Add(spot.Weight);
            }
        }

        var pick = HotSpotRules.PickIndex(weights, crew, HotSpotRules.SlotOf(now));
        return pick == HotSpotRules.NoSpot ? null : reach[pick];
    }

    /// <summary>
    /// Where a blue sweep rides: the hot spot nearest the red sighting the leader heard of,
    /// else the spot the rotation names this slot, inside the leader's leash either way, and
    /// never Buccaneer's Den: blues ride into the Den only on a planned raid (see <see cref="DenSpot"/>).
    /// </summary>
    public static FactionSpot? SweepTarget(SosariaCharacter leader, DateTime now)
    {
        if (leader == null)
        {
            return null;
        }

        var spots = For(leader.Map);
        var sweeps = SweepSpots(leader.Map);
        var leash = HomeLeash.ConfiguredRadius();
        var report = SosariaSettings.Journal?.FindReport(leader.Name, leader.Location, now, ShardEventType.Red, leader.HomeFacet);
        var weights = new int[spots.Count];

        for (var i = 0; i < spots.Count; i++)
        {
            if (!HotSpotRules.IsRunCamp(spots[i].Kind) || HomeLeash.BeyondLeash(spots[i].Center, leader.HomeSpot, leash))
            {
                continue;
            }

            if (report != null &&
                NavMetric.Chebyshev(new Point3D(report.X, report.Y, report.Z), spots[i].Center) <= SightingTiles)
            {
                return sweeps[i];
            }

            weights[i] = spots[i].Weight;
        }

        var pick = HotSpotRules.PickIndex(weights, CrewKey(leader), HotSpotRules.SlotOf(now));
        return pick == HotSpotRules.NoSpot ? null : sweeps[pick];
    }

    /// <summary>
    /// Buccaneer's Den as the meeting point of a blue Den raid (see <see cref="PartyRoads"/>):
    /// its square, where the reds hang about between runs. Null on a map without the Den.
    /// </summary>
    public static FactionSpot? DenSpot(Map map)
    {
        var spots = For(map);
        var sweeps = SweepSpots(map);

        for (var i = 0; i < spots.Count; i++)
        {
            if (spots[i].Kind == HotSpotKind.Den)
            {
                return sweeps[i];
            }
        }

        return null;
    }

    /// <summary>The nearest living, visible red out of the guards' reach, for a sweep to ride down.</summary>
    public static Mobile NearestRed(SosariaCharacter hunter)
    {
        var map = hunter?.Map;

        if (map == null || map == Map.Internal)
        {
            return null;
        }

        Mobile best = null;
        var bestDistance = int.MaxValue;

        foreach (var mobile in map.GetMobilesInRange(hunter.Location, FactionRules.InterceptRange))
        {
            if (mobile is not PlayerMobile { Alive: true, Hidden: false } red || red == hunter ||
                !PkRules.IsRed(red.Kills) || SosariaCharacter.UnderGuards(red) || !hunter.CanSee(red))
            {
                continue;
            }

            var distance = NavMetric.Chebyshev(hunter.Location, red.Location);

            if (distance < bestDistance)
            {
                best = red;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// The public moongates of this map whose pads stand out of the guards' reach: the gates a
    /// red steps through (<see cref="SosariaAI.Skills.GateTravel.MayTakeMoongate(bool, bool, bool, bool)"/>).
    /// None off Felucca, where a red takes no moongate.
    /// </summary>
    public static IReadOnlyList<Point3D> OpenGates(Map map)
    {
        if (map != Map.Felucca)
        {
            return [];
        }

        if (OpenGatesByMap.TryGetValue(map, out var known))
        {
            return known;
        }

        var gates = new List<Point3D>();

        foreach (var entry in PMList.Felucca.Entries)
        {
            if (!GuardCall.IsGuardedPlace(entry.Location, map))
            {
                gates.Add(entry.Location);
            }
        }

        OpenGatesByMap[map] = gates;
        return gates;
    }

    /// <summary>A gang's mates share a key; a lone red keys on itself.</summary>
    public static int CrewKey(SosariaCharacter character) =>
        character.OutlawGang != PkGangRules.NoGang ? character.OutlawGang : unchecked((int)character.Serial.Value);

    private static List<HotSpot> Build(Map map, DestinationCatalog catalog)
    {
        var spots = new List<HotSpot>();
        var walker = Standable.Walker(map);

        if (map == Map.Felucca)
        {
            AddGates(spots, map, walker);
            Add(spots, map, walker, HotSpotKind.Den, PkRules.BucsDenHaven);
        }

        for (var i = 0; i < catalog.All.Count; i++)
        {
            var destination = catalog.All[i];

            if (PkGangRules.IsDungeonMouth(destination.Kind, destination.Name, destination.X) &&
                HotSpotRules.IsBusyDungeon(destination.Role))
            {
                Add(spots, map, walker, HotSpotKind.DungeonDoor, destination.Arrival);
            }
            else if (destination.ParsedKind == DestinationKind.Hunt && HotSpotRules.IsHotGraveyard(destination.Name))
            {
                Add(spots, map, walker, HotSpotKind.Graveyard, destination.Arrival);
            }
        }

        return spots;
    }

    // Each public moongate of the facet with open ground round it, the Den's own left out.
    private static void AddGates(List<HotSpot> spots, Map map, TileWalker walker)
    {
        foreach (var entry in PMList.Felucca.Entries)
        {
            var camps = Camps(map, walker, entry.Location, HotSpotRules.GateCampTiles, includeCenter: false);

            if (HotSpotRules.GateIsHotSpot(
                    PkRules.InBuccaneersDen(entry.Location.X, entry.Location.Y),
                    GuardCall.IsGuardedPlace(entry.Location, map),
                    camps.Count
                ))
            {
                var yewGate = entry.Number == HotSpotRules.YewGateNumber;
                spots.Add(Spot(map, HotSpotKind.Moongate, entry.Location, camps, yewGate));
            }
        }
    }

    private static void Add(List<HotSpot> spots, Map map, TileWalker walker, HotSpotKind kind, Point3D center)
    {
        var camps = Camps(map, walker, center, HotSpotRules.NearCampTiles, includeCenter: true);

        if (camps.Count > 0)
        {
            spots.Add(Spot(map, kind, center, camps, yewGate: false));
        }
    }

    private static HotSpot Spot(Map map, HotSpotKind kind, Point3D center, List<Point3D> camps, bool yewGate) =>
        new(
            GossipLines.PlaceWord(PlaceNames.Of(camps[0], map)),
            kind,
            center,
            camps,
            HotSpotRules.WeightOf(kind, yewGate),
            HotSpotRules.HousesFor(kind, yewGate)
        );

    // The standing spots round a center, on its compass points, that the guards never reach.
    private static List<Point3D> Camps(Map map, TileWalker walker, Point3D center, int tiles, bool includeCenter)
    {
        var camps = new List<Point3D>();

        if (includeCenter && Open(map, walker, center) is { } middle)
        {
            camps.Add(middle);
        }

        for (var d = 0; d < HotSpotRules.CampDirections; d++)
        {
            var point = new Point3D(center.X + StepX[d] * tiles, center.Y + StepY[d] * tiles, center.Z);

            if (Open(map, walker, point) is { } camp)
            {
                camps.Add(camp);
            }
        }

        return camps;
    }

    private static Point3D? Open(Map map, TileWalker walker, Point3D at)
    {
        Point3D standing;

        if (walker.FloorNear(at.X, at.Y, at.Z) is { } floor)
        {
            standing = new Point3D(at.X, at.Y, floor);
        }
        else if (!TileRoute.TryNearestStanding(at, walker, TileRoute.SnapRadius, out standing))
        {
            return null;
        }

        return GuardCall.IsGuardedPlace(standing, map) ? null : standing;
    }
}
