using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class BoatRulesTests
{
    [Theory]
    [InlineData(168, true)]
    [InlineData(171, true)]
    [InlineData(310, true)]
    [InlineData(311, true)]
    [InlineData(167, false)]
    [InlineData(3, false)]
    public void IsWaterLand_ClassicWaterIds(int landId, bool water) =>
        Assert.Equal(water, BoatRules.IsWaterLand(landId));

    [Fact]
    public void Search_SkipsThePierThenOpensTheBay()
    {
        Assert.Equal(0, BoatRules.MinOpenWater);
        Assert.Equal(20, BoatRules.SearchRadius);
        Assert.Equal(1508, BoatRules.BritainOpenWater.X);
        Assert.Equal(1790, BoatRules.BritainOpenWater.Y);
        Assert.Equal(BoatRules.DeepWaterZ, BoatRules.BritainOpenWater.Z);
        Assert.True(BoatRules.IsWaterStatic(BoatRules.StaticWaterMin));
        Assert.True(BoatRules.IsWaterStatic(BoatRules.StaticWaterMax));
        Assert.False(BoatRules.IsWaterStatic(1));
        Assert.Equal(
            new[]
            {
                BoatRules.SmallBoatNorthId,
                BoatRules.SmallBoatEastId,
                BoatRules.SmallBoatSouthId,
                BoatRules.SmallBoatWestId
            },
            BoatRules.SmallBoatItemIds
        );
    }

    [Fact]
    public void PlaceZs_CoverHarborAndDeepWater()
    {
        Span<int> zs = stackalloc int[BoatRules.PlaceZCapacity];
        var n = BoatRules.FillPlaceZs(waterZ: BoatRules.DeepWaterZ, landZ: BoatRules.ShoreZ, zs);
        var list = zs[..n].ToArray();

        Assert.Contains(BoatRules.DeepWaterZ, list);
        Assert.Contains(BoatRules.HarborWaterZ, list);
        Assert.Contains(BoatRules.ShoreZ, list);
        Assert.Equal(n, DistinctCount(list));
    }

    [Theory]
    [InlineData(168, -5, -5, true)]
    [InlineData(171, -2, -2, true)]
    [InlineData(310, -5, 0, false)]
    [InlineData(3, 0, -5, false)]
    public void LandWaterAt_WaterLandMustMatchZ(int landId, int landZ, int placeZ, bool fit) =>
        Assert.Equal(fit, BoatRules.LandWaterAt(landId, landZ, placeZ));

    [Fact]
    public void ConsiderStatic_WaterStaticMatchesZ()
    {
        var hasWater = false;

        Assert.True(BoatRules.ConsiderStatic(BoatRules.StaticWaterMin, BoatRules.DeepWaterZ, BoatRules.DeepWaterZ, ref hasWater));
        Assert.True(hasWater);
    }

    [Fact]
    public void ConsiderStatic_PierPostBlocks()
    {
        var hasWater = true;

        Assert.False(BoatRules.ConsiderStatic(1, BoatRules.ShoreZ, BoatRules.DeepWaterZ, ref hasWater));
    }

    [Fact]
    public void TryFindFit_NoMap_IsFalse()
    {
        Assert.False(BoatRules.TryFindFit(null, BoatRules.BritainOpenWater, out var fit));
        Assert.Equal(Point3D.Zero, fit);
        Assert.False(BoatRules.TryFindFit(Map.Internal, BoatRules.BritainOpenWater, out _));
    }

    [Fact]
    public void SearchArea_TriesTheNearCellFirst()
    {
        var tried = new List<(int X, int Y)>();

        Assert.True(
            BoatRules.SearchArea(
                new Point3D(10, 10, BoatRules.ShoreZ),
                (x, y) =>
                {
                    tried.Add((x, y));
                    return true;
                }
            )
        );
        Assert.Equal((10, 10), Assert.Single(tried));
    }

    [Fact]
    public void SearchArea_PierPost_SkipsToOpenWater()
    {
        var taken = (X: 0, Y: 0);

        Assert.True(
            BoatRules.SearchArea(
                Point3D.Zero,
                (x, y) =>
                {
                    taken = (x, y);
                    return !(x == 0 && y == 0);
                }
            )
        );
        Assert.False(taken.X == 0 && taken.Y == 0);
        Assert.Equal(1, Math.Max(Math.Abs(taken.X), Math.Abs(taken.Y)));
    }

    [Fact]
    public void SearchArea_NoCellTaken_IsFalse() =>
        Assert.False(BoatRules.SearchArea(Point3D.Zero, (_, _) => false));

    [Fact]
    public void FootprintFits_EastFacing_ClearsANorthPier()
    {
        var at = new Point3D(5, 5, BoatRules.DeepWaterZ);
        bool OpenWater(int x, int y) => !(x == 5 && y == 6);

        Assert.False(BoatRules.FootprintFits(at, VerticalBoat(), OpenWater));
        Assert.True(BoatRules.FootprintFits(at, HorizontalBoat(), OpenWater));
        Assert.True(BoatRules.FootprintFits(at, OneTileBoat(), OpenWater));
    }

    private static MultiComponentList OneTileBoat() =>
        new(
            new List<MultiTileEntry>
            {
                new(1, 0, 0, 0, TileFlag.None)
            }
        );

    private static MultiComponentList VerticalBoat() =>
        new(
            new List<MultiTileEntry>
            {
                new(1, 0, 0, 0, TileFlag.None),
                new(1, 0, 1, 0, TileFlag.Background)
            }
        );

    private static MultiComponentList HorizontalBoat() =>
        new(
            new List<MultiTileEntry>
            {
                new(1, 0, 0, 0, TileFlag.None),
                new(1, 1, 0, 0, TileFlag.Background)
            }
        );

    private static int DistinctCount(int[] values)
    {
        var n = 0;
        for (var i = 0; i < values.Length; i++)
        {
            var seen = false;
            for (var j = 0; j < i; j++)
            {
                if (values[j] == values[i])
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
            {
                n++;
            }
        }

        return n;
    }
}
