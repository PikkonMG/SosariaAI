using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class GateTravelTests : IDisposable
{
    private const int GhostBody = 0x192;
    private static readonly Point3D Dest = new(100, 200, 0);
    private static readonly Point3D Pad = new(10, 20, 0);
    private static uint _nextSerial = 0xA501;

    private readonly List<IEntity> _placed = [];

    static GateTravelTests() => Timer.Init(0);

    public GateTravelTests()
    {
        TestMap.EnsureInternal();
        TestMap.EnsureRunningWorld();
    }

    public void Dispose() => TestMap.Remove(_placed);

    [Fact]
    public void StepThroughTeleporter_NullCharacter_IsRefused()
    {
        Assert.Equal(GateStep.Refused, GateTravel.StepThroughTeleporter(null, Dest, "test"));
    }

    [Fact]
    public void SpellGates_NullCharacter_FindNothingAndMoveNobody()
    {
        Assert.Null(GateTravel.FindSpellGateToward(null, Dest, null));
        Assert.Equal(GateStep.Refused, GateTravel.StepIntoSpellGate(null, null, "test"));
    }

    [Theory]
    [InlineData(NavGateKind.None)]
    [InlineData(NavGateKind.Teleporter)]
    [InlineData(NavGateKind.Moongate)]
    public void Apply_NoCharacterOnAGate_MovesNobody(NavGateKind kind)
    {
        Assert.Equal(GateStep.Refused, GateTravel.Apply(null, kind, Dest, null, "test"));
    }

    [Fact]
    public void ApplyMoongate_NullCharacter_IsRefused()
    {
        Assert.Equal(GateStep.Refused, GateTravel.ApplyMoongate(null, Dest, null));
    }

    [Theory]
    [InlineData(false, true, true, true, true)]
    [InlineData(false, false, true, true, true)]
    [InlineData(true, true, false, false, true)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, false, true, true, false)]
    public void MayTakeMoongate_ARedOnlyBetweenUnguardedFeluccaPads(
        bool murderer,
        bool felucca,
        bool originGuarded,
        bool destinationGuarded,
        bool may
    ) =>
        Assert.Equal(may, GateTravel.MayTakeMoongate(murderer, felucca, originGuarded, destinationGuarded));

    /// <summary>A red ghost was let through any public moongate; the contract bars it as it bars a living red.</summary>
    [Fact]
    public void MayTakeMoongate_ARedGhostIsHeldToTheRedRule_ABlueGhostIsNot()
    {
        var redGhost = Ghost();
        redGhost.Kills = PkRules.MurdersToRed;
        var blueGhost = Ghost();

        Assert.True(redGhost.IsGhost);
        Assert.False(GateTravel.MayTakeMoongate(redGhost, Pad, Dest, Map.Internal));
        Assert.True(GateTravel.MayTakeMoongate(blueGhost, Pad, Dest, Map.Internal));
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    public void MayStepThrough_NoFoe_NotGray_OutOfTheHeat(bool fighting, bool criminal, bool inHeat, bool may) =>
        Assert.Equal(may, GateTravel.MayStepThrough(fighting, criminal, inHeat));

    /// <summary>
    /// A party gate took anyone beside it: the engine's spell gate asks no heat and no flag.
    /// A gray beside the gate waits there and is not carried.
    /// </summary>
    [Fact]
    public void StepIntoSpellGate_AGrayBesideTheGateWaits()
    {
        var gray = Person();
        gray.Location = Pad;
        gray.Criminal = true;
        var gate = new Moongate(Dest, Map.Internal);
        _placed.Add(gate);
        gate.Location = Pad;

        Assert.Equal(GateStep.Waiting, GateTravel.StepIntoSpellGate(gray, gate, "test"));
        Assert.Equal(Pad, gray.Location);
    }

    private static SosariaCharacter Ghost()
    {
        var ghost = Person();
        ghost.Player = true;
        ghost.Body = GhostBody;
        return ghost;
    }

    private static SosariaCharacter Person()
    {
        var person = new SosariaCharacter((Serial)_nextSerial++);
        person.DefaultMobileInit();
        return person;
    }

    [Fact]
    public void MoongateLine_NamesBothEnds() =>
        Assert.Equal(
            "Una took a moongate from (4467, 1283, 5) to (1828, 2948, -20)",
            GateTravel.MoongateLine("Una", new Point3D(4467, 1283, 5), new Point3D(1828, 2948, -20))
        );

    [Fact]
    public void NeedsFootProof_OnlyAGoalWithinTheTileRoutersReach()
    {
        var pad = new Point3D(1828, 2948, -20);

        Assert.True(GateTravel.NeedsFootProof(pad, new Point3D(1828 + TileRoute.MaxTripTiles, 2948, 0)));
        Assert.False(GateTravel.NeedsFootProof(pad, new Point3D(1828 + TileRoute.MaxTripTiles + 1, 2948, 0)));
        Assert.False(GateTravel.NeedsFootProof(pad, Point3D.Zero));
    }

    [Fact]
    public void ExitReachCells_IsOneWorldSearchForAllTheExits() =>
        Assert.Equal(TileRoute.WorldMaxCells, GateTravel.ExitReachCells);

    [Fact]
    public void NoGateLine_NamesTheSpot()
    {
        Assert.Equal("Anna found no gate at (1336,1997)", GateTravel.NoGateLine("Anna", new Point3D(1336, 1997, 5)));
    }

    [Theory]
    [InlineData(NavGateKind.None)]
    [InlineData(NavGateKind.Teleporter)]
    [InlineData(NavGateKind.Moongate)]
    public void Apply_NullCharacter_DoesNotThrow(NavGateKind kind)
    {
        GateTravel.Apply(null, kind, Dest, null, "test");
    }

    [Fact]
    public void ApplyMoongate_NullCharacter_DoesNotThrow()
    {
        GateTravel.ApplyMoongate(null, Dest, null);
    }

}
