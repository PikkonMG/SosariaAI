using System.Linq;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Navigation;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// The Covetous mouth on the real Felucca tiles. The three pads at (2498..2500,916) carry a
/// walker down to (5455..5457,1864), and the pads there carry it back up onto the same three
/// tiles (teleporters.json marks them back). North of the pads is rock, with one lone floor
/// tile at (2500,914). The red camp on the mouth's north compass point snapped onto that
/// tile: reds sent there walked at the rock, stepped onto a pad, came back up and walked at
/// it again, every eight seconds for two hours.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapCovetousPadTests(ITestOutputHelper output)
{
    private static readonly Point3D[] Pads = [new(2498, 916, 0), new(2499, 916, 0), new(2500, 916, 0)];

    /// <summary>The Covetous Entrance in the destination catalog, south of the pads.</summary>
    private static readonly Point3D Entrance = new(2499, 919, 0);

    /// <summary>The walled-in tile north of the pads that the camp snapped onto.</summary>
    private static readonly Point3D WalledIn = new(2500, 914, 0);

    [RealMapFact]
    public void Camps_RoundTheCovetousMouth_AreAllWalkedToWithNoPadOnTheWay()
    {
        var walker = RealMapWorld.Walker();
        var pads = new TrapField(Pads.Select(pad => new TrapZone(pad.X, pad.Y, pad.Z, TrapRules.OnTileReach)));

        var camps = HotSpots.CampsAround(RealMapWorld.Felucca, walker, pads, Entrance, HotSpotRules.NearCampTiles, includeCenter: true);
        output.WriteLine($"camps: {string.Join(" ", camps)}");

        Assert.NotEmpty(camps);
        Assert.DoesNotContain(WalledIn, camps);

        foreach (var camp in camps)
        {
            var trail = TileRoute.Trail(Entrance, camp, Standable.ShunningPads(walker, pads), isIndoor: null);
            Assert.True(HotSpotRules.ReachesCamp(trail, pads.Harms), $"no walk without a pad from the entrance to the camp at {camp}");
        }
    }
}
