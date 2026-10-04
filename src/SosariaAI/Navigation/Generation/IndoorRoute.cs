using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Navigation.Generation;

/// <summary>
/// Indoor nodes are destinations. A route between two outdoor places must not
/// use them as shortcuts through a shop.
/// </summary>
public static class IndoorRoute
{
    public const int InteriorRadius = 2;

    public static void Mark(IReadOnlyList<NavNode> nodes, Func<int, int, int, bool> isIndoor)
    {
        if (nodes == null || isIndoor == null)
        {
            return;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            if (node == null)
            {
                continue;
            }

            node.Indoor = node.Indoor || isIndoor(node.X, node.Y, node.Z);
        }
    }

    /// <summary>
    /// Cuts the shortcuts through buildings, then walks the ground again to rejoin the
    /// pieces those cuts split off. A shop node was often the hub that held a street
    /// together, so cutting alone leaves the town in fragments. A node the cuts left with
    /// no walking edge gets one to the nearest node a walk reaches, and a node still alone
    /// after that is removed: a goal no plan reaches. Only the roof test marks a node
    /// indoor: a vendor in the catalog may stand at an open forge or a healer in the wilds,
    /// and marking the street beside them cut it.
    /// </summary>
    /// <param name="walker">How a walker stands and steps; <see cref="Standable.Walker"/> on a live map.</param>
    public static void Repair(List<NavNode> nodes, Func<int, int, int, bool> isIndoor, TileWalker walker)
    {
        Mark(nodes, isIndoor);
        GraphConnect.DropIndoorShortcuts(nodes, walker, TileIndoor(nodes, isIndoor));
        TerrainRoute.ConnectComponents(nodes, walker, WalkLine.Outdoors(isIndoor));
        GraphConnect.AttachDoors(nodes, walker, isIndoor);
        GraphConnect.VerifyEdges(nodes, walker, isIndoor);
        GraphConnect.LinkStranded(nodes, walker, isIndoor);
        GraphConnect.DropUnlinked(nodes);
    }

    private static Func<int, int, int, bool> TileIndoor(
        List<NavNode> nodes,
        Func<int, int, int, bool> isIndoor
    ) =>
        (x, y, z) =>
            isIndoor != null && isIndoor(x, y, z) || NearIndoorNode(nodes, x, y);

    private static bool NearIndoorNode(List<NavNode> nodes, int x, int y)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            if (node is { Indoor: true } &&
                NavMetric.Chebyshev(node.Location, new Point3D(x, y, node.Z)) <= InteriorRadius)
            {
                return true;
            }
        }

        return false;
    }
}
