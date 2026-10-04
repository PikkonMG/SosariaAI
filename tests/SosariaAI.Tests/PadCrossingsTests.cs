using System.Collections.Generic;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// Walking links across a teleporter pad, and walks off a pad, as the Trinsic passage's top
/// lays them: the landing at (1630,3320) beside the pad at (1629,3320) that sends back down.
/// </summary>
public class PadCrossingsTests
{
    private const int FarTiles = 500;
    private const int PadX = 10;
    private const int SouthTiles = 3;

    [Fact]
    public void Drop_ALinkAcrossAPad_GoesAndTheLandingGetsAWalkRoundIt()
    {
        var (pad, landing, west, south, below) = PassageTop();

        var result = PadCrossings.Drop([pad, landing, west, south, below], TestWalkers.Flat, isIndoor: null);

        // Both ends of the dropped link were left with no walk but onto the pad.
        Assert.Equal((1, 2), result);
        Assert.DoesNotContain(west.Name, landing.Connects);
        Assert.DoesNotContain(landing.Name, west.Connects);
        Assert.Contains(south.Name, landing.Connects);
        Assert.Contains(south.Name, west.Connects);
        Assert.Contains(pad.Name, landing.Connects);
    }

    [Fact]
    public void Drop_ALinkOntoAPadOverItsRowTwin_Stays()
    {
        // Two pads of one row land side by side: the twin carries the walker where the link
        // meant to go.
        var below = Node("below", FarTiles, FarTiles);
        var belowNext = Node("below-next", FarTiles + 1, FarTiles);
        var start = Node("start", PadX, 0);
        var twin = Node("twin", PadX, 1);
        var pad = Node("pad", PadX, 2);
        NavGates.AddOneWay(twin, below, NavGateKind.Teleporter);
        NavGates.AddOneWay(pad, belowNext, NavGateKind.Teleporter);
        GraphConnect.LinkForRoad(start, pad);
        GraphConnect.LinkForRoad(below, belowNext);

        var result = PadCrossings.Drop([below, belowNext, start, twin, pad], TestWalkers.Flat, isIndoor: null);

        Assert.Equal((0, 0), result);
        Assert.Contains(pad.Name, start.Connects);
    }

    [Fact]
    public void Drop_ALandingThatWalksOnlyOntoAPad_GetsAWalkOffThePads()
    {
        // The passage's upper landing: the pad below sets walkers down on a pad of its own,
        // whose only walk leads onto the pad beside it, which sends straight back down.
        var below = Node("below", FarTiles, FarTiles);
        var belowStreet = Node("below-street", FarTiles + 1, FarTiles);
        var landing = Node("landing", PadX, 1);
        var padDown = Node("pad-down", PadX, 0);
        var street = Node("street", PadX - SouthTiles, 1);
        NavGates.Add(below, landing, NavGateKind.Teleporter);
        NavGates.AddOneWay(padDown, below, NavGateKind.Teleporter);
        GraphConnect.LinkForRoad(below, belowStreet);
        GraphConnect.LinkForRoad(landing, padDown);
        GraphConnect.LinkForRoad(padDown, street);

        var result = PadCrossings.Drop([below, belowStreet, landing, padDown, street], TestWalkers.Flat, isIndoor: null);

        Assert.Equal((0, 1), result);
        Assert.Contains(street.Name, landing.Connects);
        var graph = new NavGraph("test", [below, belowStreet, landing, padDown, street]);
        Assert.True(graph.Travels(landing.Name, street.Name));
        Assert.False(graph.Travels(padDown.Name, street.Name));
    }

    [Fact]
    public void Drop_ALandingWhoseOnlyStraightWalkOffCrossesThePad_StepsOffBesideIt()
    {
        // The passage's top at (1630,3320): the one road node near the landing lies west past
        // the pad, a tile south, and the straight walk there steps onto the pad.
        var below = Node("below", FarTiles, FarTiles);
        var belowStreet = Node("below-street", FarTiles + 1, FarTiles);
        var landing = Node("landing", PadX + 1, 0);
        var padDown = Node("pad-down", PadX, 0);
        var road = Node("road", PadX - SouthTiles, 1);
        NavGates.AddOneWay(below, landing, NavGateKind.Teleporter);
        NavGates.AddOneWay(padDown, below, NavGateKind.Teleporter);
        GraphConnect.LinkForRoad(below, belowStreet);
        GraphConnect.LinkForRoad(landing, padDown);
        GraphConnect.LinkForRoad(padDown, road);
        List<NavNode> nodes = [below, belowStreet, landing, padDown, road];
        var pads = new Dictionary<(int X, int Y), NavNode> { [(padDown.X, padDown.Y)] = padDown };
        var byName = new Dictionary<string, NavNode>();
        Assert.True(PadCrossings.Crosses(landing, road, pads, byName));

        var result = PadCrossings.Drop(nodes, TestWalkers.Flat, isIndoor: null);

        Assert.Equal((0, 1), result);
        var step = Assert.Single(nodes, node => node.Name == landing.Name + PadCrossings.StepOffSuffix);
        Assert.Equal(1, NavMetric.Chebyshev(step.Location, landing.Location));
        Assert.NotEqual((padDown.X, padDown.Y), (step.X, step.Y));
        Assert.False(PadCrossings.Crosses(step, road, pads, byName));
        var graph = new NavGraph("test", nodes);
        Assert.True(graph.Travels(landing.Name, step.Name));
        Assert.True(graph.Travels(step.Name, road.Name));
    }

