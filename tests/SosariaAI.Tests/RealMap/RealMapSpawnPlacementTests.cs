using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Combat;
using SosariaAI.Navigation;
using SosariaAI.Spawning;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// Fresh spawns on the real Felucca tiles and the live nav graph. 189 of a 1200-person plan
/// were never spawned, boot after boot: among them copies homed in Jhelom and Skara Brae.
/// Their town sites are bank tiles under the bank roof, where no straight line reaches a
/// street, so the route proof turned the site down until it learned to leave by the door.
/// Moonglow's banker tile holds a bookcase on the live server, so its ground there was the
/// bookcase top; the Moonglow tests stand the bank's fixtures on the map first.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapSpawnPlacementTests(ITestOutputHelper output)
{
    private const int PlannedFleet = 1200;

    /// <summary>The population of a fresh world, which sets how wide copies scatter.</summary>
    private const int FreshWorld = 800;

    private const int BritainCopies = 150;
    private const string BritainCopyIdPrefix = "Felucca:britain-spread#";

    /// <summary>Tiles round the bank tile that count as its plaza.</summary>
    private const int BankPlazaTiles = 6;

    /// <summary>At most one Britain copy in ten on the plaza; an even spread puts one in twenty there.</summary>
    private const int PlazaShareLimit = 10;

    /// <summary>
    /// Copies tried per island site. Before the door proof 8 of Jhelom's and 17 of Skara's were
    /// refused; before the site anchor stepped off the bookcase, every one of Moonglow's.
    /// </summary>
    private const int CopiesPerSite = 60;

    private const string CopyIdPrefix = "Felucca:tam#";

    /// <summary>A copy the live boot never placed: its town site is Jhelom's.</summary>
    private const string JhelomCopy = "Felucca:tam#2";

    private const string DenRed = "Felucca:orla#45";

    /// <summary>Test items' serials, clear of every serial other tests use.</summary>
    private const uint FixtureSerialBase = 0x4F000000;

    /// <summary>The height a person stands at on top of a Moonglow bank bookcase.</summary>
    private const int BookcaseTop = 20;

    private const int BookcaseEast = 0x0A97;
    private const int BookcaseMiddle = 0x0A98;
    private const int BookcaseCorner = 0x0A9B;
    private const int BambooChair = 0x0B5B;

    /// <summary>
    /// The Moonglow bank's fixtures as the server's decoration stands them: bookcases along
    /// the banker's row, one on the banker's own tile, and chairs in front. The test world
    /// loads no world items, so without them Moonglow passed here and failed on the live server.
    /// </summary>
    private static readonly (int ItemId, Point3D At)[] MoonglowBankFixtures =
    [
        (BookcaseEast, new Point3D(4468, 1156, 0)),
        (BookcaseMiddle, new Point3D(4470, 1156, 0)),
        (BookcaseCorner, new Point3D(4471, 1156, 0)),
        (BookcaseMiddle, new Point3D(4472, 1156, 0)),
        (BookcaseEast, new Point3D(4474, 1156, 0)),
        (BambooChair, new Point3D(4469, 1159, 0)),
        (BambooChair, new Point3D(4471, 1159, 0)),
        (BambooChair, new Point3D(4473, 1159, 0)),
        (BambooChair, new Point3D(4475, 1159, 0))
    ];

    [RealMapFact]
    public void IslandCopies_SpawnAtTheirJhelomSkaraBraeAndMoonglowSites() =>
        RealMapWorld.WithLiveGraph(() =>
        {
            var refused = new List<string>();

            foreach (var site in new[] { WorkSites.JhelomTown, WorkSites.SkaraTown, WorkSites.MoonglowTown })
            {
                refused.AddRange(Refused(site, out _));
            }

            Assert.True(refused.Count == 0, string.Join(Environment.NewLine, refused));
        });

    [RealMapFact]
    public void MoonglowCopies_WithTheBankDecorated_SpawnOnTheBankFloor() =>
        RealMapWorld.WithLiveGraph(() => WithMoonglowBankFixtures(() =>
        {
            var refused = Refused(WorkSites.MoonglowTown, out var placed);

            Assert.True(refused.Count == 0, string.Join(Environment.NewLine, refused));
            Assert.All(placed, location => Assert.True(NavMetric.SameFloor(location.Z, WorkSites.MoonglowTown.Z), $"{location} is off the bank floor"));
        }));

    [RealMapFact]
    public void SiteAnchor_OfTheDecoratedMoonglowBank_StandsBesideTheBookcaseOnTheFloor() =>
        RealMapWorld.WithLiveGraph(() => WithMoonglowBankFixtures(() =>
        {
            var map = RealMapWorld.Felucca;
            var anchor = HomeSpotRules.SiteAnchor(map, WorkSites.MoonglowTown);

            Assert.Equal(BookcaseTop, HomeSpotRules.GroundedAt(map, WorkSites.MoonglowTown).Z);
            Assert.True(NavMetric.SameFloor(anchor.Z, WorkSites.MoonglowTown.Z), $"{anchor} is off the bank floor");
            Assert.Equal(1, NavMetric.Chebyshev(anchor, WorkSites.MoonglowTown));
            Assert.True(HomeSpotRules.AnchorRoutable(map, anchor));
        }));

    [RealMapFact]
    public void BankTile_ByTheDecoratedMoonglowBank_IsOnTheFloorNotTheBookcases() =>
        RealMapWorld.WithLiveGraph(() => WithMoonglowBankFixtures(() =>
        {
            var map = RealMapWorld.Felucca;

            for (var i = 0; i < CopiesPerSite; i++)
            {
                var rejects = new Dictionary<SpawnReject, int>();

                Assert.True(HomeSpotRules.TryBankTile(map, WorkSites.MoonglowTown, CopyIdPrefix + i, rejects, out var tile));
                Assert.True(NavMetric.SameFloor(tile.Z, WorkSites.MoonglowTown.Z), $"{tile} is off the bank floor");
                Assert.InRange(NavMetric.Chebyshev(tile, WorkSites.MoonglowTown), 0, SpawnPlacementRules.BankScatterTiles + 1);
            }
        }));

    [RealMapFact]
    public void JhelomCopy_ScatteredIntoTheSea_SpawnsAtItsSite() =>
        RealMapWorld.WithLiveGraph(() => Assert.True(Resolve(JhelomCopy, WorkSites.JhelomTown, out _).Placed));

    [RealMapFact]
    public void BankTile_ByTheJhelomBank_IsFreeAndClose() =>
        RealMapWorld.WithLiveGraph(() =>
        {
            var map = RealMapWorld.Felucca;
            var rejects = new Dictionary<SpawnReject, int>();

            Assert.True(HomeSpotRules.TryBankTile(map, WorkSites.JhelomTown, JhelomCopy, rejects, out var tile));
            Assert.InRange(NavMetric.Chebyshev(tile, WorkSites.JhelomTown), 0, SpawnPlacementRules.BankScatterTiles);
            Assert.True(map.CanSpawnMobile(tile));
        });

    [RealMapFact]
    public void DenRed_SpawnsInTheDen() =>
        RealMapWorld.WithLiveGraph(() =>
        {
            var (placed, _) = Resolve(DenRed, PkRules.BucsDenHaven, out var location);

            Assert.True(placed);
            Assert.True(PkRules.InBuccaneersDen(location.X, location.Y));
        });

    [RealMapFact]
    public void BritainCopies_SpreadOverTheTown_NotOntoTheBankPlaza() =>
        RealMapWorld.WithLiveGraph(() =>
        {
            // A fresh 800-person world stood 33 Britain people within six tiles of the bank: a
            // scatter into the river west of it, or behind a building with no straight walk to
            // it, ended on the bank's own street. An even spread puts about one in twenty there.
            var planned = SpawnSpread.PlannedCount;
            SpawnSpread.PlannedCount = FreshWorld;
            var onPlaza = 0;
            var refused = new List<string>();

            try
            {
                for (var i = 0; i < BritainCopies; i++)
                {
                    var id = BritainCopyIdPrefix + i;
                    var (placed, rejects) = ResolveAt(id, WorkSites.BritainTown, out var location);

                    if (!placed)
                    {
                        refused.Add($"{id}: {SpawnPlacementRules.Summary(rejects)}");
                    }
                    else if (NavMetric.Chebyshev(location, WorkSites.BritainTown) <= BankPlazaTiles)
                    {
                        onPlaza++;
                    }
                }
            }
            finally
            {
                SpawnSpread.PlannedCount = planned;
            }

            Assert.True(refused.Count == 0, string.Join(Environment.NewLine, refused));
            Assert.True(onPlaza * PlazaShareLimit <= BritainCopies, $"{onPlaza} of {BritainCopies} on the Britain bank plaza");
        });

    /// <summary>Runs with the Moonglow bank fixtures on the real map, and takes them away after.</summary>
    private static void WithMoonglowBankFixtures(Action test)
    {
        var placed = new List<Item>();

        try
        {
            for (var i = 0; i < MoonglowBankFixtures.Length; i++)
            {
                var (itemId, at) = MoonglowBankFixtures[i];
                var fixture = new Item((Serial)(FixtureSerialBase + (uint)i)) { ItemID = itemId, Movable = false, Visible = true };
                fixture.MoveToWorld(at, RealMapWorld.Felucca);
                placed.Add(fixture);
            }

            HomeSpotRules.ResetRoutableCache();
            test();
        }
        finally
        {
            TestMap.EnsureRunningWorld();

            foreach (var fixture in placed)
            {
                fixture.Delete();
            }

            HomeSpotRules.ResetRoutableCache();
        }
    }

    // Copies of one site the spawner's site tries refuse, with the reasons; the placed spots come out.
    private List<string> Refused(Point3D site, out List<Point3D> placed)
    {
        var refused = new List<string>();
        placed = [];

        for (var i = 0; i < CopiesPerSite; i++)
        {
            var id = CopyIdPrefix + i;
            var (resolved, rejects) = Resolve(id, site, out var location);

            if (resolved)
            {
                placed.Add(location);
            }
            else
            {
                refused.Add($"{id} at {site}: {SpawnPlacementRules.Summary(rejects)}");
            }
        }

        return refused;
    }

    // The spawner's site tries for a 1200-person plan.
    private (bool Placed, Dictionary<SpawnReject, int> Rejects) Resolve(string uniqueId, Point3D site, out Point3D location)
    {
        var planned = SpawnSpread.PlannedCount;
        SpawnSpread.PlannedCount = PlannedFleet;

        try
        {
            return ResolveAt(uniqueId, site, out location);
        }
        finally
        {
            SpawnSpread.PlannedCount = planned;
        }
    }

    // The spawner's site tries (SpawnSpread.Tries): the scattered spot, its mirror, the site itself.
    private (bool Placed, Dictionary<SpawnReject, int> Rejects) ResolveAt(string uniqueId, Point3D site, out Point3D location)
    {
        var map = RealMapWorld.Felucca;
        var rejects = new Dictionary<SpawnReject, int>();
        location = site;
        var placed = false;

        foreach (var start in SpawnSpread.Tries(site, SpawnSpread.Offset(site, uniqueId)))
        {
            if (CharacterSpawner.TryResolveSpawnLocation(map, start, site, rejects, out location))
            {
                placed = true;
                break;
            }
        }

        output.WriteLine($"{uniqueId} at {site}: placed {placed} at {location}, {SpawnPlacementRules.Summary(rejects)}");
        return (placed, rejects);
    }
}
