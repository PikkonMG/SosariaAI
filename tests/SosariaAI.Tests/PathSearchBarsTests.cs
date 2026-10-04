using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class PathSearchBarsTests
{
    private const int GuardedIndex = 3;
    private static readonly Point3D Start = new(2706, 2163, 0);

    [Fact]
    public void BarsNode_AGuardedNodeFarFromAStartUnderTheGuards()
    {
        var bars = new PathSearchBars(new HashSet<int> { GuardedIndex }, Start, noMoongates: true, openAroundStart: true);
        var far = new Point3D(Start.X + PathSearchBars.OpenAroundStartTiles + 1, Start.Y, 0);
        var near = new Point3D(Start.X + PathSearchBars.OpenAroundStartTiles, Start.Y, 0);

        Assert.True(bars.BarsNode(GuardedIndex, far));
        Assert.False(bars.BarsNode(GuardedIndex, near));
        Assert.False(bars.BarsNode(GuardedIndex + 1, far));
    }

    [Fact]
    public void BarsNode_AStartOutOfTheGuardsOpensNoTownNearIt()
    {
        // A red on open ground has no town to walk out of: every guarded node stays barred.
        var bars = new PathSearchBars(new HashSet<int> { GuardedIndex }, Start, noMoongates: true, openAroundStart: false);

        Assert.True(bars.BarsNode(GuardedIndex, Start));
        Assert.True(bars.BarsNode(GuardedIndex, new Point3D(Start.X + PathSearchBars.OpenAroundStartTiles, Start.Y, 0)));
        Assert.False(bars.BarsNode(GuardedIndex + 1, Start));
    }

    [Theory]
    [InlineData(NavGateKind.Moongate, true)]
    [InlineData(NavGateKind.Teleporter, false)]
    [InlineData(NavGateKind.None, false)]
    public void BarsGate_OffFeluccaOnlyPublicMoongates(NavGateKind kind, bool barred) =>
        Assert.Equal(barred, new PathSearchBars(null, Start, noMoongates: true, openAroundStart: false).BarsGate(kind, GuardedIndex + 1, GuardedIndex + 2));

    [Theory]
    [InlineData(GuardedIndex + 1, GuardedIndex + 2, false)]
    [InlineData(GuardedIndex, GuardedIndex + 1, true)]
    [InlineData(GuardedIndex + 1, GuardedIndex, true)]
    public void BarsGate_OnFeluccaAMoongateWithAGuardedPad(int from, int to, bool barred) =>
        Assert.Equal(
            barred,
            new PathSearchBars(new HashSet<int> { GuardedIndex }, Start, noMoongates: false, openAroundStart: false).BarsGate(NavGateKind.Moongate, from, to)
        );

    [Fact]
    public void BarsGate_AGuardedPadNearTheStartStaysBarred()
    {
        var bars = new PathSearchBars(new HashSet<int> { GuardedIndex }, Start, noMoongates: false, openAroundStart: true);

        Assert.False(bars.BarsNode(GuardedIndex, Start));
        Assert.True(bars.BarsGate(NavGateKind.Moongate, GuardedIndex, GuardedIndex + 1));
    }

    [Fact]
    public void For_OnlyAMurdererHasBars()
    {
        var graph = new NavGraph(FacetNames.Felucca, [Node("A", 0, 0)]);

        Assert.Null(PathSearchBars.For(graph, null, Start, murderer: true));
        Assert.Null(PathSearchBars.For(graph, null, Start, murderer: false));
    }

    [Fact]
    public void Explore_AMurdererWalksRoundTheMoongate()
    {
        var home = Node("Home", 0, 0, "Road");
        var road = Node("Road", 400, 0, "Den");
        var den = Node("Den", 800, 0);
        NavGates.Add(home, den, NavGateKind.Moongate);
        var graph = new NavGraph(FacetNames.Felucca, [home, road, den]);
        var bars = new PathSearchBars(new HashSet<int>(), Start, noMoongates: true, openAroundStart: false);

        Assert.Equal(["Den", "Home"], Search(graph, "Den", "Home", null));
        Assert.Equal(["Den", "Road", "Home"], Search(graph, "Den", "Home", bars));
    }

    [Fact]
    public void Explore_AMurdererTakesAMoongateBetweenUnguardedPads()
    {
        var home = Node("Home", 0, 0, "Road");
        var road = Node("Road", 400, 0, "Den");
        var den = Node("Den", 800, 0);
        NavGates.Add(home, den, NavGateKind.Moongate);
        var graph = new NavGraph(FacetNames.Felucca, [home, road, den]);
        var open = new PathSearchBars(new HashSet<int>(), Start, noMoongates: false, openAroundStart: false);
        // The guarded pad is the trip's own end: open to walk onto, barred as a gate.
        var guardedPad = new PathSearchBars(new HashSet<int> { home.Index }, home.Location, noMoongates: false, openAroundStart: true);

        Assert.Equal(["Den", "Home"], Search(graph, "Den", "Home", open));
        Assert.Equal(["Den", "Road", "Home"], Search(graph, "Den", "Home", guardedPad));
    }

    [Fact]
    public void Explore_AMurdererWalksRoundAGuardedTown()
    {
        var home = Node("Home", 0, 0, "Town", "Wood");
        var town = Node("Town", 100, 0, "Den");
        var wood = Node("Wood", 100, 150, "Den");
        var den = Node("Den", 200, 0);
        var graph = new NavGraph(FacetNames.Felucca, [home, town, wood, den]);
        var bars = new PathSearchBars(new HashSet<int> { town.Index }, home.Location, noMoongates: true, openAroundStart: false);

        Assert.Equal(["Den", "Town", "Home"], Search(graph, "Den", "Home", null));
        Assert.Equal(["Den", "Wood", "Home"], Search(graph, "Den", "Home", bars));
    }

    [Fact]
    public void CrossingGuards_OpensTheGuardedNodesAtAPrice()
    {
        const double stepTiles = 10;
        var bars = new PathSearchBars(new HashSet<int> { GuardedIndex }, Start, noMoongates: false, openAroundStart: false);
        var crossing = bars.CrossingGuards();

        Assert.True(crossing.CrossesGuards);
        Assert.False(crossing.BarsNode(GuardedIndex, Start));
        Assert.Equal(
            stepTiles * (PathSearchBars.GuardedTileWeight - 1),
            crossing.PenaltyTiles(GuardedIndex + 1, GuardedIndex, Start, Start, stepTiles, gate: false)
        );
        Assert.Equal(0, crossing.PenaltyTiles(GuardedIndex, GuardedIndex + 1, Start, Start, stepTiles, gate: false));
        Assert.Equal(0, bars.PenaltyTiles(GuardedIndex + 1, GuardedIndex, Start, Start, stepTiles, gate: false));
        Assert.True(crossing.BarsGate(NavGateKind.Moongate, GuardedIndex, GuardedIndex + 1));
    }

    [Fact]
    public void OpeningPads_KeepsTheGuards_AndOpensEveryPad()
    {
        var pads = new HashSet<long> { EdgeHealthRules.Key(GuardedIndex + 1, GuardedIndex + 2) };
        var red = new PathSearchBars(new HashSet<int> { GuardedIndex }, Start, noMoongates: false, openAroundStart: true, pads: pads, ghost: true);
        var open = red.OpeningPads();

        Assert.True(red.MayOpenPads);
        Assert.True(red.BarsGate(NavGateKind.Teleporter, GuardedIndex + 1, GuardedIndex + 2));
        Assert.False(open.MayOpenPads);
        Assert.False(open.BarsGate(NavGateKind.Teleporter, GuardedIndex + 1, GuardedIndex + 2));
        Assert.True(open.KeepsOffGuards);
        Assert.Same(red.Nodes, open.Nodes);
        Assert.Equal(red.Start, open.Start);
        Assert.Equal(red.OpenAroundStart, open.OpenAroundStart);
        Assert.Equal(red.Ghost, open.Ghost);
        Assert.False(open.CrossesGuards);
    }

    [Fact]
    public void OpeningPads_OfBarsOnPadsAlone_IsNoBars()
    {
        var pads = new HashSet<long> { EdgeHealthRules.Key(GuardedIndex + 1, GuardedIndex + 2) };
        var walker = new PathSearchBars(null, default, noMoongates: false, openAroundStart: false, pads: pads);

        Assert.True(walker.MayOpenPads);
        Assert.Null(walker.OpeningPads());
        Assert.False(new PathSearchBars(new HashSet<int> { GuardedIndex }, Start, noMoongates: false, openAroundStart: false).MayOpenPads);
    }

    [Fact]
    public void MayCrossGuards_OnlyATripThatStartsUnderTheGuards()
    {
        var inTown = new PathSearchBars(new HashSet<int> { GuardedIndex }, Start, noMoongates: false, openAroundStart: true);
        var onOpenGround = new PathSearchBars(new HashSet<int> { GuardedIndex }, Start, noMoongates: false, openAroundStart: false);

        Assert.True(inTown.MayCrossGuards);
        Assert.False(inTown.CrossingGuards().MayCrossGuards);
        Assert.False(onOpenGround.MayCrossGuards);
    }

    [Fact]
    public void MayCrossGuards_AGhostFromAnywhere()
    {
        var ghost = new PathSearchBars(new HashSet<int> { GuardedIndex }, Start, noMoongates: false, openAroundStart: false, ghost: true);

        Assert.True(ghost.MayCrossGuards);
        Assert.True(ghost.CrossingGuards().Ghost);
        Assert.False(ghost.CrossingGuards().MayCrossGuards);
        Assert.True(ghost.BarsNode(GuardedIndex, new Point3D(Start.X + PathSearchBars.OpenAroundStartTiles + 1, Start.Y, Start.Z)));
    }

    [Fact]
    public void Explore_AMurdererWithNoRoadClearOfTheGuards_CrossesTheLeastGuardedWay()
    {
        // A red caught in Minoc: the short road out crosses the heart of the town, the long one
        // only its edge.
        var shrine = Node("Shrine", 0, 0, "Heart", "Wood");
        var heart = Node("Heart", 50, 0, "Shrine", "Market");
        var market = Node("Market", 100, 0, "Heart", "Gate");
        var wood = Node("Wood", 0, 100, "Shrine", "Edge");
        var edge = Node("Edge", 50, 100, "Wood", "Field");
        var field = Node("Field", 100, 100, "Edge", "Gate");
        var gate = Node("Gate", 150, 0, "Market", "Field");
        var graph = new NavGraph(FacetNames.Felucca, [shrine, heart, market, wood, edge, field, gate]);
        var bars = new PathSearchBars(
            new HashSet<int> { heart.Index, market.Index, edge.Index },
            shrine.Location,
            noMoongates: false,
            openAroundStart: false
        );

        Assert.Equal(["Gate", "Market", "Heart", "Shrine"], Search(graph, "Gate", "Shrine", null));
        Assert.Empty(Search(graph, "Gate", "Shrine", bars));
        Assert.Equal(["Gate", "Field", "Edge", "Wood", "Shrine"], Search(graph, "Gate", "Shrine", bars.CrossingGuards()));
    }

    [Fact]
    public void Explore_AMurdererCrossingTheGuards_StillKeepsOffAGuardedMoongate()
    {
        var home = Node("Home", 0, 0, "Road");
        var road = Node("Road", 400, 0, "Den");
        var den = Node("Den", 800, 0);
        NavGates.Add(home, den, NavGateKind.Moongate);
        var graph = new NavGraph(FacetNames.Felucca, [home, road, den]);
        var bars = new PathSearchBars(new HashSet<int> { home.Index, road.Index }, den.Location, noMoongates: false, openAroundStart: false);

        Assert.Equal(["Den", "Road", "Home"], Search(graph, "Den", "Home", bars.CrossingGuards()));
    }

    [Fact]
    public void BarsLeg_ALegOverGuardedGroundEitherWay()
    {
        var legs = new HashSet<long> { EdgeHealthRules.Key(GuardedIndex, GuardedIndex + 1) };
        var bars = new PathSearchBars(new HashSet<int>(), Start, noMoongates: false, openAroundStart: false, legs: legs);

        Assert.True(bars.BarsLeg(GuardedIndex, GuardedIndex + 1, Start, Start));
        Assert.True(bars.BarsLeg(GuardedIndex + 1, GuardedIndex, Start, Start));
        Assert.False(bars.BarsLeg(GuardedIndex, GuardedIndex + 2, Start, Start));
        Assert.False(bars.BarsNode(GuardedIndex, Start));
    }

    [Fact]
    public void BarsLeg_ALegNearAStartUnderTheGuardsStaysOpen()
    {
        var legs = new HashSet<long> { EdgeHealthRules.Key(GuardedIndex, GuardedIndex + 1) };
        var bars = new PathSearchBars(new HashSet<int>(), Start, noMoongates: false, openAroundStart: true, legs: legs);
        var near = new Point3D(Start.X + PathSearchBars.OpenAroundStartTiles, Start.Y, 0);
        var far = new Point3D(Start.X + PathSearchBars.OpenAroundStartTiles + 1, Start.Y, 0);

        Assert.False(bars.BarsLeg(GuardedIndex, GuardedIndex + 1, near, far));
        Assert.False(bars.BarsLeg(GuardedIndex, GuardedIndex + 1, far, near));
        Assert.True(bars.BarsLeg(GuardedIndex, GuardedIndex + 1, far, far));
    }

    [Fact]
    public void CrossingGuards_OpensAGuardedLegAtAPrice()
    {
        const double stepTiles = 10;
        var legs = new HashSet<long> { EdgeHealthRules.Key(GuardedIndex, GuardedIndex + 1) };
        var crossing = new PathSearchBars(new HashSet<int>(), Start, noMoongates: false, openAroundStart: false, legs: legs).CrossingGuards();

        Assert.False(crossing.BarsLeg(GuardedIndex, GuardedIndex + 1, Start, Start));
        Assert.Equal(
            stepTiles * (PathSearchBars.GuardedTileWeight - 1),
            crossing.PenaltyTiles(GuardedIndex, GuardedIndex + 1, Start, Start, stepTiles, gate: false)
        );
        // A gate hop between the same two nodes walks no ground.
        Assert.Equal(0, crossing.PenaltyTiles(GuardedIndex, GuardedIndex + 1, Start, Start, stepTiles, gate: true));
    }

    [Fact]
    public void Explore_AMurdererWalksRoundALegThatCutsThroughATown()
    {
        // Both ends lie outside the town; the straight leg between them runs through it.
        var home = Node("Home", 0, 0, "Den", "Wood");
        var wood = Node("Wood", 100, 150, "Den");
        var den = Node("Den", 200, 0);
        var graph = new NavGraph(FacetNames.Felucca, [home, wood, den]);
        var legs = new HashSet<long> { EdgeHealthRules.Key(home.Index, den.Index) };
        var bars = new PathSearchBars(new HashSet<int>(), home.Location, noMoongates: true, openAroundStart: false, legs: legs);

        Assert.Equal(["Den", "Home"], Search(graph, "Den", "Home", null));
        Assert.Equal(["Den", "Wood", "Home"], Search(graph, "Den", "Home", bars));
    }

    private static IReadOnlyList<string> Search(NavGraph graph, string source, string target, PathSearchBars bars)
    {
        var cost = new Dictionary<string, double>();
        var prev = new Dictionary<string, string>();
        NavSearch.Explore(graph, source, null, NavSearch.DefaultGateCost, null, null, cost, prev, bars);
        return NavSearch.Reconstruct(prev, source, target);
    }

    private static NavNode Node(string name, int x, int y, params string[] connects) =>
        new()
        {
            Name = name,
            X = x,
            Y = y,
            Connects = [.. connects]
        };
}
