using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Navigation.Generation;

/// <summary>
/// Adds teleporter edges to a single-facet node list. A link is applied only when
/// both names exist in that list, so a Felucca graph cannot connect to a Trammel
/// node. A pad the data marks <c>back</c> carries a person both ways; any other pad only
/// from its own tile to its landing. The way back from a landing is the pad beside it,
/// which the data lists as a link of its own. Joining every link both ways sent plans
/// "back" through bare landings: Moonglow's hub (4442,1122) and the landing at (6574,889)
/// found no pad there, over a hundred times in one run.
/// </summary>
public static class TeleporterLinking
{
    public const string AttachedNamePrefix = "tp-";

    public static void Link(
        IList<NavNode> nodes,
        IEnumerable<(string SrcName, string DstName, bool Back)> links
    )
    {
        if (nodes == null || links == null)
        {
            return;
        }

        var byName = new Dictionary<string, NavNode>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            if (node == null || string.IsNullOrWhiteSpace(node.Name))
            {
                continue;
            }

            byName[node.Name] = node;
        }

        foreach (var (srcName, dstName, back) in links)
        {
            if (string.IsNullOrWhiteSpace(srcName) || string.IsNullOrWhiteSpace(dstName))
            {
                continue;
            }

            if (srcName.Equals(dstName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!byName.TryGetValue(srcName, out var src) ||
                !byName.TryGetValue(dstName, out var dst))
            {
                continue;
            }

            if (back)
            {
                NavGates.Add(src, dst, NavGateKind.Teleporter);
            }
            else
            {
                NavGates.AddOneWay(src, dst, NavGateKind.Teleporter);
            }
        }
    }

    /// <summary>
    /// Places a pad node on each end of every teleporter and gates them. The teleporters are
    /// the facet's own: both ends on the facet of <paramref name="nodes"/>.
    /// </summary>
    public static void Link(IList<NavNode> nodes, IEnumerable<TeleporterLink> teleporters)
    {
        if (nodes == null || teleporters == null)
        {
            return;
        }

        var named = new List<(string SrcName, string DstName, bool Back)>();

        foreach (var link in teleporters)
        {
            var srcName = FindOrAttach(nodes, new Point3D(link.Sx, link.Sy, link.Sz));
            var dstName = FindOrAttach(nodes, new Point3D(link.Dx, link.Dy, link.Dz));
            named.Add((srcName, dstName, link.Back));
        }

        Link(nodes, named);
    }

    /// <summary>A pad node on the exact teleporter tile. See <see cref="SeedAttach.FindOrAttach"/>.</summary>
    public static string FindOrAttach(IList<NavNode> nodes, Point3D point) =>
        SeedAttach.FindOrAttach(nodes, point, AttachedNamePrefix);
}
