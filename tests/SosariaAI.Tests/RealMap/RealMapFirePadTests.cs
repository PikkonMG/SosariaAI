using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Movement;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// The Fire pad at (5682,1437) on the real Felucca tiles and the live nav graph. It sends a
/// walker to Destard's landing at (5164,1009). One boot read it as dead and dropped its gate,
/// and the saved graph kept the loss on every boot after. The boot now judges the pad on the
/// lasting ground (<see cref="Standable.GroundWalker"/>) and gives back the gate of every pad
/// that fires (<see cref="TeleporterLinking.Relink"/>).
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapFirePadTests
{
    private const string FirePadNode = "tp-5682-1437";
    private const string DestardLandingNode = "tp-5164-1009";

    private static readonly Point3D FirePad = new(5682, 1437, 0);

    [RealMapFact]
    public void FirePad_FiresOnTheLastingGround()
    {
        MovementImpl.Configure();
        var walker = Standable.GroundWalker(RealMapWorld.Felucca);

        Assert.False(GatePad.NeverFires(walker, FirePad, RealMapWorld.TeleporterPadHeight));
    }

    [RealMapFact]
    public void FirePad_LostGate_ComesBackFromItsPad()
    {
        var graph = RealMapWorld.LiveGraph();
        var nodes = new List<NavNode>(graph.Nodes);
        var byName = new Dictionary<string, NavNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            byName.TryAdd(node.Name, node);
        }

        NavGates.RemoveTeleporter(byName[FirePadNode], DestardLandingNode, byName);
        var nodeCount = nodes.Count;
        var json = File.ReadAllText(Path.Combine(RealMapWorld.ServerDataRoot, WorldGenerator.TeleportersFileName));
        var firePadLinks = WorldDataSeeds.ParseTeleporters(json)
            .Where(link => RealMapWorld.FacetName.Equals(link.SrcMap, StringComparison.OrdinalIgnoreCase) && link.Sx == FirePad.X && link.Sy == FirePad.Y);

        Assert.Equal(1, TeleporterLinking.Relink(nodes, firePadLinks));
        Assert.Equal(nodeCount, nodes.Count);
        Assert.Equal(NavGateKind.Teleporter, new NavGraph(graph.Facet, nodes).GateKind(FirePadNode, DestardLandingNode));
    }
}
