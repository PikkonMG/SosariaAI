using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class HarvestTripTests
{
    private const string BritainCopy = "Felucca:connor#4";
    private const string Fixture = "connor";
    private const int Leash = 400;
    private const int WalkMinutes = 5;

    private static readonly DateTime Start = new(2026, 9, 27, 21, 26, 40, DateTimeKind.Utc);
    private static readonly Point3D BritainBank = WorkSites.BritainTown;
    private static readonly Rectangle2D Authored = WorkSites.BritainWestWood.Harvest;
    private static readonly Point3D[] NoWorldPlaces = [];

    // Three places a few hundred tiles from the Britain bank, one far past the leash.
    private static readonly Point3D NearPlace = new(BritainBank.X + 30, BritainBank.Y + 40, 0);
    private static readonly Point3D MiddlePlace = new(BritainBank.X - 120, BritainBank.Y, 0);
    private static readonly Point3D FarPlace = new(BritainBank.X + 350, BritainBank.Y, 0);
    private static readonly Point3D BeyondLeashPlace = new(BritainBank.X + 900, BritainBank.Y, 0);
    private static readonly Point3D[] NearWorldPlaces = [FarPlace, BeyondLeashPlace, NearPlace, MiddlePlace];

    [Fact]
    public void TimeUp_TheWorkClockStartsAtThePatch()
    {
        // Woodcutters walked five minutes to the wood and had three left: "the patch gave nothing" 299 times.
        var arrived = Start + TimeSpan.FromMinutes(WalkMinutes);

        Assert.False(HarvestSkill.TimeUp(Start, arrived, Start + HarvestSkill.WorkTime));
        Assert.False(HarvestSkill.TimeUp(Start, arrived, arrived + HarvestSkill.WorkTime - TimeSpan.FromSeconds(1)));
        Assert.True(HarvestSkill.TimeUp(Start, arrived, arrived + HarvestSkill.WorkTime));
    }

    [Fact]
    public void TimeUp_AWalkThatNeverArrivesEndsWithTheTrip()
    {
        Assert.False(HarvestSkill.TimeUp(Start, default, Start + HarvestSkill.WorkTime));
        Assert.True(HarvestSkill.TimeUp(Start, default, Start + HarvestSkill.TripTime));
    }

    [Fact]
    public void HarvestSites_ACopyTriesItsOwnSiteFirst_ThenTheOthersInItsLeash()
    {
        var sites = WorkSites.HarvestSites(SkillKinds.Lumberjack, BritainCopy, Authored, ownStock: false, BritainBank, Leash, NoWorldPlaces);

        Assert.Equal(WorkSites.HarvestSite(SkillKinds.Lumberjack, BritainCopy, Authored, ownStock: false), sites[0]);
        Assert.True(sites.Count > 1);

        foreach (var site in sites)
        {
            Assert.Contains(site, WorkSites.LumberSites);
        }

        // The Magincia wood lies across the sea, far outside a Britain woodcutter's leash.
        Assert.DoesNotContain(WorkSites.MaginciaWood, sites[1..]);
        Assert.Equal(sites.Count, new System.Collections.Generic.HashSet<WorkSite>(sites).Count);
    }

    [Fact]
    public void HarvestSites_AFixtureAndAnOwnStockCrafterKeepTheirSite()
    {
        Assert.Single(WorkSites.HarvestSites(SkillKinds.Lumberjack, Fixture, Authored, ownStock: false, BritainBank, Leash, NearWorldPlaces));
        Assert.Single(WorkSites.HarvestSites(SkillKinds.Lumberjack, BritainCopy, Authored, ownStock: true, BritainBank, Leash, NearWorldPlaces));
    }

    [Fact]
    public void HarvestSites_ACopyAlsoTriesTheMapsOwnPlacesInItsLeash_NearestFirst()
    {
        var sites = WorkSites.HarvestSites(SkillKinds.Fish, BritainCopy, Authored, ownStock: false, BritainBank, Leash, NearWorldPlaces);
        var world = sites.FindAll(site => site.Name.StartsWith(SkillKinds.Fish + " "));

        Assert.Equal(
            [WorkSites.WorldSite(SkillKinds.Fish, NearPlace), WorkSites.WorldSite(SkillKinds.Fish, MiddlePlace), WorkSites.WorldSite(SkillKinds.Fish, FarPlace)],
            world
        );
        Assert.DoesNotContain(WorkSites.WorldSite(SkillKinds.Fish, BeyondLeashPlace), sites);
    }

    [Fact]
    public void HarvestSites_TakesAtMostTheNearestWorldPlaces()
    {
        var many = new Point3D[WorkSites.MaxWorldSites * 2];

        for (var i = 0; i < many.Length; i++)
        {
            many[i] = new Point3D(BritainBank.X + i * 10, BritainBank.Y, 0);
        }

        var sites = WorkSites.HarvestSites(SkillKinds.Mine, BritainCopy, Authored, ownStock: false, BritainBank, Leash, many);
        var world = sites.FindAll(site => site.Name.StartsWith(SkillKinds.Mine + " "));

        Assert.Equal(WorkSites.MaxWorldSites, world.Count);
        Assert.Contains(WorkSites.WorldSite(SkillKinds.Mine, many[0]), world);
        Assert.DoesNotContain(WorkSites.WorldSite(SkillKinds.Mine, many[^1]), world);
    }

    [Fact]
    public void WorldSite_CoversTheBoxTheNavBuildSampled()
    {
        var site = WorkSites.WorldSite(SkillKinds.Lumberjack, NearPlace);
        var radius = Navigation.Generation.ResourceSampler.SampleRadius;

        Assert.Equal(NearPlace, site.Home);
        Assert.True(site.Harvest.Contains(new Point2D(NearPlace.X - radius, NearPlace.Y - radius)));
        Assert.True(site.Harvest.Contains(new Point2D(NearPlace.X + radius - 1, NearPlace.Y + radius - 1)));
        Assert.Equal(4, WorkSites.PatchCount(site.Harvest));
    }
}