    [Fact]
    public void Drop_ARowOfLandingsThatWalkOnlyToEachOtherAndOntoPads_GetsAWalkOut()
    {
        // The Destard stair up at (5129..5132,908): two landings side by side, linked to each
        // other and onto the pad down beside them, and the floor up the stair to the north.
        var below = Node("below", FarTiles, FarTiles);
        var belowStreet = Node("below-street", FarTiles + 1, FarTiles);
        var west = Node("west", PadX, SouthTiles);
        var east = Node("east", PadX + 1, SouthTiles);
        var padDown = Node("pad-down", PadX, SouthTiles + 1);
        var floor = Node("floor", PadX, 0);
        NavGates.AddOneWay(below, west, NavGateKind.Teleporter);
        NavGates.AddOneWay(belowStreet, east, NavGateKind.Teleporter);
        NavGates.AddOneWay(padDown, below, NavGateKind.Teleporter);
        GraphConnect.LinkForRoad(below, belowStreet);
        GraphConnect.LinkForRoad(west, east);
        GraphConnect.LinkForRoad(west, padDown);
        GraphConnect.LinkForRoad(east, padDown);
        GraphConnect.LinkForRoad(floor, padDown);
        List<NavNode> nodes = [below, belowStreet, west, east, padDown, floor];

        var result = PadCrossings.Drop(nodes, TestWalkers.Flat, isIndoor: null);

        Assert.Equal((0, 1), result);
        var graph = new NavGraph("test", nodes);
        Assert.True(graph.Travels(west.Name, floor.Name) || graph.Travels(east.Name, floor.Name));
        Assert.Equal(6, nodes.Count);
    }

    [Fact]
    public void Graph_NoWalkLeavesAPadNoGateLandsOn()
    {
        var (pad, landing, west, south, below) = PassageTop();

        var graph = new NavGraph("test", [pad, landing, west, south, below]);

        Assert.True(graph.Travels(landing.Name, pad.Name));
        Assert.True(graph.Travels(pad.Name, below.Name));
        Assert.False(graph.Travels(pad.Name, west.Name));
        Assert.False(graph.Travels(pad.Name, landing.Name));
    }

    [Fact]
    public void Graph_APadThatIsALandingToo_IsLeftOnFoot()
    {
        // The pad at the foot of a stair is where the pad at its head sets walkers down, and
        // it sends them back up.
        var head = Node("head", PadX, 0);
        var headStreet = Node("head-street", PadX + 1, 0);
        var foot = Node("foot", FarTiles, FarTiles);
        var footStreet = Node("foot-street", FarTiles + 1, FarTiles);
        NavGates.AddOneWay(head, foot, NavGateKind.Teleporter);
        NavGates.AddOneWay(foot, headStreet, NavGateKind.Teleporter);
        GraphConnect.LinkForRoad(head, headStreet);
        GraphConnect.LinkForRoad(foot, footStreet);

        var graph = new NavGraph("test", [head, headStreet, foot, footStreet]);

        Assert.True(graph.Travels(foot.Name, footStreet.Name));
        Assert.False(graph.Travels(head.Name, headStreet.Name));
    }

    /// <summary>
    /// A pad with a landing east of it and a street west of it, linked straight across the
    /// pad, a street node south of the landing, and the place below the pad sends to.
    /// </summary>
    private static (NavNode Pad, NavNode Landing, NavNode West, NavNode South, NavNode Below) PassageTop()
    {
        var pad = Node("pad", PadX, 0);
        var landing = Node("landing", PadX + 1, 0);
        var west = Node("west", PadX - 1, 0);
        var south = Node("south", PadX + 1, SouthTiles);
        var below = Node("below", FarTiles, FarTiles);
        NavGates.AddOneWay(pad, below, NavGateKind.Teleporter);
        GraphConnect.LinkForRoad(landing, pad);
        GraphConnect.LinkForRoad(west, pad);
        GraphConnect.LinkForRoad(landing, west);
        return (pad, landing, west, south, below);
    }

    private static NavNode Node(string name, int x, int y) =>
        new() { Name = name, X = x, Y = y, Z = 0, ArrivalRange = NavLimits.DefaultArrivalRange, Connects = [] };
}
