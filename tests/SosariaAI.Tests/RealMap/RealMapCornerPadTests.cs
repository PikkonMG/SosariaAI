using System;
using System.IO;
using System.Linq;
using Server;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// The pads down into the passage at (2399,198) and (2400,198) on the real Felucca tiles and
/// the live nav graph. The pad up out of the passage sets walkers down at (2400,199), diagonal
/// to the pad at (2399,198). The tile at (2400,198) has no floor, so the engine refuses that
/// diagonal step, and no step lands on the twin pad there. Walkers asked for the diagonal
/// until their tries ran out: 54 trips ended with "found no teleporter to take at (2399, 198, 0)".
/// A walker steps west to (2399,199) first, then north onto the pad.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapCornerPadTests(ITestOutputHelper output)
{
    private const string PadNode = "tp-2399-198";
    private const string NoFloorPadNode = "tp-2400-198";

    private static readonly Point3D Pad = new(2399, 198, 0);
    private static readonly Point3D PadLands = new(5753, 436, 79);
    private static readonly Point3D NoFloorPad = new(2400, 198, 0);
    private static readonly Point3D NoFloorPadLands = new(5754, 436, 80);
    private static readonly Point3D Landing = new(2400, 199, 0);
    private static readonly Point3D WestOfTheLanding = new(2399, 199, 0);

    /// <summary>Where the live graph plans the hop to land: the node of the pad's landing.</summary>
    private static readonly Point3D PlannedLanding = new(5753, 436, 78);

    [RealMapFact]
    public void DiagonalOntoThePad_IsRefused_AndTheWalkerStepsWestFirst()
    {
        var walker = RealMapWorld.Walker();

        Assert.Null(walker.FloorNear(NoFloorPad.X, NoFloorPad.Y, NoFloorPad.Z));
        Assert.False(walker.Step(Landing.X, Landing.Y, Landing.Z, Pad.X, Pad.Y, out _));
        Assert.True(walker.Step(WestOfTheLanding.X, WestOfTheLanding.Y, WestOfTheLanding.Z, Pad.X, Pad.Y, out _));

        Assert.Equal(WestOfTheLanding, GatePad.EntryStep(walker, Landing, Pad));
        Assert.Equal(Pad, GatePad.EntryStep(walker, WestOfTheLanding, Pad));
    }

    [RealMapFact]
    public void NoStepReachesTheTwinPad_AndPickTakesThePadBesideIt()
    {
        var walker = RealMapWorld.Walker();
        var height = RealMapWorld.TeleporterPadHeight;
        var twin = new GatePad.Pad(NoFloorPad, NoFloorPadLands, SameMap: true, GatePad.SomeStepReaches(walker, NoFloorPad, height));
        var pad = new GatePad.Pad(Pad, PadLands, SameMap: true, GatePad.SomeStepReaches(walker, Pad, height));

        Assert.False(twin.Reachable);
        Assert.True(pad.Reachable);
        Assert.Equal(1, GatePad.Pick(Landing, PlannedLanding, [twin, pad]));
        Assert.Equal(0, GatePad.Pick(Landing, PlannedLanding, [pad, twin]));
    }

    /// <summary>
    /// The live graph is sound here: the pad's node is walked to from a node that steps
    /// straight onto the pad, and no walk leads to the twin pad no step lands on.
    /// </summary>
    [RealMapFact]
    public void LiveGraph_WalksToThePadFromATileThatStepsOntoIt()
    {
        var walker = RealMapWorld.Walker();
        var graph = RealMapWorld.LiveGraph();

        Assert.True(graph.TryGetNode(PadNode, out var pad));
        Assert.Contains(
            pad.Connects,
            name => !graph.IsGate(name, PadNode) && graph.TryGetNode(name, out var approach) &&
                    walker.Step(approach.X, approach.Y, approach.Z, pad.X, pad.Y, out _)
        );
        Assert.DoesNotContain(
            graph.Nodes,
            node => node.Connects?.Contains(NoFloorPadNode) == true && !graph.IsGate(node.Name, NoFloorPadNode) &&
                    !graph.IsGate(NoFloorPadNode, node.Name)
        );
    }

    /// <summary>
    /// Every landing a Felucca pad sets down beside another pad, where the engine refuses the
    /// step onto that pad, has an entry step onto it round the corner.
    /// </summary>
    [RealMapFact]
    public void EveryLandingBesideAPad_HasAnEntryStepOntoIt()
    {
        var walker = RealMapWorld.Walker();
        var height = RealMapWorld.TeleporterPadHeight;
        var json = File.ReadAllText(Path.Combine(RealMapWorld.ServerDataRoot, WorldGenerator.TeleportersFileName));
        var links = WorldDataSeeds.ParseTeleporters(json)
            .Where(link => RealMapWorld.FacetName.Equals(link.SrcMap, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var landings = links
            .Where(link => RealMapWorld.FacetName.Equals(link.DstMap, StringComparison.OrdinalIgnoreCase))
            .Select(link => new Point3D(link.Dx, link.Dy, link.Dz))
            .ToList();
        var corners = 0;

        foreach (var link in links)
        {
            var pad = new Point3D(link.Sx, link.Sy, link.Sz);

            if (!GatePad.SomeStepReaches(walker, pad, height))
            {
                continue;
            }

            foreach (var landing in landings)
            {
                if (NavMetric.Chebyshev(landing, pad) != 1 || !GatePad.OnPadFloor(landing, pad) ||
                    walker.FloorNear(landing.X, landing.Y, landing.Z) is not { } floor ||
                    walker.Step(landing.X, landing.Y, floor, pad.X, pad.Y, out _))
                {
                    continue;
                }

                corners++;
                var entry = GatePad.EntryStep(walker, landing with { Z = floor }, pad);
                output.WriteLine($"landing {landing} beside pad {pad}: step to {entry}");
                Assert.True(entry != null, $"landing {landing} has no step onto pad {pad}");
            }
        }

        Assert.True(corners > 0);
    }
}
