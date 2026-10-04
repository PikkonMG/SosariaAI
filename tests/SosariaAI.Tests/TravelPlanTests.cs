using System.Linq;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class TravelPlanTests
{
    private const int TeleporterSpanTiles = 2000;
    private const int WalkSpanTiles = 10;

    [Fact]
    public void From_LongLegWithoutGateMeta_MarksNone()
    {
        // No gate is recorded, so nothing may teleport the character. The walk fails
        // honestly instead of moving it from bare ground.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0, "B"),
                Node("B", WalkSpanTiles, 0, 0, "A", "C"),
                Node("C", WalkSpanTiles + TeleporterSpanTiles, 0, 0, "B")
            ]
        );

        var steps = TravelPlan.From(graph, ["A", "B", "C"]);

        Assert.Equal(3, steps.Count);
        Assert.Equal("A", steps[0].Node);
        Assert.Equal(NavGateKind.None, steps[0].ArrivalGate);
        Assert.Equal("B", steps[1].Node);
        Assert.Equal(NavGateKind.None, steps[1].ArrivalGate);
        Assert.Equal("C", steps[2].Node);
        Assert.Equal(NavGateKind.None, steps[2].ArrivalGate);
        Assert.Equal(new Point3D(0, 0, 0), steps[0].Location);
        Assert.Equal(new Point3D(WalkSpanTiles, 0, 0), steps[1].Location);
        Assert.Equal(new Point3D(WalkSpanTiles + TeleporterSpanTiles, 0, 0), steps[2].Location);
    }

    [Fact]
    public void From_DiagonalRoadLeg_IsAWalkNotATeleport()
    {
        // Road nodes sit 32 tiles apart on each axis. The diagonal leg is 45 tiles in a
        // straight line but fits the pathfinder box, so the character must walk it.
        const int roadSpacing = NavLimits.SoftLegDistance;
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0, "B"),
                Node("B", roadSpacing, roadSpacing, 0, "A")
            ]
        );

        var steps = TravelPlan.From(graph, ["A", "B"]);

        Assert.Equal(NavGateKind.None, steps[1].ArrivalGate);
    }

    [Fact]
    public void From_AllShortWalks_MarksNone()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0, "B"),
                Node("B", WalkSpanTiles, 0, 0, "A", "C"),
                Node("C", WalkSpanTiles * 2, 0, 0, "B")
            ]
        );

        var steps = TravelPlan.From(graph, ["A", "B", "C"]);

        Assert.Equal(3, steps.Count);
        Assert.Equal(NavGateKind.None, steps[0].ArrivalGate);
        Assert.Equal(NavGateKind.None, steps[1].ArrivalGate);
        Assert.Equal(NavGateKind.None, steps[2].ArrivalGate);
    }

    [Fact]
    public void From_RecordedGate_MarksItsKind()
    {
        var a = Node("A", 0, 0, 0);
        var b = Node("B", TeleporterSpanTiles, 0, 0);
        NavGates.Add(a, b, NavGateKind.Moongate);
        var graph = new NavGraph(FacetNames.Felucca, [a, b]);

        var steps = TravelPlan.From(graph, ["A", "B"]);

        Assert.Equal(NavGateKind.Moongate, steps[1].ArrivalGate);
    }

    [Fact]
    public void ExitToward_SkipsAnotherGateAndAimsAtWalkingLeg()
    {
        TravelStep[] steps =
        [
            new("start", new Point3D(10, 10, 0), NavGateKind.None),
            new("arrival", new Point3D(100, 100, 0), NavGateKind.Moongate),
            new("other-gate", new Point3D(900, 900, 0), NavGateKind.Moongate),
            new("road", new Point3D(88, 112, 0), NavGateKind.None)
        ];

        Assert.Equal(new Point3D(88, 112, 0), TravelPlan.ExitToward(steps, 1, null));
    }

    [Fact]
    public void WithoutPassThroughPads_DropsAPadThePlanOnlyWalksAcross()
    {
        // The Orc Cave landing: the walk to the first hall went over the exit pad beside it.
        var graph = OrcCaveLanding();
        var steps = TravelPlan.From(graph, ["landing", "exitPad", "corridor", "hall"]);

        var kept = TravelPlan.WithoutPassThroughPads(graph, steps);

        Assert.Equal(["landing", "corridor", "hall"], kept.Select(step => step.Node));
    }

    [Fact]
    public void WithoutPassThroughPads_KeepsAPadThePlanTakes()
    {
        var graph = OrcCaveLanding();
        var steps = TravelPlan.From(graph, ["corridor", "exitPad", "outside"]);

        var kept = TravelPlan.WithoutPassThroughPads(graph, steps);

        Assert.Equal(["corridor", "exitPad", "outside"], kept.Select(step => step.Node));
        Assert.Equal(NavGateKind.Teleporter, kept[^1].ArrivalGate);
    }

    [Fact]
    public void WithoutPassThroughPads_KeepsTheGoalAndALandingThatIsNoPad()
    {
        var graph = OrcCaveLanding();
        var toPad = TravelPlan.From(graph, ["corridor", "exitPad"]);
        var fromOutside = TravelPlan.From(graph, ["outside", "landing", "corridor"]);

        Assert.Equal(["corridor", "exitPad"], TravelPlan.WithoutPassThroughPads(graph, toPad).Select(step => step.Node));
        Assert.Equal(
            ["outside", "landing", "corridor"],
            TravelPlan.WithoutPassThroughPads(graph, fromOutside).Select(step => step.Node)
        );
        Assert.False(TravelPlan.IsPad(graph, "landing"));
        Assert.True(TravelPlan.IsPad(graph, "exitPad"));
    }

    /// <summary>A door pad outside lands beside an exit pad; the corridor leaves past the exit pad.</summary>
    private static NavGraph OrcCaveLanding()
    {
        var outside = Node("outside", 1013, 1433, 0);
        var landing = Node("landing", 5138, 2016, 0, "exitPad");
        var exitPad = Node("exitPad", 5139, 2015, 0, "landing", "corridor");
        var corridor = Node("corridor", 5135, 2011, 0, "exitPad", "hall");
        var hall = Node("hall", 5147, 1965, 0, "corridor");
        NavGates.AddOneWay(outside, landing, NavGateKind.Teleporter);
        NavGates.AddOneWay(exitPad, outside, NavGateKind.Teleporter);
        return new NavGraph(FacetNames.Felucca, [outside, landing, exitPad, corridor, hall]);
    }

    private static NavNode Node(string name, int x, int y, int z, params string[] connects) =>
        new()
        {
            Name = name,
            X = x,
            Y = y,
            Z = z,
            Connects = [.. connects]
        };
}
