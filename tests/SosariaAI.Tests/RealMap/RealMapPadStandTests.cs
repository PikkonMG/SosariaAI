using Server;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// Two places on the real Felucca tiles where a walker stood at a pad it wanted and was not
/// carried. At the Covetous mouth the pad up out of the dungeon sets walkers down on the pad
/// in at (2420,883); its only open sides are south and the pad at (2421,883), and the fixed
/// turn of steps off took that pad first. In the Britain sewer the walk to the pad at
/// (6031,1499,42) ended at the foot of its stair at (6032,1498,22), twenty below, and the pad
/// search skipped a pad that far above.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapPadStandTests
{
    private static readonly Point3D CovetousPad = new(2420, 883, 0);
    private static readonly Point3D CovetousPadBeside = new(2421, 883, 0);
    private static readonly Point3D CovetousStepOff = new(2420, 884, 0);

    private static readonly Point3D SewerPad = new(6031, 1499, 42);
    private static readonly Point3D SewerPadLands = new(1491, 1640, 24);
    private static readonly Point3D SewerStairFoot = new(6032, 1498, 22);
    private static readonly Point3D SewerLanding = new(6032, 1499, 31);

    [RealMapFact]
    public void OnTheCovetousPad_TheWalkerStepsOffSouth_NotOntoThePadBeside()
    {
        var walker = RealMapWorld.Walker();
        var pads = RealMapWorld.DataPads();

        Assert.False(walker.Step(CovetousPad.X, CovetousPad.Y, CovetousPad.Z, CovetousPad.X, CovetousPad.Y - 1, out _));
        Assert.True(walker.Step(CovetousPad.X, CovetousPad.Y, CovetousPad.Z, CovetousPadBeside.X, CovetousPadBeside.Y, out _));
        Assert.True(pads.ContainsKey((CovetousPadBeside.X, CovetousPadBeside.Y)));

        var off = GatePad.StepOffTile(
            walker,
            CovetousPad,
            (x, y, z) => pads.TryGetValue((x, y), out var padZ) && NavMetric.SameFloor(padZ, z)
        );

        Assert.Equal(CovetousStepOff, off);
        Assert.Equal(CovetousPad, GatePad.EntryStep(walker, CovetousStepOff, CovetousPad));
    }

    [RealMapFact]
    public void AtTheFootOfTheSewerStair_ThePadAboveIsPicked_AndReachedByItsLanding()
    {
        var walker = RealMapWorld.Walker();
        var height = RealMapWorld.TeleporterPadHeight;

        Assert.False(GatePad.OnPadFloor(SewerStairFoot, SewerPad));
        Assert.True(GatePad.SomeStepReaches(walker, SewerPad, height));
        Assert.Equal(
            0,
            GatePad.Pick(SewerStairFoot, SewerPadLands, [new GatePad.Pad(SewerPad, SewerPadLands, SameMap: true, Reachable: true)])
        );

        Assert.False(walker.Step(SewerStairFoot.X, SewerStairFoot.Y, SewerStairFoot.Z, SewerPad.X, SewerPad.Y, out _));
        Assert.Equal(SewerLanding, GatePad.EntryStep(walker, SewerStairFoot, SewerPad));

        Assert.True(walker.Step(SewerLanding.X, SewerLanding.Y, SewerLanding.Z, SewerPad.X, SewerPad.Y, out var onPad));
        Assert.True(GatePad.Fires(SewerPad.Z, height, onPad));
    }
}
