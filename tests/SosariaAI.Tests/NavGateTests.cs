using System.Collections.Generic;
using System.Text.Json;
using Server.Json;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class NavGateTests
{
    private const int LongConnectTiles = 200;
    private const int ShortConnectTiles = 10;

    [Theory]
    [InlineData("teleporter", NavGateKind.Teleporter)]
    [InlineData("TELEPORTER", NavGateKind.Teleporter)]
    [InlineData("moongate", NavGateKind.Moongate)]
    [InlineData("Moongate", NavGateKind.Moongate)]
    [InlineData("portal", NavGateKind.None)]
    [InlineData("", NavGateKind.None)]
    [InlineData(null, NavGateKind.None)]
    public void ParseKind_MapsKnownNames(string kind, NavGateKind expected)
    {
        Assert.Equal(expected, NavGateLink.ParseKind(kind));
    }

    [Fact]
    public void KindName_UsesLowercaseStrings()
    {
        Assert.Equal("teleporter", NavGateLink.KindName(NavGateKind.Teleporter));
        Assert.Equal("moongate", NavGateLink.KindName(NavGateKind.Moongate));
        Assert.Null(NavGateLink.KindName(NavGateKind.None));
    }

    [Fact]
    public void Add_IsUndirected_AndFillsConnects()
    {
        var left = Node("gate", 0, 0, 0);
        var right = Node("inside", 100, 0, 0);

        NavGates.Add(left, right, NavGateKind.Moongate);

        Assert.Contains("inside", left.Connects);
        Assert.Contains("gate", right.Connects);
        Assert.Equal(NavGateKind.Moongate, NavGates.KindBetween(left, "inside"));
        Assert.Equal(NavGateKind.Moongate, NavGates.KindBetween(right, "gate"));
        Assert.Single(left.Gates);
        Assert.Single(right.Gates);
        Assert.Equal("moongate", left.Gates[0].Kind);
    }

    [Fact]
    public void Add_SkipsSelf()
    {
        var node = Node("same", 0, 0, 0);

        NavGates.Add(node, node, NavGateKind.Teleporter);

        Assert.Empty(node.Connects);
        Assert.Null(node.Gates);
        Assert.Equal(NavGateKind.None, NavGates.KindBetween(node, "same"));
    }

    [Fact]
    public void Add_SkipsDuplicateTo()
    {
        var left = Node("a", 0, 0, 0);
        var right = Node("b", 50, 0, 0);

        NavGates.Add(left, right, NavGateKind.Moongate);
        NavGates.Add(left, right, NavGateKind.Teleporter);

        Assert.Equal(["b"], left.Connects);
        Assert.Equal(["a"], right.Connects);
        Assert.Single(left.Gates);
        Assert.Equal(NavGateKind.Moongate, NavGates.KindBetween(left, "b"));
    }

    [Fact]
    public void InferLongConnects_MarksLongAsTeleporter_LeavesShortAsNone()
    {
        var near = Node("near", 0, 0, 0, "far", "close");
        var far = Node("far", LongConnectTiles, 0, 0, "near");
        var close = Node("close", ShortConnectTiles, 0, 0, "near");

        NavGates.InferLongConnects([near, far, close]);

        Assert.Equal(NavGateKind.Teleporter, NavGates.KindBetween(near, "far"));
        Assert.Equal(NavGateKind.Teleporter, NavGates.KindBetween(far, "near"));
        Assert.Equal(NavGateKind.None, NavGates.KindBetween(near, "close"));
        Assert.Equal(NavGateKind.None, NavGates.KindBetween(close, "near"));
        Assert.True(NavMetric.Chebyshev(near.Location, far.Location) > NavLimits.MaxLegDistance);
        Assert.True(NavMetric.Chebyshev(near.Location, close.Location) <= NavLimits.MaxLegDistance);
    }

    [Fact]
    public void JsonRoundTrip_KeepsGates_AndOldConnectsOnlyStillLoads()
    {
        var withGate = Node("gate", 0, 0, 0);
        var inside = Node("inside", LongConnectTiles, 0, 0);
        NavGates.Add(withGate, inside, NavGateKind.Teleporter);

        var file = new NavFile
        {
            Facet = "felucca",
            Nodes = [withGate, inside]
        };

        var json = JsonConfig.Serialize(file);
        var loaded = JsonSerializer.Deserialize<NavFile>(json, JsonConfig.DefaultOptions);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Nodes.Count);
        Assert.Equal(NavGateKind.Teleporter, NavGates.KindBetween(loaded.Nodes[0], "inside"));
        Assert.Equal(NavGateKind.Teleporter, NavGates.KindBetween(loaded.Nodes[1], "gate"));
        Assert.Contains("inside", loaded.Nodes[0].Connects);

        var legacyJson = """
            {"facet":"felucca","nodes":[{"name":"a","x":0,"y":0,"z":0,"connects":["b"],"arrivalRange":3}]}
            """;
        var legacy = JsonSerializer.Deserialize<NavFile>(legacyJson, JsonConfig.DefaultOptions);

        Assert.NotNull(legacy);
        Assert.Single(legacy.Nodes);
        Assert.Equal(["b"], legacy.Nodes[0].Connects);
        Assert.Null(legacy.Nodes[0].Gates);
        Assert.Equal(NavGateKind.None, NavGates.KindBetween(legacy.Nodes[0], "b"));
    }

    [Fact]
    public void RemoveTeleporter_AOneWayPad_LosesItsGateAndItsLinkToTheLanding()
    {
        var pad = Node("pad", 1406, 3996, 6, "street");
        var street = Node("street", 1406, 3997, 5, "pad");
        var landing = Node("landing", 1414, 3828, 5);
        NavGates.AddOneWay(pad, landing, NavGateKind.Teleporter);
        var byName = new Dictionary<string, NavNode> { ["pad"] = pad, ["street"] = street, ["landing"] = landing };

        Assert.True(NavGates.RemoveTeleporter(pad, "landing", byName));
        Assert.Null(pad.Gates);
        Assert.Equal(["street"], pad.Connects);
        Assert.Empty(landing.Connects);
        Assert.False(NavGates.RemoveTeleporter(pad, "landing", byName));
    }

    [Fact]
    public void RemoveTeleporter_ATwoWayGate_KeepsTheWayBackAndItsLink()
    {
        var near = Node("near", 0, 0, 0);
        var far = Node("far", 500, 0, 0);
        NavGates.Add(near, far, NavGateKind.Teleporter);
        var byName = new Dictionary<string, NavNode> { ["near"] = near, ["far"] = far };

        Assert.True(NavGates.RemoveTeleporter(near, "far", byName));
        Assert.Null(near.Gates);
        Assert.Equal(NavGateKind.Teleporter, NavGates.KindBetween(far, "near"));
        Assert.Contains("far", near.Connects);
        Assert.Contains("near", far.Connects);
    }

    [Fact]
    public void RemoveTeleporter_LeavesAMoongateAlone()
    {
        var gate = Node("gate", 0, 0, 0);
        var far = Node("far", 500, 0, 0);
        NavGates.Add(gate, far, NavGateKind.Moongate);

        Assert.False(NavGates.RemoveTeleporter(gate, "far", new Dictionary<string, NavNode> { ["gate"] = gate, ["far"] = far }));
        Assert.Equal(NavGateKind.Moongate, NavGates.KindBetween(gate, "far"));
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
