using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
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
        Assert.Equal($"I placed a small house at ({HouseRules.DefaultPlotX}, {HouseRules.DefaultPlotY}, {HouseRules.DefaultPlotZ}).", HouseRules.BoughtLine(plot.ToString()));
        Assert.Equal("I bought a small house.", HouseRules.BoughtLine(null));
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

        foreach (var spot in HouseRules.CandidateSpots(plot, plot))
        {
            Assert.True(HouseRules.OutsideTown(spot.X));

            if (spot == plot)
            {
                sawPlot = true;
            }
        }

        foreach (var spot in HouseRules.CandidateSpots(CharactersFile.DefaultBankSpot, plot))
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
}
