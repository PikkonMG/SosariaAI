using System.Collections.Generic;
using System.Linq;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Navigation;
using SosariaAI.Spawning;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests.RealMap;

/// <summary>Home corners of Britain copies on the real Felucca tiles.</summary>
[Collection(RealMapCollection.Name)]
public class RealMapHomeCornerTests(ITestOutputHelper output)
{
    private const int Copies = 200;
    private const int PlannedFleet = 1600;

    /// <summary>With the old scatter round the bank, 192 of 200 corners fell back to the bank tile.</summary>
    private const int MostCopies = Copies * 9 / 10;

    /// <summary>No tile is the corner of more than this many copies.</summary>
    private const int MostPerTile = 6;

    private static readonly Point3D BritainBank = new(1425, 1690, 0);

    /// <summary>The street nodes of Britain's shops in the generated catalog, within a town's reach of its bank.</summary>
    private static readonly Point3D[] BritainShops =
    [
        new(1381, 1646, 30),
        new(1432, 1751, 14),
        new(1445, 1646, 10),
        new(1449, 1723, 6),
        new(1451, 1679, 0),
        new(1469, 1668, 0),
        new(1470, 1647, 17),
        new(1415, 1702, 6)
    ];

    [RealMapFact]
    public void CornerFor_BritainCopies_StandByTheirShopsNotInTheBank()
    {
        var map = RealMapWorld.Felucca;
        RealMapWorld.Walker();
        var planned = SpawnSpread.PlannedCount;
        SpawnSpread.PlannedCount = PlannedFleet;
        var places = HomeSpotRules.TownPlaces(BritainShops, BritainBank);
        var corners = new List<Point3D>();

        try
        {
            for (var i = 1; i <= Copies; i++)
            {
                corners.Add(HomeSpotRules.CornerFor(BritainBank, $"Felucca:connor#{i}", map, places));
            }
        }
        finally
        {
            SpawnSpread.PlannedCount = planned;
        }

        var offPlaza = corners.Count(corner => !BankPlaza.Contains(corner, BritainBank));
        var busiestTile = corners.GroupBy(corner => corner).Max(group => group.Count());
        var placesUsed = places.Count(place => corners.Any(corner => NavMetric.Chebyshev(corner, place) <= HomeSpotRules.PlaceScatterRadius));

        output.WriteLine($"{offPlaza} of {Copies} off the plaza, busiest tile {busiestTile}, places used {placesUsed} of {places.Count}");
        Assert.True(offPlaza >= MostCopies, $"{offPlaza} of {Copies} corners are off the bank plaza");
        Assert.True(busiestTile <= MostPerTile, $"{busiestTile} copies share one corner tile");
        Assert.Equal(places.Count, placesUsed);
        Assert.DoesNotContain(corners, WorkSites.IsWorkSpot);
    }
}
