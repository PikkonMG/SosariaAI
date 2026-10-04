using System.Collections.Generic;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class DeadPadsTests
{
    private const int LegTiles = 20;
    private const int FarTiles = 500;
    private const int MainlandRoadNodes = 4;

    [Fact]
    public void Drop_ADeadPadThatWasTheOnlyWayBack_TakesThePadInWithIt()
    {
        // Jhelom: the hub pad carries walkers to the south island, and the island's only way
        // back is the platform pad, which fires for nobody.
        var (town, hub, mainland) = Mainland();
        var landing = Node("landing", FarTiles, 0);
        var shop = Node("shop", FarTiles + LegTiles, 0);
        var platform = Node("platform", FarTiles + LegTiles * 2, 0);
        Walk(landing, shop);
        Walk(shop, platform);
        NavGates.AddOneWay(hub, landing, NavGateKind.Teleporter);
        NavGates.AddOneWay(platform, town, NavGateKind.Teleporter);

        var dropped = DeadPads.Drop([.. mainland, landing, shop, platform], node => node == platform);

        Assert.Equal((1, 1), dropped);
        Assert.Null(platform.Gates);
        Assert.Null(hub.Gates);
        Assert.DoesNotContain("landing", hub.Connects);
        Assert.DoesNotContain("platform", town.Connects);
        Assert.Contains("shop", landing.Connects);
    }

    [Fact]
    public void Drop_ADeadPadBesideAnotherWayBack_LeavesThePadInAlone()
    {
        // A row of pads out of a dungeon room: one lies under the floor, its twin fires.
        var (town, hub, mainland) = Mainland();
        var landing = Node("landing", FarTiles, 0);
        var deadTwin = Node("deadTwin", FarTiles + LegTiles, 0);
        var liveTwin = Node("liveTwin", FarTiles + LegTiles, LegTiles);
        Walk(landing, deadTwin);
        Walk(landing, liveTwin);
        NavGates.AddOneWay(hub, landing, NavGateKind.Teleporter);
        NavGates.AddOneWay(deadTwin, town, NavGateKind.Teleporter);
        NavGates.AddOneWay(liveTwin, town, NavGateKind.Teleporter);

        var dropped = DeadPads.Drop([.. mainland, landing, deadTwin, liveTwin], node => node == deadTwin);

        Assert.Equal((1, 0), dropped);
        Assert.Equal(NavGateKind.Teleporter, NavGates.KindBetween(hub, "landing"));
        Assert.Equal(NavGateKind.Teleporter, NavGates.KindBetween(liveTwin, "town"));
    }

    [Fact]
    public void Drop_APlaceThatNeverHadAWayBack_IsNotJudged()
    {
        // An island no walk leaves keeps its pad in when a dead pad elsewhere goes.
        var (town, hub, mainland) = Mainland();
        var island = Node("island", FarTiles, FarTiles);
        var room = Node("room", 0, FarTiles);
        var deadPad = Node("deadPad", LegTiles, FarTiles);
        Walk(room, deadPad);
        NavGates.AddOneWay(hub, island, NavGateKind.Teleporter);
        NavGates.AddOneWay(town, room, NavGateKind.Teleporter);
        NavGates.AddOneWay(deadPad, hub, NavGateKind.Teleporter);

        var dropped = DeadPads.Drop([.. mainland, island, room, deadPad], node => node == deadPad);

        Assert.Equal((1, 1), dropped);
        Assert.Equal(NavGateKind.Teleporter, NavGates.KindBetween(hub, "island"));
        Assert.Null(town.Gates);
    }

    [Fact]
    public void Drop_NoDeadPad_ChangesNothing()
    {
        var (town, hub, mainland) = Mainland();
        var landing = Node("landing", FarTiles, 0);
        NavGates.AddOneWay(hub, landing, NavGateKind.Teleporter);

        Assert.Equal((0, 0), DeadPads.Drop([.. mainland, landing], _ => false));
        Assert.Equal((0, 0), DeadPads.Drop(null, _ => true));
        Assert.Equal(NavGateKind.Teleporter, NavGates.KindBetween(hub, "landing"));
    }

    /// <summary>
    /// A town street, the hub and the road between them, joined on foot: the largest piece of
    /// walking links, larger than any piece a test puts beside it.
    /// </summary>
    private static (NavNode Town, NavNode Hub, List<NavNode> All) Mainland()
    {
        var town = Node("town", 0, 0);
        var hub = Node("hub", LegTiles, 0);
        var all = new List<NavNode> { town };

        for (var i = 1; i <= MainlandRoadNodes; i++)
        {
            var road = Node($"road{i}", 0, LegTiles * i);
            Walk(all[^1], road);
            all.Add(road);
        }

        Walk(all[^1], hub);
        all.Add(hub);
        return (town, hub, all);
    }

    private static void Walk(NavNode a, NavNode b)
    {
        a.Connects.Add(b.Name);
        b.Connects.Add(a.Name);
    }

    private static NavNode Node(string name, int x, int y) => new() { Name = name, X = x, Y = y, Connects = [] };
}
