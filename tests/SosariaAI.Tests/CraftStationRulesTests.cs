using Server;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class CraftStationRulesTests
{
    private static readonly Point3D Door = new(1500, 1580, 20);

    [Fact]
    public void PairSpot_StandsBetweenAnAnvilAndItsForge()
    {
        Point3D[] anvils = [new(1504, 1580, 20)];
        Point3D[] forges = [new(1506, 1580, 20)];

        var spot = CraftStationRules.PairSpot(anvils, forges, Door);

        Assert.NotNull(spot);
        Assert.Equal(CraftStationRules.StandRange, spot.Value.Range);
        Assert.True(NavMetric.Chebyshev(spot.Value.Spot, anvils[0]) + spot.Value.Range <= CraftStationRules.EngineReach);
        Assert.True(NavMetric.Chebyshev(spot.Value.Spot, forges[0]) + spot.Value.Range <= CraftStationRules.EngineReach);
    }

    [Fact]
    public void PairSpot_AForgeThreeTilesOff_IsWorkedFromTheTileBetween()
    {
        // Skara Brae: the anvil at 633 and the forge at 636 on one row.
        Point3D[] anvils = [new(633, 2195, 0)];
        Point3D[] forges = [new(636, 2195, 0)];

        var spot = CraftStationRules.PairSpot(anvils, forges, new Point3D(620, 2180, 0));

        Assert.NotNull(spot);
        Assert.Equal(0, spot.Value.Range);
        Assert.True(NavMetric.Chebyshev(spot.Value.Spot, anvils[0]) <= CraftStationRules.EngineReach);
        Assert.True(NavMetric.Chebyshev(spot.Value.Spot, forges[0]) <= CraftStationRules.EngineReach);
    }

    [Fact]
    public void PairSpot_IgnoresAnvilsFarFromAnyForge()
    {
        Point3D[] anvils = [new(1504, 1580, 20)];
        Point3D[] forges = [new(1510, 1580, 20)];

        Assert.Null(CraftStationRules.PairSpot(anvils, forges, Door));
        Assert.Null(CraftStationRules.PairSpot([], forges, Door));
        Assert.Null(CraftStationRules.PairSpot(anvils, null, Door));
    }

    [Fact]
    public void PairSpot_TakesTheNearestPair()
    {
        Point3D[] anvils = [new(1520, 1580, 20), new(1502, 1580, 20)];
        Point3D[] forges = [new(1521, 1580, 20), new(1503, 1580, 20)];

        var spot = CraftStationRules.PairSpot(anvils, forges, Door);

        Assert.NotNull(spot);
        Assert.True(NavMetric.Chebyshev(Door, spot.Value.Spot) < NavMetric.Chebyshev(Door, anvils[0]));
    }

    [Fact]
    public void NearestSpot_PicksTheClosestHeat()
    {
        Point3D near = new(1502, 1581, 20);
        Point3D far = new(1509, 1589, 20);

        Assert.Equal(near, CraftStationRules.NearestSpot([far, near], Door).Value.Spot);
        Assert.Null(CraftStationRules.NearestSpot([], Door));
    }

    [Fact]
    public void PairSpots_OneSpotPerStation_OnTheAnvilsFloor()
    {
        // The Britain smithy on its raised floor: an anvil and a forge two tiles apart at z 30.
        Point3D[] anvils = [new(1423, 1556, 30), new(1423, 1556, 30)];
        Point3D[] forges = [new(1424, 1558, 30), new(1424, 1557, 30), new(1424, 1558, 0)];

        var spots = CraftStationRules.PairSpots(anvils, forges);

        Assert.Single(spots);
        Assert.Equal(30, spots[0].Spot.Z);
        Assert.True(NavMetric.Chebyshev(spots[0].Spot, anvils[0]) <= CraftStationRules.EngineReach);
    }

    [Fact]
    public void PickPlace_SpreadsCraftersOverStations()
    {
        StationShop near = new(new Point3D(1423, 1557, 30), new Point3D(1423, 1557, 30));
        StationShop far = new(new Point3D(1362, 1574, 30), new Point3D(1362, 1574, 30));
        var from = new Point3D(1425, 1600, 20);

        Assert.Equal(near, CraftStationRules.PickPlace([near, far], [0, 0], from));
        Assert.Equal(far, CraftStationRules.PickPlace([near, far], [2, 0], from));
        Assert.Equal(far, CraftStationRules.PickPlace([near, far], [CraftStationRules.FullCrowd, CraftStationRules.FullCrowd - 1], from));
        Assert.Null(CraftStationRules.PickPlace([near], [CraftStationRules.FullCrowd], from));
        Assert.Null(CraftStationRules.PickPlace([], [], from));
    }

    [Fact]
    public void Distinct_OneEntryPerStation()
    {
        StationShop marker = new(new Point3D(1418, 1547, 30), new Point3D(1423, 1557, 30));
        StationShop anvil = new(new Point3D(1423, 1557, 30), new Point3D(1423, 1557, 30));
        StationShop west = new(new Point3D(1362, 1574, 30), new Point3D(1362, 1574, 30));

        var kept = CraftStationRules.Distinct([marker, anvil, west]);

        Assert.Equal([marker, west], kept);
    }

    [Fact]
    public void CanWorkAt_NeedsTheAnvilAndTheForgeInTheEnginesReach()
    {
        Point3D[] anvils = [new(1504, 1580, 20)];
        Point3D[] forges = [new(1505, 1580, 20)];

        Assert.True(CraftStationRules.CanWorkAt(new Point3D(1506, 1582, 20), anvils, forges, CraftStation.AnvilAndForge));
        Assert.False(CraftStationRules.CanWorkAt(new Point3D(1507, 1582, 20), anvils, forges, CraftStation.AnvilAndForge));
        Assert.False(CraftStationRules.CanWorkAt(new Point3D(1504, 1582, 20), anvils, [], CraftStation.AnvilAndForge));
    }

    [Fact]
    public void CanWorkAt_ASmithOnTheFloorAboveCannotReach()
    {
        Point3D[] anvils = [new(1504, 1580, 20)];
        Point3D[] forges = [new(1505, 1580, 20)];
        var above = 20 + CraftStationRules.EngineHeight + 1;

        Assert.False(CraftStationRules.CanWorkAt(new Point3D(1504, 1581, above), anvils, forges, CraftStation.AnvilAndForge));
    }

    [Fact]
    public void CanWorkAt_HeatAloneForACook_AndAnywhereForACounterTrade()
    {
        Point3D[] heat = [new(1424, 1712, 20)];

        Assert.True(CraftStationRules.CanWorkAt(new Point3D(1426, 1714, 20), heat, [], CraftStation.Heat));
        Assert.False(CraftStationRules.CanWorkAt(new Point3D(1427, 1714, 20), heat, [], CraftStation.Heat));
        Assert.True(CraftStationRules.CanWorkAt(Door, [], [], CraftStation.None));
    }

    [Fact]
    public void WorkTiles_SeveralSmithsShareAnAnvilBesideItsForge()
    {
        // An anvil with its forge beside it is worked from twenty tiles, not one.
        Point3D anvil = new(1504, 1580, 20);
        Point3D forge = new(1505, 1580, 20);
        var stand = CraftStationRules.PairSpot([anvil], [forge], Door).Value;

        var tiles = CraftStationRules.WorkTiles(stand, [anvil], [forge], CraftStation.AnvilAndForge);

        Assert.True(tiles.Count >= CraftStationRules.FullCrowd);
        Assert.DoesNotContain(anvil, tiles);
        Assert.DoesNotContain(forge, tiles);
        Assert.All(tiles, tile => Assert.True(CraftStationRules.CanWorkAt(tile, [anvil], [forge], CraftStation.AnvilAndForge)));
    }

    [Fact]
    public void WorkTiles_AForgeFourTilesOffLeavesOnlyTheRowBetween()
    {
        const int middleX = 635;
        Point3D anvil = new(633, 2195, 0);
        Point3D forge = new(637, 2195, 0);
        var stand = CraftStationRules.PairSpot([anvil], [forge], new Point3D(620, 2180, 0)).Value;

        var tiles = CraftStationRules.WorkTiles(stand, [anvil], [forge], CraftStation.AnvilAndForge);

        Assert.Equal(CraftStationRules.EngineReach * 2 + 1, tiles.Count);
        Assert.All(tiles, tile => Assert.Equal(middleX, tile.X));
        Assert.Equal(stand.Spot, tiles[0]);
    }

    [Fact]
    public void WorkTiles_TheNearestTileToTheStandComesFirst()
    {
        Point3D heat = new(1424, 1712, 20);
        var stand = new StandSpot(heat, CraftStationRules.StandRange);

        var tiles = CraftStationRules.WorkTiles(stand, [heat], [], CraftStation.Heat);

        Assert.Equal(1, NavMetric.Chebyshev(heat, tiles[0]));
        Assert.Equal(CraftStationRules.EngineReach, NavMetric.Chebyshev(heat, tiles[^1]));
    }

    [Fact]
    public void NearestTile_TakesTheClosestFreeTile()
    {
        Point3D near = new(1501, 1580, 20);
        Point3D far = new(1506, 1584, 20);

        Assert.Equal(near, CraftStationRules.NearestTile([far, near], Door));
        Assert.Null(CraftStationRules.NearestTile([], Door));
    }
}
