using System.Collections.Generic;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class NodeHeightTests
{
    private const int BankStreetZ = 7;
    private const int WrongZ = 20;
    private const int BlockedX = 5;

    [Fact]
    public void Settle_NodeAFloorOff_MovesOntoTheSurface()
    {
        // Felucca-1419-1703 sat at 20 on a Britain street where walkers stand at 7.
        var off = Node("off", 0, WrongZ);
        var right = Node("right", 1, BankStreetZ);

        var moved = NodeHeight.Settle([off, right], (_, _, _) => BankStreetZ);

        Assert.Equal(1, moved);
        Assert.Equal(BankStreetZ, off.Z);
        Assert.Equal(BankStreetZ, right.Z);
    }

    [Fact]
    public void Settle_NoSurfaceFound_KeepsTheHeight()
    {
        var node = Node("wall", BlockedX, WrongZ);

        var moved = NodeHeight.Settle([node], (x, _, z) => x == BlockedX ? null : z);

        Assert.Equal(0, moved);
        Assert.Equal(WrongZ, node.Z);
    }

    [Fact]
    public void Settle_NullInputs_MoveNothing()
    {
        Assert.Equal(0, NodeHeight.Settle(null, (_, _, z) => z));
        Assert.Equal(0, NodeHeight.Settle(new List<NavNode> { Node("a", 0, 0) }, null));
    }

    [Fact]
    public void Stands_FollowsTheSurfaceFinder()
    {
        var stands = NodeHeight.Stands((x, _, z) => x == BlockedX ? null : z);

        Assert.True(stands(0, 0, 0));
        Assert.False(stands(BlockedX, 0, 0));
        Assert.Null(NodeHeight.Stands(null));
    }

    private static NavNode Node(string name, int x, int z) =>
        new() { Name = name, X = x, Y = 0, Z = z, Connects = [] };
}
