using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// <see cref="Traveler.WalkReach"/>, <see cref="Traveler.WalkReaches"/> and <see cref="Traveler.WalkTrip"/>:
/// the nodes a walk from one spot reaches and the tiles of the trip to each, which a ghost uses
/// to pick the ankh or healer with the shortest trip of those it can get to.
/// </summary>
public class WalkReachTests
{
    private const int WallX = 5;
    private const int ShopFromStreet = 10;
    private const int MidX = 20;
    private const int FarX = 50;
    private const int LoopY = 200;
    private const int SideStartX = 30;
    private const int TilePrecision = 6;
    private static readonly Point3D OnShore = new(1, 0, 0);

    [Fact]
    public void APlatformOnlyItsExitPadsTouch_IsNotReached()
    {
        // The Spirituality ankh platform: its pads send a visitor to the shore, and nothing
        // brings one onto it. The graph pieces put it with the shore all the same.
        var shore = Node("Shore", 0, 0, 0, "Landing");
        var landing = Node("Landing", 30, 0, 0);
        var platform = Node("Platform", 60, 0, 20);
        NavGates.AddOneWay(platform, landing, NavGateKind.Teleporter);
        var graph = new NavGraph(FacetNames.Felucca, [shore, landing, platform]);

        var reach = Reach(graph, OnShore, TestWalkers.Flat);

        Assert.True(graph.SameComponent("Shore", "Platform"));
        Assert.Contains("Landing", reach.Keys);
        Assert.DoesNotContain("Platform", reach.Keys);
    }

    [Fact]
    public void AOneWayPad_CarriesTheWalkerItsOwnWay()
    {
        var shore = Node("Shore", 0, 0, 0, "Pad");
        var pad = Node("Pad", 10, 0, 0);
        var island = Node("Island", 400, 0, 0);
        NavGates.AddOneWay(pad, island, NavGateKind.Teleporter);
        var graph = new NavGraph(FacetNames.Felucca, [shore, pad, island]);

        Assert.Contains("Island", Reach(graph, OnShore, TestWalkers.Flat).Keys);
        Assert.DoesNotContain("Shore", Reach(graph, new Point3D(401, 0, 0), TestWalkers.Flat).Keys);
    }

    [Fact]
    public void ASpotWalledOffFromEveryStart_ReachesNothing()
    {
        // Hrolf's ghost stood where the walk to every near node was blocked.
        var graph = new NavGraph(FacetNames.Felucca, [Node("East", 10, 0, 0, "Far"), Node("Far", 20, 0, 0)]);
        var walled = TestWalkers.Ground(static (x, _, _) => x != WallX, groundZ: null);

        Assert.Empty(Reach(graph, OnShore, walled));
        Assert.Contains("Far", Reach(graph, OnShore, TestWalkers.Flat).Keys);
    }

    [Fact]
    public void AnIndoorGoal_IsReachedFromTheStreetBesideIt_ButNotWalkedThrough()
    {
        // A healer's shop: a walk goes in only to the goal it aims at, never on through it.
        var street = Node("Street", 0, 0, 0, "Shop");
        var shop = Node("Shop", 10, 0, 0, "Yard");
        var yard = Node("Yard", 60, 0, 0);
        shop.Indoor = true;
        var graph = new NavGraph(FacetNames.Felucca, [street, shop, yard]);

        var reach = Reach(graph, OnShore, TestWalkers.Flat);

        Assert.DoesNotContain("Shop", reach.Keys);
        Assert.True(Traveler.WalkReaches(graph, reach, shop, bars: null));
        Assert.False(Traveler.WalkReaches(graph, reach, yard, bars: null));

        // The trip steps in from the street: the walk to the street, then the step to the door.
        Assert.Equal(reach["Street"] + ShopFromStreet, Traveler.WalkTrip(graph, reach, shop, bars: null));
    }

    [Fact]
    public void TheTrip_IsTheWalkToTheStart_ThenTheRoad()
    {
        var shore = Node("Shore", 0, 0, 0, "Mid");
        var mid = Node("Mid", MidX, 0, 0, "Far");
        var far = Node("Far", FarX, 0, 0);
        var graph = new NavGraph(FacetNames.Felucca, [shore, mid, far]);

        var reach = Reach(graph, OnShore, TestWalkers.Flat);

        Assert.Equal(OnShore.X, reach["Shore"], TilePrecision);
        Assert.Equal(OnShore.X + FarX, Traveler.WalkTrip(graph, reach, far, bars: null).Value, TilePrecision);
    }

    [Fact]
    public void TheTrip_SetsOutFromTheNearestStartWithARoad_AsThePlanDoes()
    {
        // The plan walks from the first start, nearest first, with a road to its goal, even
        // when a later start lies nearer that goal.
        var near = Node("Near", 0, 0, 0, "LoopNorth");
        var loopNorth = Node("LoopNorth", 0, LoopY, 0, "LoopEast");
        var loopEast = Node("LoopEast", SideStartX, LoopY, 0, "Side");
        var side = Node("Side", SideStartX, 0, 0);
        var graph = new NavGraph(FacetNames.Felucca, [near, loopNorth, loopEast, side]);

        var reach = Reach(graph, OnShore, TestWalkers.Flat);

        Assert.Equal(OnShore.X + LoopY + SideStartX + LoopY, reach["Side"], TilePrecision);
    }

    [Fact]
    public void NoTrip_ToAGoalTheWalkDoesNotReach()
    {
        var graph = new NavGraph(FacetNames.Felucca, [Node("Shore", 0, 0, 0), Node("Island", FarX, 0, 0)]);
        graph.TryGetNode("Island", out var island);

        Assert.Null(Traveler.WalkTrip(graph, Reach(graph, OnShore, TestWalkers.Flat), island, bars: null));
        Assert.Null(Traveler.WalkTrip(graph, new Dictionary<string, double>(), goal: null, bars: null));
    }

    [Fact]
    public void AnIndoorGoal_BehindAOneWayPad_IsNotReached()
    {
        var street = Node("Street", 0, 0, 0);
        var cellar = Node("Cellar", 10, 0, 0);
        cellar.Indoor = true;
        NavGates.AddOneWay(cellar, street, NavGateKind.Teleporter);
        var graph = new NavGraph(FacetNames.Felucca, [street, cellar]);

        Assert.False(Traveler.WalkReaches(graph, Reach(graph, OnShore, TestWalkers.Flat), cellar, bars: null));
    }

    [Fact]
    public void NoGraph_OrNoNodeInReach_ReachesNothing()
    {
        var graph = new NavGraph(FacetNames.Felucca, [Node("Far", 1000, 0, 0)]);

        Assert.Empty(Reach(null, OnShore, TestWalkers.Flat));
        Assert.Empty(Reach(graph, OnShore, TestWalkers.Flat));
        Assert.Empty(Traveler.WalkReach(graph, OnShore, walker: null, isIndoor: null, bars: null));
        Assert.False(Traveler.WalkReaches(graph, new Dictionary<string, double>(), goal: null, bars: null));
    }

    private static Dictionary<string, double> Reach(NavGraph graph, Point3D from, TileWalker walker) =>
        Traveler.WalkReach(graph, from, walker, isIndoor: null, bars: null);

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
