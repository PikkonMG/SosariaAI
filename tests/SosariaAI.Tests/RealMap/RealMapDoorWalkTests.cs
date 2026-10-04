using System;
using System.Collections.Generic;
using System.Reflection;
using Server;
using Server.Items;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;
using Moves = Server.Movement.Movement;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// The Minoc bank door on the real Felucca tiles, with its two dark wood leaves placed as the
/// server's door generator places them. Walkers stalled there all evening: a person stood in
/// the doorway, the door could not close, its open leaf blocked the tile beside, and the own
/// step search walked through the leaf and round the doorway corner as if no door stood there.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapDoorWalkTests
{
    private const uint WalkerSerial = 0x3D0D01;
    private const uint BystanderSerial = 0x3D0D02;
    private const int WalkerBody = 0x190;
    private const string TickCountField = "_tickCount";

    /// <summary>
    /// The engine clock the motor tests run at: long after the step clock's start, so a fresh
    /// character counts as standing, not as one that just stepped. The test world's clock stands at zero.
    /// </summary>
    private const long StandingTick = 1_000_000;

    /// <summary>The west leaf's frame; open, it swings one tile south-west onto the porch.</summary>
    private static readonly Point3D WestDoor = new(2503, 559, 0);

    /// <summary>The east leaf's frame; open, it swings one tile south-east.</summary>
    private static readonly Point3D EastDoor = new(2504, 559, 0);

    private static readonly Point3D WestLeaf = new(2502, 560, 0);
    private static readonly Point3D Porch = new(2503, 560, 0);
    private static readonly Point3D EastPorch = new(2504, 560, 0);
    private static readonly Point3D Street = new(2501, 560, 0);
    private static readonly Point3D PorchSouthWest = new(2502, 561, 0);
    private static readonly Point3D BelowPorch = new(2503, 561, 0);
    private static readonly Point3D BankFloor = new(2503, 558, 0);

    private readonly List<IEntity> _placed = [];

    public RealMapDoorWalkTests()
    {
        if (RealMapWorld.Available)
        {
            KitWorld.Ensure();
        }
    }

    [RealMapFact]
    public void ClosedDoor_OpensStraightAhead_NeverCornerwise() =>
        WithBankDoors((_, _) =>
        {
            var map = RealMapWorld.Felucca;

            Assert.True(Step(Porch, WestDoor));
            Assert.False(Step(EastPorch, WestDoor));
            Assert.NotNull(DoorTiles.ClosedDoorAt(map, WestDoor.X, WestDoor.Y, WestDoor.Z));
            Assert.True(DoorTiles.IsDoorway(map, WestDoor.X, WestDoor.Y));
            Assert.False(DoorTiles.IsDoorway(map, Porch.X, Porch.Y));
        });

    [RealMapFact]
    public void OpenDoor_ItsLeafBlocksTheTileBeside_AndTheCornerPastIt() =>
        WithBankDoors((west, east) =>
        {
            var map = RealMapWorld.Felucca;
            west.Open = true;
            east.Open = true;

            Assert.Equal(WestLeaf, west.Location);
            Assert.False(Step(Street, WestLeaf));
            Assert.False(Step(Porch, PorchSouthWest));
            Assert.True(Step(Porch, WestDoor));
            Assert.Null(DoorTiles.ClosedDoorAt(map, WestDoor.X, WestDoor.Y, WestDoor.Z));
            Assert.True(DoorTiles.IsDoorway(map, WestDoor.X, WestDoor.Y));
            Assert.False(DoorTiles.IsDoorway(map, WestLeaf.X, WestLeaf.Y));
        });

    [RealMapFact]
    public void OwnSteps_WalkRoundTheOpenLeaf_IntoTheBank() =>
        WithBankDoors((west, east) =>
        {
            west.Open = true;
            east.Open = true;
            var walker = RealMapWorld.Walker();

            var steps = FloorSteps.Around(Street, BankFloor, 0, walker.SafeStep);

            Assert.NotNull(steps);
            var at = Street;

            foreach (var direction in steps)
            {
                var x = at.X;
                var y = at.Y;
                Moves.Offset(direction, ref x, ref y);
                Assert.True(walker.Step(at.X, at.Y, at.Z, x, y, out var z), $"step {direction} from {at} is refused");
                at = new Point3D(x, y, z);
                Assert.NotEqual(WestLeaf, at);
            }

            Assert.Equal(BankFloor, at);
        });

    [RealMapFact]
    public void StandingInTheDoorway_StepsOutOfIt_OntoAFreeTile() =>
        WithBankDoors((west, east) =>
        {
            var map = RealMapWorld.Felucca;
            west.Open = true;
            east.Open = true;
            var person = Place(WalkerSerial, WestDoor);
            var bystander = Place(BystanderSerial, Porch);
            person.Direction = Direction.South;

            Assert.True(person.Motor.StepOutOfDoorway());
            Assert.False(DoorTiles.IsDoorway(map, person.X, person.Y));
            Assert.NotEqual(WestLeaf, person.Location);
            Assert.NotEqual(bystander.Location, person.Location);
        });

    [RealMapFact]
    public void StandingBesideTheDoorway_StaysPut() =>
        WithBankDoors((_, _) =>
        {
            var person = Place(WalkerSerial, Porch);

            Assert.False(person.Motor.StepOutOfDoorway());
            Assert.Equal(Porch, person.Location);
        });

    [RealMapFact]
    public void MoveToPoint_RangeZero_StepsOntoThePoint() =>
        WithBankDoors((_, _) =>
        {
            // A walk onto an exit pad stood one tile short: the motor's reach of one called it there.
            var person = Place(WalkerSerial, BelowPorch);

            person.Motor.MoveToPoint(Porch);
            Assert.Equal(BelowPorch, person.Location);
            person.Motor.MoveToPoint(Porch, 0);
            Assert.Equal(Porch, person.Location);
        });

    private static bool Step(Point3D from, Point3D to) =>
        Standable.TryStep(RealMapWorld.Felucca, from.X, from.Y, from.Z, to.X, to.Y, out _);

    private SosariaCharacter Place(uint serial, Point3D at)
    {
        var person = new SosariaCharacter((Serial)serial) { Name = $"Walker {serial:X}" };
        _placed.Add(person);
        person.DefaultMobileInit();
        person.Body = WalkerBody;
        person.MoveToWorld(at, RealMapWorld.Felucca);
        return person;
    }

    /// <summary>Runs with the Minoc bank's two linked leaves in place, shut, and takes everything placed away after.</summary>
    private void WithBankDoors(Action<BaseDoor, BaseDoor> test)
    {
        var west = new DarkWoodDoor(DoorFacing.WestCW);
        var east = new DarkWoodDoor(DoorFacing.EastCCW);
        _placed.Add(west);
        _placed.Add(east);
        west.Link = east;
        east.Link = west;
        west.MoveToWorld(WestDoor, RealMapWorld.Felucca);
        east.MoveToWorld(EastDoor, RealMapWorld.Felucca);
        var clock = typeof(Core).GetField(TickCountField, BindingFlags.NonPublic | BindingFlags.Static)!;
        var tick = clock.GetValue(null);
        clock.SetValue(null, StandingTick);

        try
        {
            test(west, east);
        }
        finally
        {
            clock.SetValue(null, tick);
            TestMap.EnsureRunningWorld();
            TestMap.Remove(_placed);
            _placed.Clear();
        }
    }
}
