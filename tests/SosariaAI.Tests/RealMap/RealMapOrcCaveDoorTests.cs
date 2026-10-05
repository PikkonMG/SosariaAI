using System;
using System.Collections.Generic;
using System.Reflection;
using Server;
using Server.Items;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// The Orc Cave door on the real Felucca tiles, with the posts, rail and flowstone its
/// decoration lays round the two pads in. The rune for the door lands north of the frame:
/// walkers walked straight at the pad, into the posts, and stood there until the step ran out.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapOrcCaveDoorTests
{
    private const uint WalkerSerial = 0x3D0E01;
    private const int WalkerBody = 0x190;
    private const string TickCountField = "_tickCount";
    private const long StandingTick = 1_000_000;
    private const int ThinkMs = 1_000;
    private const int MaxThinks = 12;

    /// <summary>The items the server's Orc Cave decoration lays round the door: item id and tile.</summary>
    private static readonly (int Id, int X, int Y, int Z)[] DoorFrame =
    [
        (0x0009, 1013, 1432, 0), (0x0009, 1013, 1435, 0), (0x0009, 1014, 1432, 1), (0x0009, 1014, 1435, 0),
        (0x0434, 1015, 1432, 0), (0x0434, 1015, 1435, 0), (0x08E0, 1012, 1431, 0), (0x08E0, 1012, 1433, 21),
        (0x08E0, 1013, 1437, 0), (0x08E2, 1012, 1433, 2), (0x08E2, 1012, 1435, 0), (0x08E2, 1012, 1436, 0),
        (0x08E6, 1012, 1434, 21), (0x08E6, 1013, 1435, 21), (0x08E8, 1012, 1432, 0), (0x08E8, 1012, 1434, 0),
        (0x08E8, 1013, 1432, 0), (0x08E8, 1013, 1436, 0), (0x08FB, 1014, 1432, 0), (0x08FB, 1014, 1435, 0),
        (0x1775, 1013, 1434, 21), (0x1776, 1012, 1435, 21), (0x1776, 1013, 1433, 21)
    ];

    private static readonly Point3D[] Pads = [new(1013, 1433, 0), new(1013, 1434, 0)];
    private static readonly Point3D PadsLand = new(5138, 2016, 0);

    /// <summary>Where the door rune sets a person down, north of the frame.</summary>
    private static readonly Point3D RuneSpot = new(1015, 1429, 0);

    /// <summary>Where walkers stood beside the frame when the step ran out.</summary>
    private static readonly Point3D BesideTheFrame = new(1015, 1432, 0);

    [RealMapFact]
    public void FromTheRuneSpot_WalksRoundTheFrameOntoAPad() => AssertReachesAPad(RuneSpot);

    [RealMapFact]
    public void FromBesideTheFrame_StepsOntoAPad() => AssertReachesAPad(BesideTheFrame);

    private static void AssertReachesAPad(Point3D start)
    {
        RealMapWorld.Walker();
        KitWorld.Ensure();
        var placed = new List<IEntity>();
        var clock = typeof(Core).GetField(TickCountField, BindingFlags.NonPublic | BindingFlags.Static)!;
        var tick = clock.GetValue(null);
        var now = StandingTick;
        clock.SetValue(null, now);

        try
        {
            foreach (var (id, ix, iy, iz) in DoorFrame)
            {
                var item = new Static(id);
                placed.Add(item);
                item.MoveToWorld(new Point3D(ix, iy, iz), RealMapWorld.Felucca);
            }

            foreach (var at in Pads)
            {
                var pad = new Teleporter(PadsLand, RealMapWorld.Felucca);
                placed.Add(pad);
                pad.MoveToWorld(at, RealMapWorld.Felucca);
            }

            var person = new SosariaCharacter((Serial)WalkerSerial) { Name = "Walker" };
            placed.Add(person);
            person.DefaultMobileInit();
            person.Body = WalkerBody;
            person.MoveToWorld(start, RealMapWorld.Felucca);
            var onPad = false;

            // The test world runs no pad timer, so the person stands on the pad it reached.
            for (var i = 0; i < MaxThinks && !onPad; i++)
            {
                GateTravel.StepThroughTeleporter(person, PadsLand, nameof(RealMapOrcCaveDoorTests));
                onPad = Array.Exists(Pads, pad => pad.X == person.X && pad.Y == person.Y);
                now += ThinkMs;
                clock.SetValue(null, now);
            }

            Assert.True(onPad, $"from {start} the walker stood at {person.Location}");
        }
        finally
        {
            clock.SetValue(null, tick);
            TestMap.EnsureRunningWorld();
            TestMap.Remove(placed);
        }
    }
}
