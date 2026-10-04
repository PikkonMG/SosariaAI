using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class WorkSitesTests
{
    // Fixed values from xxHash32 over uppercase UTF-16LE with the server's seed.
    // Unlike comparing two calls, these catch a process-randomized hash.
    [Theory]
    [InlineData("Felucca:mira#1", 1969214354)]
    [InlineData("Trammel:connor#2", 1545768680)]
    [InlineData("Felucca:tobin#10", 1289624457)]
    public void StableIndex_KeepsTheSameValueAcrossRestarts(string id, int expected)
    {
        Assert.Equal(expected, WorkSites.StableIndex(id));
        Assert.Equal(expected, WorkSites.StableIndex(id.ToLowerInvariant()));
    }

    [Fact]
    public void Fixture_KeepsAuthoredHome()
    {
        var home = WorkSites.HomeFor(CharactersFile.DefaultSpawn, "Felucca:connor", SkillKinds.Lumberjack);
        Assert.Equal(CharactersFile.DefaultSpawn, home);
    }

    [Fact]
    public void Copies_DoNotAllShareBritainWestWood()
    {
        var homes = new HashSet<Point3D>();

        for (var i = 1; i <= 12; i++)
        {
            homes.Add(WorkSites.HomeFor(
                CharactersFile.DefaultSpawn,
                "Felucca:connor#" + i,
                SkillKinds.Lumberjack
            ));
        }

        Assert.True(homes.Count >= 2);
        Assert.Contains(homes, h => h != CharactersFile.DefaultSpawn);
    }

    [Fact]
    public void Harvest_CopyUsesANamedLumberSite()
    {
        var authored = new Rectangle2D(
            CharactersFile.DefaultForestAreaX,
            CharactersFile.DefaultForestAreaY,
            CharactersFile.DefaultForestAreaWidth,
            CharactersFile.DefaultForestAreaHeight
        );
        var area = WorkSites.HarvestFor(SkillKinds.Lumberjack, "Felucca:wren#3", authored);

        Assert.True(area.Width <= WorkSites.PatchSize);
        Assert.True(area.Height <= WorkSites.PatchSize);
    }

    [Fact]
    public void Patch_SplitsALargeArea()
    {
        var area = new Rectangle2D(1000, 1000, 96, 48);
        var a = WorkSites.Patch(area, "a");
        var b = WorkSites.Patch(area, "b#1");

        Assert.True(area.Contains(new Point2D(a.X, a.Y)));
        Assert.True(area.Contains(new Point2D(b.X, b.Y)));
        Assert.True(a.Width <= WorkSites.PatchSize);
        Assert.True(b.Width <= WorkSites.PatchSize);
    }

    [Fact]
    public void Patch_SaltZero_MatchesUnsalted()
    {
        var area = new Rectangle2D(1000, 1000, 96, 48);

        Assert.Equal(WorkSites.Patch(area, "a"), WorkSites.Patch(area, "a", 0));
    }

    [Fact]
    public void Patch_SaltWalksEveryCellOnce()
    {
        // A dead cell rotates to a neighbor, not back to itself: salting walks
        // every cell of the area exactly once before it repeats.
        var area = new Rectangle2D(1000, 1000, 72, 48);
        var count = WorkSites.PatchCount(area);
        var seen = new HashSet<Rectangle2D>();

        for (var i = 0; i < count; i++)
        {
            seen.Add(WorkSites.Patch(area, "a", i));
        }

        Assert.Equal(count, seen.Count);
        Assert.Equal(6, count);
    }

    [Fact]
    public void PatchCount_SmallArea_IsOne()
    {
        Assert.Equal(1, WorkSites.PatchCount(new Rectangle2D(1000, 1000, 20, 20)));
        Assert.Equal(4, WorkSites.PatchCount(new Rectangle2D(1000, 1000, 48, 48)));
    }

    [Fact]
    public void Patch_TheCellsCoverTheWholeSite()
    {
        // The Britain west wood is 40 tiles on a side. Whole cells only gave it one 24-tile
        // cell, every woodcutter there cut the same trees bare, and the 16-tile strip beside
        // it was never worked.
        var wood = WorkSites.BritainWestWood.Harvest;
        var count = WorkSites.PatchCount(wood);
        var covered = new HashSet<Point2D>();

        for (var i = 0; i < count; i++)
        {
            var cell = WorkSites.Patch(wood, "Felucca:connor#1", i);
            Assert.True(cell.Width <= WorkSites.PatchSize && cell.Height <= WorkSites.PatchSize);

            for (var x = cell.X; x < cell.X + cell.Width; x++)
            {
                for (var y = cell.Y; y < cell.Y + cell.Height; y++)
                {
                    Assert.True(covered.Add(new Point2D(x, y)));
                }
            }
        }

        Assert.Equal(4, count);
        Assert.Equal(wood.Width * wood.Height, covered.Count);
    }

    [Fact]
    public void PrimaryWork_Woodcutter_IsLumberjack()
    {
        var file = CharactersFile.CreateDefault();
        var connor = file.Facets[FacetNames.Felucca].Roster[0];
        Assert.Equal(SkillKinds.Lumberjack, WorkSites.PrimaryWork(connor));
    }

    [Fact]
    public void PrimaryWork_ASmithWhoMinesItsOwnOre_IsStillASmith()
    {
        var connor = CharactersFile.CreateDefault().Facets[FacetNames.Felucca].Roster[0];
        var smith = new CharacterDefinition { Id = connor.Id, Routines = connor.Routines, Choices = connor.Choices };
        CraftCareerRules.Apply(smith, SkillKinds.Smith, "Felucca:connor#3");

        Assert.True(smith.UsesSkill(SkillKinds.Mine));
        Assert.Equal(SkillKinds.Smith, WorkSites.PrimaryWork(smith));
    }

    [Fact]
    public void NearestSite_IsTheHarvestNearestHome()
    {
        Assert.Equal(WorkSites.MinocHills, WorkSites.NearestSite(SkillKinds.Mine, WorkSites.MinocTown));
        Assert.Equal(WorkSites.VesperMine, WorkSites.NearestSite(SkillKinds.Mine, WorkSites.VesperTown));
        Assert.Equal(WorkSites.CoveMine, WorkSites.NearestSite(SkillKinds.Mine, WorkSites.BritainTown));
        Assert.Equal(WorkSites.YewWood, WorkSites.NearestSite(SkillKinds.Lumberjack, WorkSites.YewTown));
        Assert.Equal(WorkSites.BritainWestWood, WorkSites.NearestSite(SkillKinds.Lumberjack, WorkSites.BritainTown));
    }

    [Fact]
    public void HarvestSite_ACraftersOwnStockPatchIsTheOneItsRoutineNames()
    {
        for (var i = 1; i <= 20; i++)
        {
            var id = $"Felucca:connor#{i}";

            Assert.Equal(WorkSites.CoveMine.Harvest, WorkSites.HarvestSite(SkillKinds.Mine, id, WorkSites.CoveMine.Harvest, ownStock: true).Harvest);
            Assert.Contains(WorkSites.HarvestSite(SkillKinds.Mine, id, WorkSites.CoveMine.Harvest, ownStock: false), WorkSites.MineSites);
        }
    }

    [Fact]
    public void CopyWithoutHarvestSites_DoesNotUseOrigin()
    {
        var home = WorkSites.HomeFor(CharactersFile.BranSpawn, "Felucca:bran#2", SkillKinds.IdleWander);

        Assert.NotEqual(Point3D.Zero, home);
        Assert.Contains(WorkSites.TownSites, site => site.Home == home);
    }

    [Fact]
    public void FishCopy_UsesTownSiteNotMinocGate()
    {
        var homes = new HashSet<Point3D>();

        for (var i = 1; i <= 9; i++)
        {
            homes.Add(WorkSites.HomeFor(
                CharactersFile.TobinShore,
                "Felucca:tobin#" + i,
                SkillKinds.Fish
            ));
        }

        Assert.DoesNotContain(WorkSites.MinocGate, homes);
        Assert.Contains(homes, h => h == WorkSites.VesperTown || h == WorkSites.SkaraTown || h == CharactersFile.TobinShore);
    }

    [Fact]
    public void MineCopies_DoNotAllShareBritainMine()
    {
        var homes = new HashSet<Point3D>();

        for (var i = 1; i <= 16; i++)
        {
            homes.Add(WorkSites.HomeFor(
                CharactersFile.MiraSpawn,
                "Felucca:mira#" + i,
                SkillKinds.Mine
            ));
        }

        Assert.True(homes.Count >= 3);
        Assert.Contains(WorkSites.CoveMine, WorkSites.MineSites);
        Assert.Contains(WorkSites.VesperMine, WorkSites.MineSites);
    }

    [Fact]
    public void MineCopies_NeverHomeOnTheBritainLedge()
    {
        // The Britain mine ledge at z 40 has one graph edge down, a 13-level drop the
        // pathfinder cannot make. Only the fixture miner has an authored footpath down.
        // Copies homed there failed every trip and piled up on the ledge.
        for (var i = 1; i <= 64; i++)
        {
            var id = "Felucca:mira#" + i;
            Assert.NotEqual(CharactersFile.MiraSpawn, WorkSites.HomeFor(CharactersFile.MiraSpawn, id, SkillKinds.Mine));
            Assert.False(WorkSites.BritainMine.Harvest.Contains(
                new Point2D(WorkSites.WalkTarget(CharactersFile.MiraMineApproach, id))
            ));
        }

        Assert.DoesNotContain(WorkSites.BritainMine, WorkSites.MineSites);
        Assert.Equal(CharactersFile.MiraSpawn, WorkSites.HomeFor(CharactersFile.MiraSpawn, "Felucca:mira", SkillKinds.Mine));
    }

    [Fact]
    public void Approach_SameArea_SpreadsCopies()
    {
        var area = new Rectangle2D(1440, 1500, 60, 50);
        var a = WorkSites.Approach(area, "Felucca:mira#1", 40);
        var b = WorkSites.Approach(area, "Felucca:mira#2", 40);

        Assert.True(area.Contains(new Point2D(a.X, a.Y)));
        Assert.True(area.Contains(new Point2D(b.X, b.Y)));
        Assert.False(a.X == b.X && a.Y == b.Y);
    }

    [Fact]
    public void LumberSites_StayNextToTownGates()
    {
        Assert.Contains(WorkSites.BritainWestWood, WorkSites.LumberSites);
        Assert.Contains(WorkSites.BritainSouthRoad, WorkSites.LumberSites);
        Assert.Contains(WorkSites.YewWood, WorkSites.LumberSites);
        Assert.Contains(WorkSites.MaginciaWood, WorkSites.LumberSites);
        Assert.Equal(WorkSites.YewTown, WorkSites.YewWood.Home);
        Assert.Equal(WorkSites.MaginciaTown, WorkSites.MaginciaWood.Home);
    }

    [Fact]
    public void WalkTarget_FixturesDoNotShareTheForestApproach()
    {
        var a = WorkSites.WalkTarget(CharactersFile.DefaultForestApproach, "Felucca:connor");
        var b = WorkSites.WalkTarget(CharactersFile.DefaultForestApproach, "Felucca:wren");

        Assert.False(a.X == b.X && a.Y == b.Y);
    }

    [Fact]
    public void WalkTarget_LumberCopies_DoNotAllWalkTheBritainWood()
    {
        var tiles = new HashSet<Point3D>();
        var britain = 0;

        for (var i = 1; i <= 16; i++)
        {
            var dest = WorkSites.WalkTarget(CharactersFile.DefaultForestApproach, "Felucca:connor#" + i);
            tiles.Add(dest);

            if (WorkSites.BritainWestWood.Harvest.Contains(new Point2D(dest.X, dest.Y)))
            {
                britain++;
            }
        }

        Assert.True(tiles.Count >= 8);
        Assert.True(britain < 16);
    }

    [Fact]
    public void WalkTarget_MineCopies_LeaveTheBritainApproach()
    {
        var tiles = new HashSet<Point3D>();

        for (var i = 1; i <= 16; i++)
        {
            tiles.Add(WorkSites.WalkTarget(CharactersFile.MiraMineApproach, "Felucca:mira#" + i));
        }

        Assert.True(tiles.Count >= 8);
        Assert.DoesNotContain(CharactersFile.MiraMineApproach, tiles);
    }

    [Fact]
    public void WalkTarget_CopyBank_LeavesTheBritainBankTile()
    {
        var dest = WorkSites.WalkTarget(CharactersFile.DefaultBankSpot, "Felucca:connor#3");

        Assert.NotEqual(CharactersFile.DefaultBankSpot, dest);
        Assert.True(WorkSites.IsCopy("Felucca:connor#3"));
    }

    [Fact]
    public void TownSites_AreBankAreas_NotMoongatePads()
    {
        Assert.Contains(WorkSites.TownSites, site => site.Home == WorkSites.SkaraTown);
        Assert.Contains(WorkSites.TownSites, site => site.Home == WorkSites.JhelomTown);
        Assert.Contains(WorkSites.TownSites, site => site.Home == WorkSites.TrinsicTown);
        Assert.DoesNotContain(WorkSites.TownSites, site => site.Home == WorkSites.SkaraGate);
        Assert.DoesNotContain(WorkSites.TownSites, site => site.Home == WorkSites.JhelomGate);
        Assert.DoesNotContain(WorkSites.TownSites, site => site.Home == WorkSites.TrinsicGate);
    }

    [Fact]
    public void IdleCopies_GiveJhelomOnlyASmallPopulationShare()
    {
        var jhelom = 0;

        for (var i = 1; i <= 256; i++)
        {
            var home = WorkSites.HomeFor(
                CharactersFile.BranSpawn,
                "Felucca:bran#" + i,
                SkillKinds.IdleWander
            );
            if (home == WorkSites.JhelomTown)
            {
                jhelom++;
            }
        }

        Assert.InRange(jhelom, 1, 24);
    }

    [Fact]
    public void AtNearestGate_OnlyTrueWithinGateReach()
    {
        // The trapped miners stood at (3563,2139,31) on the Magincia pad: the gate
        // constant is (3563,2139,0) and the x/y distance is zero at any height.
        Assert.True(WorkSites.AtNearestGate(WorkSites.MaginciaGate));
        Assert.True(WorkSites.AtNearestGate(new Point3D(3563, 2139, 31)));
        Assert.True(WorkSites.AtNearestGate(new Point3D(3563 + GatePad.ReachTiles, 2139, 0)));
        Assert.False(WorkSites.AtNearestGate(new Point3D(3563 + GatePad.ReachTiles + 1, 2139, 0)));
        Assert.False(WorkSites.AtNearestGate(WorkSites.MaginciaTown));
        Assert.False(WorkSites.AtNearestGate(new Point3D(3515, 2120, 20)));
    }

    [Fact]
    public void NearestGate_SkipsTheGatesTheWalkerMayNotUse()
    {
        // The Britain graveyard: the Britain pad is nearest, and guarded for a red.
        var graveyard = new Point3D(1386, 1476, 10);
        bool NotBritain(Point3D gate) => gate != WorkSites.BritainGate;

        Assert.Equal(WorkSites.BritainGate, WorkSites.NearestGate(graveyard));
        Assert.NotEqual(WorkSites.BritainGate, WorkSites.NearestGate(graveyard, NotBritain));
        Assert.Equal(WorkSites.BritainGate, WorkSites.NearestGate(graveyard, static _ => false));
        Assert.True(WorkSites.AtNearestGate(WorkSites.BritainGate));
        Assert.False(WorkSites.AtNearestGate(WorkSites.BritainGate, NotBritain));
    }

    [Fact]
    public void LegacyGateHomes_AreRecognizedForSafeMigration()
    {
        Assert.True(WorkSites.IsLegacyGateHome(WorkSites.JhelomGate));
        Assert.False(WorkSites.IsLegacyGateHome(WorkSites.JhelomTown));
        Assert.True(WorkSites.NeedsNewCopyHome(WorkSites.JhelomGate));
        Assert.True(WorkSites.NeedsNewCopyHome(CharactersFile.MiraSpawn));
        Assert.False(WorkSites.NeedsNewCopyHome(WorkSites.CoveMine.Home));
        Assert.False(WorkSites.NeedsNewCopyHome(WorkSites.JhelomTown));
    }

    [Fact]
    public void WalkTarget_MineCopies_AimAtTheirOwnSiteHeight_NotTheBritainLedge()
    {
        // Cove miners carried the ledge's 40 into the Cove mine, where the floor lies near 0:
        // thirteen walks found no tile to stand on within eight tiles of the goal.
        var cove = 0;

        for (var i = 1; i <= 64; i++)
        {
            var id = "Felucca:mira#" + i;
            var site = WorkSites.SiteFor(SkillKinds.Mine, id, WorkSites.BritainMine.Harvest);
            var dest = WorkSites.WalkTarget(CharactersFile.MiraMineApproach, id);

            Assert.Equal(site.Home.Z, dest.Z);
            cove += site == WorkSites.CoveMine ? 1 : 0;
        }

        Assert.True(cove > 0);
        Assert.NotEqual(CharactersFile.MiraMineApproach.Z, WorkSites.CoveMine.Home.Z);
    }

    [Fact]
    public void ApproachHeight_FixtureKeepsTheAuthoredHeight_CopyTakesItsSite()
    {
        Assert.Equal(
            CharactersFile.MiraMineApproach.Z,
            WorkSites.ApproachHeight(CharactersFile.MiraMineApproach, "Felucca:mira", CharactersFile.MiraSpawn)
        );
        Assert.Equal(
            WorkSites.CoveTown.Z,
            WorkSites.ApproachHeight(CharactersFile.MiraMineApproach, "Felucca:mira#1", WorkSites.CoveTown)
        );
    }

    [Fact]
    public void TryWalkPatch_AWorkWalkNamesThePatchItsTargetLiesIn()
    {
        for (var i = 1; i <= 16; i++)
        {
            var id = "Felucca:mira#" + i;

            Assert.True(WorkSites.TryWalkPatch(CharactersFile.MiraMineApproach, id, out var patch));
            Assert.True(patch.Contains(new Point2D(WorkSites.WalkTarget(CharactersFile.MiraMineApproach, id))));
        }

        Assert.False(WorkSites.TryWalkPatch(CharactersFile.DefaultBankSpot, "Felucca:mira#1", out _));
    }

    [Fact]
    public void WalkTarget_FixtureBank_StaysNearBritainBank()
    {
        var dest = WorkSites.WalkTarget(CharactersFile.DefaultBankSpot, "Felucca:connor");

        Assert.True(NavMetric.Chebyshev(dest, CharactersFile.DefaultBankSpot) <= WorkSites.BankScatterRadius);
        Assert.NotEqual(Point3D.Zero, dest);
    }
}
