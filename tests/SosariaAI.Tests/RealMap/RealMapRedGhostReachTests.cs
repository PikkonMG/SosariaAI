using Server;
using SosariaAI.Behaviour;
using SosariaAI.Skills;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// A red ghost at the camps reds die at, on the real Felucca tiles, the live nav graph and the
/// server's own town guards: it reaches an ankh. In a two-hour run 68 of 69 ghosts that stood up
/// with no way back were reds, killed at the Yew gate, the Shame door and the Britain graveyard.
/// From the Yew woods a red's roads clear of the guards reached 77 nodes, every way out through
/// Yew; the guards take no ghost, so a red ghost crosses them when nothing else leads out.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapRedGhostReachTests(ITestOutputHelper output)
{
    private static readonly Point3D YewWoods = new(766, 826, 0);
    private static readonly Point3D YewGateCamp = new(800, 696, 0);
    private static readonly Point3D ShameDoorCamp = new(474, 1590, 0);
    private static readonly Point3D GraveyardCamp = new(1381, 1447, 10);

    private static readonly ResSource[] Ankhs =
    [
        Shrine(new Point3D(1301, 639, 16)),
        Shrine(new Point3D(1458, 844, 0)),
        Shrine(new Point3D(1858, 874, 0)),
        Shrine(new Point3D(1594, 2489, 20)),
        Shrine(new Point3D(1730, 3528, 3)),
        Shrine(new Point3D(3355, 289, 4)),
        Shrine(new Point3D(4217, 564, 36))
    ];

    [RealMapFact]
    public void RedGhostAtACamp_ReachesAnAnkh()
    {
        var map = RealMapWorld.Felucca;
        var regions = RealMapWorld.RegisterGuards(map);

        try
        {
            RealMapWorld.WithLiveGraph(() =>
            {
                foreach (var camp in new[] { YewWoods, YewGateCamp, ShameDoorCamp, GraveyardCamp })
                {
                    var pick = GhostSeek.ShortestTrip(camp, Ankhs, GhostReach.For(map, RealMapWorld.FacetName, camp, murderer: true).TripTiles);
                    output.WriteLine($"{camp}: picks {pick?.Location}");

                    Assert.NotNull(pick);
                }
            });
        }
        finally
        {
            foreach (var region in regions)
            {
                region.Unregister();
            }
        }
    }

    private static ResSource Shrine(Point3D spot) => new(GhostSeek.KindShrine, spot, GhostRules.ShrineStandTiles);
}
