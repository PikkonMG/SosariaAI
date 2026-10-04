using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Items;
using SosariaAI.Navigation;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// The Covetous floor traps, placed as the server's own decoration file places them, on the
/// real Felucca tiles: routes into the dungeon from the level one landing go round them.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapTrapTests
{
    private const string DecorationFolder = "Decoration";
    private const string BritanniaFolder = "Britannia";
    private const string CovetousFile = "_covetous.cfg";
    private const string ItemNamespace = "Server.Items.";
    private const string CommentMark = "#";
    private const int PointParts = 3;

    /// <summary>Where the Covetous entrance teleporter lands on level one.</summary>
    private static readonly Point3D LevelOneLanding = new(5456, 1863, 0);

    /// <summary>Traps this close to the landing lie on the first walk into the dungeon.</summary>
    private const int EntranceTraps = 40;

    /// <summary>A goal this far past a trap, seen from the landing, is a walk that may cross it.</summary>
    private const int PastTrap = 2;

    private readonly ITestOutputHelper _output;

    public RealMapTrapTests(ITestOutputHelper output) => _output = output;

    [RealMapFact]
    public void TileRoute_PastTheCovetousEntranceTraps_GoesRoundThem()
    {
        var traps = new TrapField(CovetousTraps());
        var plain = RealMapWorld.Walker();
        var knowing = plain with { Harms = traps.Harms };
        var crossings = 0;

        Assert.True(traps.Count > 0, "no Covetous traps read from the decoration file");

        foreach (var trap in CovetousTraps().Where(trap => NavMetric.Chebyshev(new Point3D(trap.X, trap.Y, trap.Z), LevelOneLanding) <= EntranceTraps))
        {
            var goal = new Point3D(
                trap.X + PastTrap * Math.Sign(trap.X - LevelOneLanding.X),
                trap.Y + PastTrap * Math.Sign(trap.Y - LevelOneLanding.Y),
                trap.Z
            );
            var plainTrail = TileRoute.Trail(LevelOneLanding, goal, plain, RealMapWorld.IsIndoor);

            if (plainTrail.Count == 0 || knowing.IsHarmed(goal.X, goal.Y, goal.Z))
            {
                continue;
            }

            var knowingTrail = TileRoute.Trail(LevelOneLanding, goal, knowing, RealMapWorld.IsIndoor);
            var plainHurt = plainTrail.Count(tile => knowing.IsHarmed(tile.X, tile.Y, tile.Z));
            var knowingHurt = knowingTrail.Count(tile => knowing.IsHarmed(tile.X, tile.Y, tile.Z));
            _output.WriteLine($"past the trap at ({trap.X},{trap.Y}) to {goal}: plain {plainTrail.Count} tiles, {plainHurt} trapped; knowing {knowingTrail.Count} tiles, {knowingHurt} trapped");

            Assert.NotEmpty(knowingTrail);
            Assert.True(knowingHurt <= plainHurt, $"the route to {goal} steps on more trapped tiles than a plain walk");

            if (plainHurt > knowingHurt)
            {
                crossings++;
            }
        }

        Assert.True(crossings > 0, "no walk from the Covetous landing went round a trap a plain walk crossed");
    }

    /// <summary>
    /// The traps of the Covetous decoration file: a trap type line, then one line per trap
    /// with its x, y and z, until a blank or comment line.
    /// </summary>
    private static List<TrapZone> CovetousTraps()
    {
        var path = Path.Combine(RealMapWorld.ServerDataRoot, DecorationFolder, BritanniaFolder, CovetousFile);
        var zones = new List<TrapZone>();
        int? reach = null;

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();

            if (line.Length == 0 || line.StartsWith(CommentMark, StringComparison.Ordinal))
            {
                reach = null;
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == PointParts && parts.All(part => int.TryParse(part, out _)))
            {
                if (reach is { } found)
                {
                    zones.Add(new TrapZone(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), found));
                }

                continue;
            }

            var type = typeof(BaseTrap).Assembly.GetType(ItemNamespace + parts[0]);
            reach = type != null && typeof(BaseTrap).IsAssignableFrom(type) ? TrapTiles.ReachOf(type) : null;
        }

        return zones;
    }
}
