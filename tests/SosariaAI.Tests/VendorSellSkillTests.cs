using System.Collections.Generic;
using Server;
using Server.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class VendorSellSkillTests
{
    private static Destination ShopAt(int x, int y) => new() { X = x, Y = y };

    [Fact]
    public void StallSpot_UsesTheMarkerInsideTheTown()
    {
        var candidates = new List<Destination>
        {
            ShopAt(2000, 2000),
            ShopAt(1005, 1005)
        };

        var spot = VendorSellSkill.StallSpot(
            candidates,
            dest => dest.Arrival.X < 1500,
            new Point3D(100, 100, 0));

        Assert.Equal(new Point3D(1005, 1005, 0), spot);
    }

    [Fact]
    public void StallSpot_FallsBackToTheSellersSpotWhenTheTownHasNoMarker()
    {
        var candidates = new List<Destination> { ShopAt(2000, 2000) };
        var fallback = new Point3D(100, 100, 0);

        Assert.Equal(
            fallback,
            VendorSellSkill.StallSpot(candidates, _ => false, fallback));
    }

    [Fact]
    public void HarvestShop_AGuildmasterNeverCounts()
    {
        // The town check skipped a guildmaster and the stall check counted it: a town served
        // only by the smiths' guildmaster had its shop spawners wiped and refilled on every scan.
        Assert.False(VendorSellSkill.CountsAsHarvestShop(nameof(BlacksmithGuildmaster), true, ShopFinder.SmithToken));
        Assert.True(VendorSellSkill.CountsAsHarvestShop(nameof(Blacksmith), false, ShopFinder.SmithToken));
    }
}
