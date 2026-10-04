using System.Linq;
using Server;
using SosariaAI.Navigation;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// The top of the Trinsic passage on the real Felucca tiles and the live nav graph. The pad
/// at (5899,1411) sets walkers down at (1630,3320), beside the pad at (1629,3320) that sends
/// them back down. The live graph linked the landing straight west to (1628,3320), across
/// that pad, and walkers sent along the link stalled 275 times at (5898,1411) on a leg four
/// thousand tiles away. Below, the pads at (5918,1411) and (5918,1412) carry walkers down into
/// the passage's lower pocket, and the pocket's pads at (5961,1408) and (5961,1409) carry them
/// back up onto the same two tiles. The server data lays a third pad down at (5918,1410), at
/// -29, but the ground there stands at -23 (the land corners -17 and -28): a pad one tall
/// fires only for a mover level with it or below its top, so it carries nobody. The boot lays
/// it level with the ground (<see cref="TeleporterRepair"/>), and it carries walkers down as
/// the two beside it do. The pocket has a real way out, its pads up.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapPadCrossingTests(ITestOutputHelper output)
{
    private const string Landing = "tp-1630-3320";
    private const string PadBackDown = "tp-1629-3320";
    private const string StreetWest = "Felucca-1628-3320";

    /// <summary>The pad west of the upper landings that carries walkers up to the landing above.</summary>
    private const string PadUpToTheSurface = "tp-5899-1411";

    private static readonly string[] UpperLandings = ["tp-5918-1411", "tp-5918-1412"];

    /// <summary>The pads down into the pocket, where the server data lays them.</summary>
    private static readonly Point3D[] LivePadsDown = [new(5918, 1411, -29), new(5918, 1412, -29)];

    private static readonly Point3D DeadPadDown = new(5918, 1410, -29);

    /// <summary>The ground under the dead pad down: where the boot lays it.</summary>
    private const int GroundUnderDeadPadZ = -23;

    /// <summary>Where the pocket's pad up sets a walker down, and where walkers stood beside the pads down.</summary>
    private static readonly Point3D PocketLanding = new(5961, 1408, 59);

    private static readonly Point3D BesideThePadsDown = new(5919, 1410, -20);

    /// <summary>A Trinsic street tile, where a walker from the landing heads home.</summary>
    private static readonly Point3D TrinsicStreet = new(1911, 2810, 0);

    [RealMapFact]
    public void LiveGraph_LosesTheLinkAcrossThePad_AndTheLandingWalksHomeRoundIt()
    {
        var walker = RealMapWorld.Walker();
        var graph = RealMapWorld.LiveGraph();

        var nodes = graph.Nodes.ToList();
        var (dropped, relinked) = PadCrossings.Drop(nodes, walker, RealMapWorld.IsIndoor);
        graph = new NavGraph(graph.Facet, nodes);
        output.WriteLine($"{dropped} links across a pad dropped, {relinked} nodes relinked");

        Assert.True(graph.TryGetNode(Landing, out var landing));
        Assert.DoesNotContain(StreetWest, landing.Connects);
        Assert.False(graph.Travels(PadBackDown, StreetWest));

        var from = landing.Location;
        var goal = Traveler.PreferredGoal(graph, from, TrinsicStreet);
        Assert.NotNull(goal);
        var path = Traveler.PlanNames(graph, from, goal.Name, walker, RealMapWorld.IsIndoor, avoid: null, out var why);
        output.WriteLine($"{path.Count} nodes: {string.Join(" ", path.Take(6))} {why}");

        Assert.True(path.Count > 0, why);
        Assert.DoesNotContain(PadBackDown, path);
    }

    [RealMapFact]
    public void PadAtTheHeadOfTheRow_FiresForNoStep_AndThePadsBesideItFire()
    {
        var walker = RealMapWorld.Walker();

        Assert.True(GatePad.NeverFires(walker, DeadPadDown, RealMapWorld.TeleporterPadHeight));
        Assert.All(LivePadsDown, pad => Assert.False(GatePad.NeverFires(walker, pad, RealMapWorld.TeleporterPadHeight), $"pad {pad}"));
    }

    [RealMapFact]
    public void PadAtTheHeadOfTheRow_LaidLevelWithTheGround_Fires()
    {
        var walker = RealMapWorld.Walker();
        var leveled = RealMapWorld.LaidLevel(walker, DeadPadDown);

        Assert.Equal(GroundUnderDeadPadZ, leveled.Z);
        Assert.False(GatePad.NeverFires(walker, leveled, RealMapWorld.TeleporterPadHeight));
    }

    [RealMapFact]
    public void LiveGraph_TheUpperLandingsWalkOffThePads_AndThePocketPlansHome()
    {
        var walker = RealMapWorld.Walker();
        var graph = RealMapWorld.LiveGraph();
        RealMapWorld.DropDeadDataPads(graph.Nodes, walker);
        var nodes = graph.Nodes.ToList();
        PadCrossings.Drop(nodes, walker, RealMapWorld.IsIndoor);
        graph = new NavGraph(graph.Facet, nodes);

        foreach (var name in UpperLandings)
        {
            Assert.True(graph.TryGetNode(name, out var landing));
            Assert.Contains(
                landing.Connects,
                other => graph.Travels(name, other) && !graph.IsGate(name, other) &&
                         graph.TryGetNode(other, out var street) && street.Gates is not { Count: > 0 }
            );
        }

        foreach (var from in new[] { PocketLanding, BesideThePadsDown })
        {
            var goal = Traveler.PreferredGoal(graph, from, TrinsicStreet);
            Assert.NotNull(goal);
            var path = Traveler.PlanNames(graph, from, goal.Name, walker, RealMapWorld.IsIndoor, avoid: null, out var why);
            output.WriteLine($"from {from}: {path.Count} nodes: {string.Join(" ", path.Take(8))} {why}");

            // A plan that walks onto a pad takes it: the pad carries the walker off. A plan that
            // lands on a pad by a gate walks on from it, since a landing sets off no pad.
            Assert.True(path.Count > 0, $"from {from}: {why}");

            for (var i = 1; i + 1 < path.Count; i++)
            {
                if (!graph.IsGate(path[i - 1], path[i]) && graph.TryGetNode(path[i], out var stop) &&
                    stop.Gates is { } gates && gates.Exists(gate => gate.ParsedKind == NavGateKind.Teleporter))
                {
                    Assert.True(graph.IsGate(path[i], path[i + 1]), $"a walk steps onto {path[i]} and walks on to {path[i + 1]}");
                }
            }

            Assert.Contains(PadUpToTheSurface, path);
        }
    }
}
