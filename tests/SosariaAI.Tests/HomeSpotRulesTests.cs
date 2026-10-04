using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class HomeSpotRulesTests
{
    private const int Leash = 100;
    private static readonly Point3D Spawn = new(3000, 3000, 0);
    private static readonly Point3D NearBank = new(3050, 3010, 0);
    private static readonly Point3D FarBank = new(1425, 1695, 0);

    [Fact]
    public void Resolve_NoSpawn_IsBritainBank() =>
        Assert.Equal(CharactersFile.DefaultBankSpot, HomeSpotRules.Resolve(NearBank, Point3D.Zero, Leash));

    [Fact]
    public void Resolve_BankInsideLeash_IsBank() =>
        Assert.Equal(NearBank, HomeSpotRules.Resolve(NearBank, Spawn, Leash));

    [Fact]
    public void Resolve_BankBeyondLeash_IsSpawn() =>
        Assert.Equal(Spawn, HomeSpotRules.Resolve(FarBank, Spawn, Leash));

    [Fact]
    public void Resolve_NoBank_IsSpawn() =>
        Assert.Equal(Spawn, HomeSpotRules.Resolve(Point3D.Zero, Spawn, Leash));

    private static readonly Point3D BritainBank = new(1425, 1690, 0);
    private static readonly Point3D Butcher = new(1449, 1723, 6);
    private static readonly Point3D Provisioner = new(1469, 1668, 0);
    private static readonly Point3D TownCrierOnThePlaza = new(1421, 1698, 0);
    private static readonly Point3D Moonglow = new(4471, 1156, 0);

    [Fact]
    public void CornerFor_NoHomeSpot_StaysZero() =>
        Assert.Equal(Point3D.Zero, HomeSpotRules.CornerFor(Point3D.Zero, "Felucca:connor#2", Map.Internal, [Butcher]));

    [Fact]
    public void CornerFor_Fixture_KeepsTheAuthoredAnchor() =>
        Assert.Equal(NearBank, HomeSpotRules.CornerFor(NearBank, "Felucca:bran", Map.Internal, [Butcher]));

    [Fact]
    public void CornerFor_NoMap_KeepsTheAnchor() =>
        Assert.Equal(NearBank, HomeSpotRules.CornerFor(NearBank, "Felucca:connor#2", Map.Internal, [Butcher]));

    [Fact]
    public void TownPlaces_KeepsTheTownsShopsOffThePlazaOnceEachInAFixedOrder()
    {
        var places = HomeSpotRules.TownPlaces(
            [Provisioner, Moonglow, TownCrierOnThePlaza, Butcher, Provisioner, Point3D.Zero],
            BritainBank
        );

        Assert.Equal([Butcher, Provisioner], places);
        Assert.Empty(HomeSpotRules.TownPlaces([Butcher], Point3D.Zero));
        Assert.Empty(HomeSpotRules.TownPlaces((IEnumerable<Point3D>)null, BritainBank));
    }

    [Fact]
    public void PlaceFor_IsStablePerIdAndSpreadsCopiesOverThePlaces()
    {
        List<Point3D> places = [Butcher, Provisioner, new(1437, 1647, 0), new(1416, 1754, 10)];
        var used = new HashSet<Point3D>();

        for (var i = 1; i <= Copies; i++)
        {
            var id = $"Felucca:connor#{i}";
            var place = HomeSpotRules.PlaceFor(places, id, 0);

            Assert.Contains(place, places);
            Assert.Equal(place, HomeSpotRules.PlaceFor(places, id, 0));
            used.Add(place);
        }

        Assert.Equal(places.Count, used.Count);
        Assert.Equal(Point3D.Zero, HomeSpotRules.PlaceFor([], "Felucca:connor#1", 0));
    }

    [Fact]
    public void IsFreeCorner_NotOnThePlazaNorInAHarvestBox()
    {
        Assert.True(HomeSpotRules.IsFreeCorner(Butcher, BritainBank));
        Assert.False(HomeSpotRules.IsFreeCorner(TownCrierOnThePlaza, BritainBank));
        Assert.False(HomeSpotRules.IsFreeCorner(CharactersFile.DefaultForestApproach, Moonglow));
    }

    [Fact]
    public void LoginSpot_CopyLogsInAtItsCornerNotOnTheBankTile()
    {
        Assert.Equal(Butcher, HomeSpotRules.LoginSpot(isCopy: true, isRed: false, Butcher, BritainBank));
        Assert.Equal(BritainBank, HomeSpotRules.LoginSpot(isCopy: false, isRed: false, Butcher, BritainBank));
        Assert.Equal(BritainBank, HomeSpotRules.LoginSpot(isCopy: true, isRed: true, Butcher, BritainBank));
        Assert.Equal(BritainBank, HomeSpotRules.LoginSpot(isCopy: true, isRed: false, Point3D.Zero, BritainBank));
    }

    private const int Copies = 100;

    private const int StreetZ = 7;
    private const int UpperFloorZ = 27;
    private static readonly int[] StreetAndUpperFloor = [StreetZ, UpperFloorZ];

    [Fact]
    public void FloorSpot_TakesTheFloorTheWalkerFindsNearTheAnchor()
    {
        // A tile with a street and an upper floor over it: the spot is on the anchor's
        // floor, at the height the walker stands there, not at the anchor's height.
        var walker = ColumnWorld.Walker(static (_, _) => StreetAndUpperFloor);
        var anchor = new Point3D(10, 10, StreetZ - 2);

        Assert.Equal(new Point3D(12, 10, StreetZ), HomeSpotRules.FloorSpot(walker, 12, 10, anchor));
        Assert.Equal(new Point3D(12, 10, UpperFloorZ), HomeSpotRules.FloorSpot(walker, 12, 10, new Point3D(10, 10, UpperFloorZ)));
    }

    [Fact]
    public void FloorSpot_NoFloorOnTheAnchorsFloor_IsNull()
    {
        var walker = ColumnWorld.Walker(static (x, _) => x == 12 ? ColumnWorld.Solid : ColumnWorld.GroundOnly);

        Assert.Null(HomeSpotRules.FloorSpot(walker, 12, 10, Point3D.Zero));
        Assert.Null(HomeSpotRules.FloorSpot(TestWalkers.Flat, 12, 10, new Point3D(10, 10, UpperFloorZ)));
        Assert.Null(HomeSpotRules.FloorSpot(null, 12, 10, Point3D.Zero));
    }
}
