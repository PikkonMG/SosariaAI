using Server;
using Server.Movement;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class StandableTests
{
    [Fact]
    public void TryFind_NoMap_IsFalse()
    {
        Assert.False(Standable.TryFind(null, 0, 0, 0, out _));
        Assert.False(Standable.TryFind(Map.Internal, 0, 0, 0, out _));
        Assert.False(Standable.TryFindGround(null, 0, 0, out _));
        Assert.False(Standable.TryFindGround(Map.Internal, 0, 0, out _));
        Assert.Null(Standable.Walker(null).FloorNear(0, 0, 0));
        Assert.Null(Standable.Walker(Map.Internal).FloorNear(0, 0, 0));
    }

    [Fact]
    public void GroundSpan_IsWiderThanTheFloorWindow()
    {
        Assert.True(Standable.GroundSpan >= Standable.DropBelow);
        Assert.True(Standable.GroundSpan >= Standable.ClimbAbove);
    }

    private const int TestX = 10;
    private const int TestY = 10;
    private const int TestZ = 7;
    private const int LandedZ = 9;

    /// <summary>Stands in for the engine and records what the step check asked it.</summary>
    private sealed class RecordingMovement : IMovementImpl
    {
        /// <summary>What the engine answers: a step allowed or refused.</summary>
        public bool Allows { get; init; } = true;

        public int Asked { get; private set; }
        public Point3D AskedFrom { get; private set; }
        public int ProbeZ { get; private set; }
        public bool ProbeIsPlayer { get; private set; }
        public int ProbeBody { get; private set; }
        public Direction AskedDirection { get; private set; }

        public bool CheckMovement(Mobile m, Direction d, out int newZ) => CheckMovement(m, m.Map, m.Location, d, out newZ);

        public bool CheckMovement(Mobile m, Map map, Point3D loc, Direction d, out int newZ)
        {
            Asked++;
            AskedFrom = loc;
            ProbeZ = m.Z;
            ProbeIsPlayer = m.Player;
            ProbeBody = m.Body.BodyID;
            AskedDirection = d;
            newZ = LandedZ;
            return Allows;
        }
    }

    [Fact]
    public void TryStep_AsksTheEngineFromTheStartSpot()
    {
        // The engine keeps the landing floor nearest the mobile's own height, so the probe
        // must stand on the start spot. It is a player, for the diagonal corner rule, and
        // wears a person's body, which a door leaf stops as it stops the walker.
        var map = TestMap.EnsureLand();
        var engine = Movement.Impl;
        var recorder = new RecordingMovement();
        Movement.Impl = recorder;

        try
        {
            Assert.True(Standable.TryStep(map, TestX, TestY, TestZ, TestX + 1, TestY + 1, out var z));
            Assert.Equal(LandedZ, z);
            Assert.Equal(new Point3D(TestX, TestY, TestZ), recorder.AskedFrom);
            Assert.Equal(TestZ, recorder.ProbeZ);
            Assert.True(recorder.ProbeIsPlayer);
            Assert.Equal(Standable.WalkerBody, recorder.ProbeBody);
            Assert.Equal(Direction.Down, recorder.AskedDirection);
            Assert.True(Standable.Walker(map).Step(TestX, TestY, TestZ, TestX, TestY - 1, out _));
            Assert.Equal(Direction.North, recorder.AskedDirection);
        }
        finally
        {
            Movement.Impl = engine;
        }
    }

    [Fact]
    public void TryStep_RefusedWithNoDoorAhead_IsNotAskedAgainThroughTheDoors()
    {
        // Only a closed door straight ahead earns the door-opening body's second look.
        var map = TestMap.EnsureLand();
        var engine = Movement.Impl;
        var recorder = new RecordingMovement { Allows = false };
        Movement.Impl = recorder;

        try
        {
            Assert.False(Standable.TryStep(map, TestX, TestY, TestZ, TestX, TestY - 1, out _));
            Assert.Equal(1, recorder.Asked);
            Assert.Equal(Standable.WalkerBody, recorder.ProbeBody);
        }
        finally
        {
            Movement.Impl = engine;
        }
    }

    [Fact]
    public void TryStep_OnlyToANeighbourOnALiveMap()
    {
        var map = TestMap.EnsureLand();

        Assert.False(Standable.TryStep(map, TestX, TestY, 0, TestX + 2, TestY, out _));
        Assert.False(Standable.TryStep(map, TestX, TestY, 0, TestX, TestY, out _));
        Assert.False(Standable.TryStep(Map.Internal, TestX, TestY, 0, TestX + 1, TestY, out _));
        Assert.False(Standable.TryStep(null, TestX, TestY, 0, TestX + 1, TestY, out _));
    }

    [Theory]
    [InlineData(NavGateKind.None)]
    [InlineData(NavGateKind.Teleporter)]
    [InlineData(NavGateKind.Moongate)]
    public void GatePad_NoMap_IsNotUnder(NavGateKind kind)
    {
        Assert.False(GatePad.IsUnder(null, Point3D.Zero, kind));
        Assert.False(GatePad.IsUnder(Map.Internal, Point3D.Zero, kind));
    }
}
