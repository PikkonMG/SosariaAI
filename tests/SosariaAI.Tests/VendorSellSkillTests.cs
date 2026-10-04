using System.Collections.Generic;
using Server;
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
}
