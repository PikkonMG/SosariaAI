using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class SpawnPlacementRulesTests
{
    private static readonly Point3D Bank = new(1317, 3773, 0);

    [Fact]
    public void BankTile_WithinThreeTilesOfTheBank_TheSameEveryBoot()
    {
        var tiles = new HashSet<Point3D>();

        for (var i = 1; i <= 50; i++)
        {
            var id = $"Felucca:tam#{i}";

            for (var attempt = 0; attempt < SpawnPlacementRules.BankTries; attempt++)
            {
                var tile = SpawnPlacementRules.BankTile(Bank, id, attempt);

                Assert.InRange(NavMetric.Chebyshev(tile, Bank), 0, SpawnPlacementRules.BankScatterTiles);
                Assert.Equal(tile, SpawnPlacementRules.BankTile(Bank, id, attempt));
                tiles.Add(tile);
            }
        }

        // The crowd spreads over the bank's steps, not onto one tile.
        Assert.True(tiles.Count > SpawnPlacementRules.BankScatterTiles * SpawnPlacementRules.BankScatterTiles);
    }

    [Fact]
    public void Decisive_TheLastTestAnyTileFailed_NotTheMostCounted()
    {
        // Jhelom on the real map: the sea round the scattered spot has no floor, and the
        // nine free tiles by the bank had no route out.
        var rejects = new Dictionary<SpawnReject, int>
        {
            [SpawnReject.NoFloor] = 1374,
            [SpawnReject.Blocked] = 3,
            [SpawnReject.Sealed] = 9
        };

        Assert.Equal(SpawnReject.Sealed, SpawnPlacementRules.Decisive(rejects));
        Assert.Equal(SpawnReject.Blocked, SpawnPlacementRules.Decisive(new Dictionary<SpawnReject, int> { [SpawnReject.Blocked] = 1 }));
        Assert.Equal(SpawnReject.NoFloor, SpawnPlacementRules.Decisive(new Dictionary<SpawnReject, int>()));
    }

    [Fact]
    public void Summary_NamesEachFailedTestWithItsCount()
    {
        var rejects = new Dictionary<SpawnReject, int> { [SpawnReject.Sealed] = 189, [SpawnReject.Blocked] = 2 };

        Assert.Equal("no free tile the engine would spawn on 2, no route out to the roads 189", SpawnPlacementRules.Summary(rejects));
        Assert.Equal(string.Empty, SpawnPlacementRules.Summary(null));
    }
}
