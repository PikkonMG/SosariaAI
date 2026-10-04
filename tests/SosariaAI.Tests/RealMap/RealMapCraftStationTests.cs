using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// The crafters' stations on the real Felucca tiles. The anvils and most forges are the
/// server's decoration items, read here from its decoration files; the forges of Skara Brae
/// and Jhelom are map statics, read from the real map round each anvil as the game does.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapCraftStationTests(ITestOutputHelper output)
{
    /// <summary>A station or shop this close to a town's bank serves that town.</summary>
    private const int TownReach = 200;

    /// <summary>A stand spot stands when the walker finds a floor this close to its height.</summary>
    private const int FloorSlack = 5;

    private const string DecorationFolder = "Decoration";
    private const string SharedDecoration = "Britannia";
    private const string DecorationExtension = "*.cfg";
    private const string CommentMark = "#";
    private const string HexPrefix = "0x";
    private const int CoordinateCount = 3;
    private const int TypeLineParts = 2;
    private const string DestinationsFile = "destinations-felucca.json";

    /// <summary>The Britain smithy on its raised floor, the one the owner named: anvil 1423,1556 and forge 1424,1558 at z 30.</summary>
    private static readonly Point3D BritainSmithy = new(1423, 1557, 30);

    [RealMapFact]
    public void SmithStations_EveryTownASmithHomesIn_HasAForgeToStandAt()
    {
        var map = RealMapWorld.Felucca;
        var walker = RealMapWorld.Walker();
        var spots = SmithSpots(map);
        var failed = new List<string>();

        foreach (var town in CraftCareerRules.HomesFor(SkillKinds.Smith))
        {
            var standing = spots
                .Where(stand => NavMetric.Chebyshev(stand.Spot, town.Home) <= TownReach && Stands(walker, stand.Spot))
                .ToList();

            output.WriteLine($"{town.Name}: {string.Join("; ", standing.Select(stand => $"{stand.Spot} range {stand.Range}"))}");

            if (standing.Count == 0)
            {
                failed.Add(town.Name);
            }
        }

        Assert.True(failed.Count == 0, $"no workable forge near: {string.Join(", ", failed)}");
    }

    [RealMapFact]
    public void SmithStations_TheBritainSmithyOnItsRaisedFloor()
    {
        var walker = RealMapWorld.Walker();
        var britain = SmithSpots(RealMapWorld.Felucca).OrderBy(stand => NavMetric.Chebyshev(stand.Spot, BritainSmithy)).First();

        Assert.True(NavMetric.Chebyshev(britain.Spot, BritainSmithy) <= CraftStationRules.SameStationTiles, $"nearest station {britain.Spot}");
        Assert.Equal(BritainSmithy.Z, britain.Spot.Z);
        Assert.True(Stands(walker, britain.Spot), $"{britain.Spot} does not stand");
        Assert.Equal(CraftStationRules.StandRange, britain.Range);
    }

    [RealMapFact]
    public void Shops_EveryTownACrafterHomesIn_HasAShopOfItsTrade()
    {
        var path = Path.Combine(RealMapWorld.NavRoot, DestinationsFile);

        if (!File.Exists(path))
        {
            output.WriteLine($"No {DestinationsFile} in the nav folder yet; the shop check waits for a nav build.");
            return;
        }

        var file = JsonSerializer.Deserialize<DestinationFile>(File.ReadAllText(path));
        var catalog = new DestinationCatalog(file?.Destinations ?? []);
        var failed = new List<string>();

        foreach (var career in CraftCareerRules.Careers.Where(career => career.Kind != SkillKinds.Smith))
        {
            var trade = CraftCareerRules.TradeByKind(career.Kind);

            foreach (var town in CraftCareerRules.HomesFor(career.Kind))
            {
                var shop = trade.StationShopTokens
                    .SelectMany(token => catalog.NearestFirst(token, town.Home, 1))
                    .OrderBy(dest => NavMetric.Chebyshev(dest.Arrival, town.Home))
                    .FirstOrDefault();

                var distance = shop == null ? int.MaxValue : NavMetric.Chebyshev(shop.Arrival, town.Home);
                output.WriteLine($"{career.Kind} {town.Name}: {shop?.Name ?? "none"} at {distance}");

                if (distance > TownReach)
                {
                    failed.Add($"{career.Kind} in {town.Name}");
                }
            }
        }

        Assert.True(failed.Count == 0, $"no shop of the trade near: {string.Join(", ", failed)}");
    }

    private static bool Stands(TileWalker walker, Point3D spot) =>
        walker.FloorNear(spot.X, spot.Y, spot.Z) is { } floor && Math.Abs(floor - spot.Z) <= FloorSlack;

    // Anvils and forges from the decoration files, and forge statics round each anvil.
    private static List<StandSpot> SmithSpots(Map map)
    {
        var anvils = new List<Point3D>();
        var forges = new List<Point3D>();

        foreach (var folder in new[] { SharedDecoration, RealMapWorld.FacetName })
        {
            var path = Path.Combine(RealMapWorld.ServerDataRoot, DecorationFolder, folder);

            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var cfg in Directory.EnumerateFiles(path, DecorationExtension))
            {
                ReadDecoration(File.ReadLines(cfg), anvils, forges);
            }
        }

        foreach (var anvil in anvils)
        {
            for (var dx = -CraftStationRules.SearchRadius; dx <= CraftStationRules.SearchRadius; dx++)
            {
                for (var dy = -CraftStationRules.SearchRadius; dy <= CraftStationRules.SearchRadius; dy++)
                {
                    foreach (var tile in map.Tiles.GetStaticAndMultiTiles(anvil.X + dx, anvil.Y + dy))
                    {
                        var at = new Point3D(anvil.X + dx, anvil.Y + dy, tile.Z);

                        if (SmeltRules.IsForgeId(tile.ID) && !forges.Contains(at))
                        {
                            forges.Add(at);
                        }
                    }
                }
            }
        }

        return CraftStationRules.PairSpots(anvils, forges);
    }

    // A decoration file: a "Type 0xID (props)" line, then one "x y z" line per placed item.
    private static void ReadDecoration(IEnumerable<string> lines, List<Point3D> anvils, List<Point3D> forges)
    {
        var itemId = -1;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (line.Length == 0 || line.StartsWith(CommentMark, StringComparison.Ordinal))
            {
                itemId = -1;
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (char.IsLetter(line[0]))
            {
                itemId = parts.Length >= TypeLineParts && parts[1].StartsWith(HexPrefix, StringComparison.OrdinalIgnoreCase) &&
                         int.TryParse(parts[1][HexPrefix.Length..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id)
                    ? id
                    : -1;
                continue;
            }

            if (itemId < 0 || parts.Length < CoordinateCount ||
                !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y) || !int.TryParse(parts[2], out var z))
            {
                continue;
            }

            if (CraftStations.IsAnvilId(itemId))
            {
                anvils.Add(new Point3D(x, y, z));
            }
            else if (SmeltRules.IsForgeId(itemId))
            {
                forges.Add(new Point3D(x, y, z));
            }
        }
    }
}
