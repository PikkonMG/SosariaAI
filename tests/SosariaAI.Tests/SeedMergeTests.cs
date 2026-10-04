using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class SeedMergeTests
{
    private const int CloseSpan = 3;
    private const int FarSpan = 20;
    private const int OriginX = 0;
    private const int OriginY = 0;
    private const int OriginZ = 5;
    private const int LaterZ = 9;
    private const int StandX = 2;
    private const int StandY = 1;
    private const int StandZ = 0;
    private const int MarkerZ = 0;
    private const int HillZ = 36;
    private const int RoofZ = 20;
    private const int FloorZ = 7;
    private const string FirstName = "west";
    private const string SecondName = "east";
    private const string ThirdName = "north";
    private const string FirstKind = "bank";
    private const string SecondKind = "vendor";

    [Fact]
    public void Merge_SeedsThreeTilesApart_BecomeOne()
    {
        var later = new GraphSeed(SecondName, OriginX + CloseSpan, OriginY, LaterZ, SecondKind);
        var first = new GraphSeed(FirstName, OriginX, OriginY, OriginZ, FirstKind);

        var merged = SeedMerge.Merge([later, first]);

        Assert.Single(merged);
        Assert.Equal(first, merged[0].Head);
        Assert.Equal((OriginX + OriginX + CloseSpan) / 2, merged[0].X);
        Assert.Equal(OriginY, merged[0].Y);
        Assert.Equal(2, merged[0].Members.Count);
        Assert.True(CloseSpan <= SeedMerge.MergeChebyshev);
    }

    [Fact]
    public void Merge_SeedsTwentyTilesApart_StayTwo()
    {
        var first = new GraphSeed(FirstName, OriginX, OriginY, OriginZ, FirstKind);
        var far = new GraphSeed(SecondName, OriginX + FarSpan, OriginY, LaterZ, SecondKind);

        var merged = SeedMerge.Merge([far, first]);

        Assert.Equal(2, merged.Count);
        Assert.Equal(first, merged[0].Head);
        Assert.Equal(far, merged[1].Head);
        Assert.True(FarSpan > SeedMerge.MergeChebyshev);
    }

    [Fact]
    public void Snap_BlockedOrigin_MovesToNearbyStandable()
    {
        var surfaceAt = FloorAt((StandX, StandY, StandZ));

        var snapped = SeedMerge.Snap(OriginX, OriginY, StandZ, surfaceAt);

        Assert.Equal((StandX, StandY, StandZ), snapped);
        Assert.True(NavMetric.Chebyshev(Flat(OriginX, OriginY), Flat(StandX, StandY)) <= SeedMerge.SnapRadius);
    }

    [Fact]
    public void Snap_AskedHeightOffTheFloor_ReturnsTheFloorFound()
    {
        // A seed at 20 over a street at 7: the node must stand at 7, or the game
        // pathfinder files the goal on the layer above and never reaches it.
        var snapped = SeedMerge.Snap(OriginX, OriginY, RoofZ, (_, _, _) => FloorZ);

        Assert.Equal((OriginX, OriginY, FloorZ), snapped);
    }

    [Fact]
    public void HeightHints_MostSeedsFirst_LowerOnATie()
    {
        var hints = SeedMerge.HeightHints(
            [
                Seed(FirstName, RoofZ),
                Seed(SecondName, FloorZ),
                Seed(ThirdName, FloorZ),
                Seed(ThirdName, MarkerZ)
            ]
        );

        Assert.Equal([FloorZ, MarkerZ, RoofZ], hints);
    }

    [Fact]
    public void Place_FirstSeedHeightStandsNowhere_UsesAnotherMemberHeight()
    {
        // A marker carries height 0 and sorts first, but the shrine stands at 36 on a hill.
        var cluster = new SeedCluster(
            Seed(FirstName, MarkerZ),
            OriginX,
            OriginY,
            [Seed(FirstName, MarkerZ), Seed(SecondName, HillZ)]
        );

        var placed = SeedMerge.Place(cluster, FloorNear(HillZ), groundZ: null);

        Assert.Equal((OriginX, OriginY, HillZ), placed);
    }

    [Fact]
    public void Place_NoMemberHeightStands_FallsBackToTheGround()
    {
        var cluster = SeedCluster.Of(Seed(FirstName, MarkerZ));

        var placed = SeedMerge.Place(cluster, FloorNear(HillZ), (_, _) => HillZ);

        Assert.Equal((OriginX, OriginY, HillZ), placed);
    }

    [Fact]
    public void Place_NothingStands_IsNull()
    {
        var cluster = SeedCluster.Of(Seed(FirstName, MarkerZ));

        Assert.Null(SeedMerge.Place(cluster, (_, _, _) => null, (_, _) => MarkerZ));
    }

    private static GraphSeed Seed(string name, int z) => new(name, OriginX, OriginY, z, FirstKind);

    private static Point3D Flat(int x, int y) => new(x, y, 0);

    /// <summary>The floor at <paramref name="floor"/>, found only from within one walker step.</summary>
    private static Func<int, int, int, int?> FloorNear(int floor) =>
        (_, _, z) => z - Standable.DropBelow <= floor && floor <= z + Standable.ClimbAbove ? floor : null;

    private static Func<int, int, int, int?> FloorAt(params (int X, int Y, int Z)[] cells)
    {
        var set = new HashSet<(int, int, int)>(cells);
        return (x, y, z) => set.Contains((x, y, z)) ? z : null;
    }
}
