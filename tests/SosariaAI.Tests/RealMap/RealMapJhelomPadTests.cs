using System.Diagnostics;
using Server;
using SosariaAI.Admin;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using SosariaAI.Spawning;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// The Jhelom teleporter pads on the real Felucca tiles and the nav graph. The server data lays
/// the pad at (1406,3996) at 5 in the middle of a raised platform whose centre tile stands at 6:
/// a step onto it lands above the pad, and the engine fires it for nobody. It is the only way
/// off the south Jhelom island, which the hub pad at (1419,3832) carries walkers to, and the
/// walkers routed over it stepped on and off it for hours. The boot lays it level with its
/// floor (<see cref="TeleporterRepair"/>), and it fires. The other Jhelom pads fire as laid.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapJhelomPadTests(ITestOutputHelper output)
{
    private const string PlatformPadNode = "tp-1406-3996";
    private const string HubPadNode = "tp-1419-3832";
    private const string SouthLandingNode = "tp-1466-4015";

    private static readonly Point3D PlatformPad = new(1406, 3996, 5);

    /// <summary>The pads of the Jhelom chain that work: the west island's, and the two out of the hub.</summary>
    private static readonly Point3D[] LivePads =
    [
        new(1142, 3621, 5),
        new(1409, 3824, 5),
        new(1419, 3832, 5)
    ];

    /// <summary>A street tile west of the Britain bank, where the town's trips start.</summary>
    private static readonly Point3D BritainStreet = new(1415, 1690, 0);

    /// <summary>A spot on the south Jhelom island, a goal walkers had no road to.</summary>
    private static readonly Point3D SouthIsland = new(1425, 3981, 0);

    /// <summary>The height of the platform's centre tile: the floor a step onto the pad lands on.</summary>
    private const int PlatformFloorZ = 6;

    [RealMapFact]
    public void PlatformPad_FiresForNoStepOntoIt()
    {
        var walker = RealMapWorld.Walker();

        Assert.True(walker.Step(PlatformPad.X, PlatformPad.Y - 1, PlatformPad.Z, PlatformPad.X, PlatformPad.Y, out var landed));
        Assert.False(GatePad.Fires(PlatformPad.Z, RealMapWorld.TeleporterPadHeight, landed));
        Assert.True(GatePad.NeverFires(walker, PlatformPad, RealMapWorld.TeleporterPadHeight));
    }

    [RealMapFact]
    public void TheOtherJhelomPads_Fire()
    {
        var walker = RealMapWorld.Walker();

        Assert.All(LivePads, pad => Assert.False(GatePad.NeverFires(walker, pad, RealMapWorld.TeleporterPadHeight), $"pad {pad}"));
    }

    [RealMapFact]
    public void PlatformPad_LaidLevelWithItsFloor_Fires()
    {
        var walker = RealMapWorld.Walker();
        var leveled = RealMapWorld.LaidLevel(walker, PlatformPad);

        Assert.Equal(PlatformFloorZ, leveled.Z);
        Assert.False(GatePad.NeverFires(walker, leveled, RealMapWorld.TeleporterPadHeight));
    }

    /// <summary>
    /// A Felucca graph built from the server data, with the pads laid as the boot lays them: the
    /// platform pad keeps its gate, the hub pad its gate onto the island, and trips from Britain
    /// to the Jhelom bank and onto the south island both plan.
    /// </summary>
    [RealMapFact(fullBuild: true)]
    public void Generate_KeepsThePlatformPad_AndTheSouthIslandPlans()
    {
        var walker = RealMapWorld.Walker();
        var timer = Stopwatch.StartNew();

        Assert.True(
            WorldGenerator.TryGenerate(
                RealMapWorld.FacetName,
                RealMapWorld.ServerDataRoot,
                WorldSetupRules.T2ASpawnSets,
                walker,
                out var graph,
                out _,
                groundZ: (x, y) => Standable.TryFindGround(RealMapWorld.Felucca, x, y, out var z) ? z : 0,
                isIndoor: RealMapWorld.IsIndoor
            )
        );
        NodeHeight.Settle(graph.Nodes, walker.FloorNear);
        var (dead, intoSealed) = RealMapWorld.DropDeadDataPads(graph.Nodes, walker);
        graph = new NavGraph(graph.Facet, graph.Nodes);
        output.WriteLine($"{dead} dead pad gates, {intoSealed} gates into sealed places, built in {timer.Elapsed.TotalSeconds:F0} s");

        Assert.Equal(NavGateKind.Teleporter, graph.GateKind(PlatformPadNode, "tp-1414-3828"));
        Assert.Equal(NavGateKind.Teleporter, graph.GateKind(HubPadNode, SouthLandingNode));

        foreach (var to in new[] { WorkSites.JhelomTown, SouthIsland })
        {
            var goal = Traveler.PreferredGoal(graph, BritainStreet, to);
            Assert.NotNull(goal);
            var path = Traveler.PlanNames(graph, BritainStreet, goal.Name, walker, RealMapWorld.IsIndoor, avoid: null, out var why);
            Assert.True(path.Count > 0, $"Britain to {to}: {why}");
        }
    }
}
