using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;
using Moves = Server.Movement.Movement;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// The walk stall spots of a live activity log, on the real Felucca tiles. This process
/// has no world items, so a door stands open here, as the character's own step opens it.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapStallTests
{
    /// <summary>A ghost's reach to an ankh.</summary>
    private const int AnkhReach = 2;

    /// <summary>Where the ghosts stood on the Spirituality shrine, and the shrine's arrival inside it.</summary>
    private static readonly Point3D OnTheShrine = new(1590, 2485, 5);

    private static readonly Point3D ShrineArrival = new(1595, 2490, 0);

    /// <summary>
    /// Where walkers stood, and the leg the engine path would not take: the way runs
    /// through a doorway, and the engine plans round a shut door as round a wall. Trinsic,
    /// two Skara Brae homes, and a Britain shop floor.
    /// </summary>
    private static readonly (Point3D At, Point3D Leg)[] DoorLegs =
    [
        (new Point3D(1812, 2823, 0), new Point3D(1812, 2825, 0)),
        (new Point3D(588, 2152, 0), new Point3D(587, 2146, 0)),
        (new Point3D(632, 2190, 0), new Point3D(630, 2194, 0)),
        (new Point3D(1492, 1626, 10), new Point3D(1494, 1619, 20))
    ];

    /// <summary>A Trinsic home written at the moongate's height, over ground a floor higher.</summary>
    private static readonly Point3D SunkHome = new(1815, 2917, -20);

    private static readonly Point3D TrinsicMoongate = new(1828, 2949, -20);

    /// <summary>
    /// Two Yew bank crowd seats on the bank's south wall, whose only floor is the roof at 20,
    /// and the street in front of them under the eaves.
    /// </summary>
    private static readonly Point3D[] YewWallSeats = [new(652, 823, 20), new(655, 823, 20)];

    private static readonly Point3D YewBankStreet = new(652, 822, 0);

    [RealMapFact]
    public void ShrineArrival_TileRouteEndsOnATileThatStands()
    {
        var walker = RealMapWorld.Walker();

        foreach (var reach in new[] { 0, AnkhReach })
        {
            var route = TileRoute.Find(OnTheShrine, ShrineArrival, walker, RealMapWorld.IsIndoor, reach, out var why);

            Assert.True(route.Count > 0, why);
            Assert.All(route, point => Assert.Equal(point.Z, walker.FloorNear(point.X, point.Y, point.Z)));
        }
    }

    [RealMapFact]
    public void DoorLegs_OwnStepsReachTheLeg()
    {
        var walker = RealMapWorld.Walker();

        foreach (var (at, leg) in DoorLegs)
        {
            var steps = FloorSteps.Around(at, leg, 0, walker.Step);

            Assert.True(steps != null, $"no steps from {at} to {leg}");
            var end = Walk(at, steps, walker.Step);
            Assert.True(WalkArrival.Arrived(end, leg, 0, checkFloor: true), $"{at} to {leg} ended at {end}");
        }
    }

    [RealMapFact]
    public void SunkHome_TakesTheGroundAndRoutesFromTheMoongate()
    {
        var walker = RealMapWorld.Walker();

        var home = ArrivalHeight.PreferGround(RealMapWorld.Felucca, SunkHome);

        Assert.Equal(home.Z, walker.FloorNear(home.X, home.Y, home.Z));
        Assert.NotEmpty(TileRoute.Find(TrinsicMoongate, home, walker, RealMapWorld.IsIndoor));
    }

    [RealMapFact]
    public void YewWallSeats_AreRoofsOverAWall_AndMoveToTheTileBeside()
    {
        var map = RealMapWorld.Felucca;
        var walker = RealMapWorld.Walker();

        foreach (var seat in YewWallSeats)
        {
            Assert.True(ArrivalHeight.IsRoofOverBlockedTile(map, seat), $"{seat}");
            var street = ArrivalHeight.StreetSpot(map, seat);

            Assert.Equal(1, NavMetric.Chebyshev(seat, street));
            Assert.Equal(street.Z, walker.FloorNear(street.X, street.Y, street.Z));
            Assert.False(ArrivalHeight.IsRoofOverBlockedTile(map, street), $"{street}");
        }

        Assert.False(ArrivalHeight.IsRoofOverBlockedTile(map, YewBankStreet));
        Assert.Equal(YewBankStreet, ArrivalHeight.StreetSpot(map, YewBankStreet));
    }

    [RealMapFact]
    public void MurdererGhost_KeepsOffTheTowns_AndTakesTheOpenMoongates()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [new NavNode { Name = "shrine", X = OnTheShrine.X, Y = OnTheShrine.Y, Z = OnTheShrine.Z, Connects = [] }]
        );

        var bars = PathSearchBars.For(graph, RealMapWorld.Felucca, OnTheShrine, murderer: true);

        Assert.NotNull(bars);
        Assert.False(bars.NoMoongates);
    }

    private static Point3D Walk(Point3D from, IReadOnlyList<Direction> steps, TileStep step)
    {
        var at = from;

        foreach (var direction in steps)
        {
            var x = at.X;
            var y = at.Y;
            Moves.Offset(direction, ref x, ref y);
            Assert.True(step(at.X, at.Y, at.Z, x, y, out var z), $"step {direction} from {at} is refused");
            at = new Point3D(x, y, z);
        }

        return at;
    }
}
