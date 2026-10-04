using System;
using System.Collections.Generic;

namespace SosariaAI.Navigation.Generation;

/// <summary>
/// The floor a node stands on. A node's height is the surface found at its tile, never
/// the height it was asked about. The game pathfinder files a goal on a layer by its
/// height, so a node one floor off the real one is a goal no walk can reach.
/// </summary>
public static class NodeHeight
{
    /// <summary>A stand test from a surface finder: a tile stands when a surface is found.</summary>
    public static Func<int, int, int, bool> Stands(Func<int, int, int, int?> surfaceAt) =>
        surfaceAt == null ? null : (x, y, z) => surfaceAt(x, y, z) != null;

    /// <summary>
    /// Moves every node onto the surface at its own tile, nearest the height it has. A
    /// node with no surface found keeps its height. Returns how many nodes moved.
    /// </summary>
    public static int Settle(IEnumerable<NavNode> nodes, Func<int, int, int, int?> surfaceAt)
    {
        if (nodes == null || surfaceAt == null)
        {
            return 0;
        }

        var moved = 0;

        foreach (var node in nodes)
        {
            if (node == null || surfaceAt(node.X, node.Y, node.Z) is not { } floor || floor == node.Z)
            {
                continue;
            }

            node.Z = floor;
            moved++;
        }

        return moved;
    }
}
