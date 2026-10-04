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
        var sites = WorkSites.HarvestSites(SkillKinds.Lumberjack, BritainCopy, Authored, ownStock: false, BritainBank, Leash);

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
        Assert.Single(WorkSites.HarvestSites(SkillKinds.Lumberjack, Fixture, Authored, ownStock: false, BritainBank, Leash));
        Assert.Single(WorkSites.HarvestSites(SkillKinds.Lumberjack, BritainCopy, Authored, ownStock: true, BritainBank, Leash));
    }
}
