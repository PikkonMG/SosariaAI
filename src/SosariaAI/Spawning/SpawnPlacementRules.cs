using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Spawning;

/// <summary>The placement test a spawn tile failed.</summary>
public enum SpawnReject
{
    /// <summary>No floor at the tile on the site's own floor.</summary>
    NoFloor,

    /// <summary>A floor, but the engine would not spawn a person there: a wall, an item, a person, or a region that refuses spawns.</summary>
    Blocked,

    /// <summary>A free tile with no straight walk to the site.</summary>
    NoWalkLine,

    /// <summary>A free tile that no route proof leads out of to the world's roads.</summary>
    Sealed
}

/// <summary>
/// Where a fresh person lands when its site has no room: a tile a few steps from its bank,
/// as a player logs in at the bank, a straight walk from the bank's proven anchor, and the
/// one line that names which test turned a spawn down. Pure.
/// </summary>
public static class SpawnPlacementRules
{
    /// <summary>A person placed at its bank stands at most this many tiles from the bank's own spot.</summary>
    public const int BankScatterTiles = 3;

    /// <summary>Bank tiles tried, a stable pick per person and try, before the bank's own spot.</summary>
    public const int BankTries = 12;

    private const int BankSpan = BankScatterTiles * 2 + 1;
    private const int RowSalt = 17;

    /// <summary>A tile within <see cref="BankScatterTiles"/> of the bank, the same one per person and try.</summary>
    public static Point3D BankTile(Point3D bank, string uniqueId, int attempt)
    {
        var roll = WorkSites.StableRoll(uniqueId, attempt);
        var dx = roll % BankSpan - BankScatterTiles;
        var dy = roll / RowSalt % BankSpan - BankScatterTiles;
        return new Point3D(bank.X + dx, bank.Y + dy, bank.Z);
    }

    /// <summary>
    /// The test that finally turned a spawn down: the last one, in the order they run, that any
    /// tile failed. A tile that failed a later test passed every earlier one, so the count of
    /// sea tiles with no floor never hides the free tiles that had no route out.
    /// <see cref="SpawnReject.NoFloor"/> when none was counted.
    /// </summary>
    public static SpawnReject Decisive(IReadOnlyDictionary<SpawnReject, int> rejects)
    {
        var decisive = SpawnReject.NoFloor;

        foreach (var reject in Enum.GetValues<SpawnReject>())
        {
            if ((rejects?.GetValueOrDefault(reject) ?? 0) > 0)
            {
                decisive = reject;
            }
        }

        return decisive;
    }

    /// <summary>The words the boot summary uses for a failed test.</summary>
    public static string Describe(SpawnReject reject) =>
        reject switch
        {
            SpawnReject.NoFloor => "no floor on the site's level",
            SpawnReject.Blocked => "no free tile the engine would spawn on",
            SpawnReject.NoWalkLine => "no straight walk to the site",
            _ => "no route out to the roads"
        };

    /// <summary>The boot summary's list of failed tests: "no floor on the site's level 120, ..." in test order.</summary>
    public static string Summary(IReadOnlyDictionary<SpawnReject, int> counts)
    {
        var parts = new List<string>();

        foreach (var reject in Enum.GetValues<SpawnReject>())
        {
            var count = counts?.GetValueOrDefault(reject) ?? 0;

            if (count > 0)
            {
                parts.Add($"{Describe(reject)} {count}");
            }
        }

        return string.Join(", ", parts);
    }
}
