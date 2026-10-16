using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class HouseRulesTests
{
    private static readonly Point3D PorchClickOffset = new(0, 4, 0);

    [Fact]
    public void CanBuy_NeedsGoldAndNoHouse()
    {
        Assert.True(HouseRules.CanBuy(HouseRules.ArchitectPrice, HouseRules.ArchitectPrice, alreadyHasHouse: false));
        Assert.False(HouseRules.CanBuy(HouseRules.ArchitectPrice - 1, HouseRules.ArchitectPrice, alreadyHasHouse: false));
        Assert.False(HouseRules.CanBuy(HouseRules.ArchitectPrice, HouseRules.ArchitectPrice, alreadyHasHouse: true));
        Assert.False(HouseRules.CanBuy(HouseRules.ArchitectPrice, 0, alreadyHasHouse: false));
    }

    [Fact]
    public void DefaultPlot_IsEastOfBritain()
    {
        var plot = HouseRules.DefaultPlot();
        Assert.Equal(HouseRules.DefaultPlotX, plot.X);
        Assert.Equal(HouseRules.DefaultPlotY, plot.Y);
        Assert.Equal(HouseRules.DefaultPlotZ, plot.Z);
        Assert.Equal($"I placed a small house at ({HouseRules.DefaultPlotX}, {HouseRules.DefaultPlotY}, {HouseRules.DefaultPlotZ}).", HouseRules.BoughtLine(HouseWantRules.SmallTarget, plot.ToString()));
        Assert.Equal("I bought a small house.", HouseRules.BoughtLine(HouseWantRules.SmallTarget, null));
        Assert.False(HouseRules.OutsideTown(HouseRules.BritainTownMaxX));
        Assert.True(HouseRules.OutsideTown(HouseRules.DefaultPlotX));
        Assert.True(HouseRules.NearPlot(HouseRules.DefaultPlot(), HouseRules.DefaultPlot(), HouseRules.PlaceArrivalTiles));
        Assert.False(HouseRules.NearPlot(CharactersFile.DefaultBankSpot, HouseRules.DefaultPlot(), HouseRules.PlaceArrivalTiles));
    }

    [Fact]
    public void FailedWalk_AtBank_IsNotPlotArrival()
    {
        Assert.False(
            HouseRules.NearPlot(
                CharactersFile.DefaultBankSpot,
                HouseRules.DefaultPlot(),
                HouseRules.PlaceArrivalTiles
            )
        );
    }

    [Fact]
    public void Stands_NeedsSerialAndLiveHouse()
    {
        Assert.False(HouseRules.Stands(HouseRules.NoHouseSerial, atFeet: true, serialLive: true));
        Assert.False(HouseRules.Stands(1, atFeet: false, serialLive: false));
        Assert.True(HouseRules.Stands(1, atFeet: true, serialLive: false));
        Assert.True(HouseRules.Stands(1, atFeet: false, serialLive: true));
        Assert.Null(HouseRules.BySerial(HouseRules.NoHouseSerial));
    }

    [Fact]
    public void PlacementCenter_SubtractsClickOffset()
    {
        var click = HouseRules.DefaultPlot();
        var center = HouseRules.PlacementCenter(click, PorchClickOffset);

        Assert.Equal(click.X, center.X);
        Assert.Equal(click.Y - PorchClickOffset.Y, center.Y);
        Assert.Equal(click.Z, center.Z);
    }

    [Fact]
    public void CandidateSpots_IncludePlotAndSkipTown()
    {
        var plot = HouseRules.DefaultPlot();
        var sawPlot = false;
        var sawBank = false;

        foreach (var spot in HouseRules.CandidateSpots(plot, plot, 0, HouseRules.PlaceSearchRadius))
        {
            Assert.True(HouseRules.OutsideTown(spot.X));

            if (spot == plot)
            {
                sawPlot = true;
            }
        }

        foreach (var spot in HouseRules.CandidateSpots(CharactersFile.DefaultBankSpot, plot, 0, HouseRules.PlaceSearchRadius))
        {
            Assert.True(HouseRules.OutsideTown(spot.X));

            if (spot == CharactersFile.DefaultBankSpot)
            {
                sawBank = true;
            }

            if (spot == plot)
            {
                sawPlot = true;
            }
        }

        Assert.True(sawPlot);
        Assert.False(sawBank);
    }

    [Fact]
    public void CandidateSpots_APlotOfTheOperatorsOwn_IsSearchedRoundAsItIs()
    {
        // A plot west of Britain's x 1740, as on a map of the shard's own, found no spot at all.
        var plot = new Point3D(HouseRules.BritainTownMaxX - HouseRules.PlaceSearchRadius * 2, HouseRules.DefaultPlotY, HouseRules.DefaultPlotZ);
        var spots = new List<Point3D>(HouseRules.CandidateSpots(plot, plot, 0, HouseRules.PlaceSearchRadius));

        Assert.Contains(plot, spots);
        Assert.Contains(new Point3D(plot.X - HouseRules.PlaceSearchStep, plot.Y, plot.Z), spots);
        Assert.DoesNotContain(spots, spot => HouseRules.OutsideTown(spot.X));
    }

    [Fact]
    public void CandidateSpots_KeepClearOfTheMiddleAndInsideTheReach()
    {
        var plot = new Point3D(HouseRules.GraveyardPlotX, HouseRules.GraveyardPlotY, HouseRules.GraveyardPlotZ);
        var spots = new List<Point3D>(
            HouseRules.CandidateSpots(plot, plot, HouseRules.GraveyardClearTiles, HouseRules.GraveyardReachTiles)
        );

        Assert.NotEmpty(spots);
        Assert.DoesNotContain(plot, spots);
        Assert.All(
            spots,
            spot => Assert.InRange(
                NavMetric.Chebyshev(spot, plot),
                HouseRules.GraveyardClearTiles,
                HouseRules.GraveyardReachTiles
            )
        );
    }

    [Fact]
    public void TakesPlot_AHouseOnOrNearThePlot()
    {
        var plot = HouseRules.DefaultPlot();
        var edge = HouseRules.PlaceSearchRadius + HouseRules.PlotTakenMargin;

        Assert.True(HouseRules.TakesPlot(plot, plot, HouseRules.PlaceSearchRadius));
        Assert.True(HouseRules.TakesPlot(new Point3D(plot.X + edge, plot.Y, plot.Z), plot, HouseRules.PlaceSearchRadius));
        Assert.False(HouseRules.TakesPlot(new Point3D(plot.X + edge + 1, plot.Y, plot.Z), plot, HouseRules.PlaceSearchRadius));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public void MayUsePlot_AVendorPlotOnlyForACrafter(bool vendorPlot, bool crafter, bool may) =>
        Assert.Equal(may, HouseRules.MayUsePlot(vendorPlot, crafter));
}
