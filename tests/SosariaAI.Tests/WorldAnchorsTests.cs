using System;
using System.Collections.Generic;
using Server;
using Server.Guilds;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// The places a facet offers: Britannia's tables on a stock map, the map's own catalog on any
/// other. The catalogs here are small made-up maps.
/// </summary>
public class WorldAnchorsTests
{
    private const string CustomFacet = "Sosaria";
    private const int WorkReach = 400;
    private const int FarAway = 2000;

    private static readonly Point3D WestBank = new(100, 100, 0);
    private static readonly Point3D EastBank = new(900, 100, 0);
    private static readonly Point3D SouthBank = new(100, 600, 0);
    private static readonly Point3D WestWood = new(140, 120, 0);
    private static readonly Point3D LostRock = new(100 + FarAway, 100, 0);
    private static readonly Point3D Entrance = new(700, 700, 0);
    private static readonly Point3D Gate = new(120, 90, 0);

    [Fact]
    public void AStockFacet_TakesTheBritanniaTables()
    {
        Assert.Same(WorldAnchors.Stock, WorldAnchors.For(FacetNames.Felucca));
        Assert.Same(WorldAnchors.Stock, WorldAnchors.ForId("Felucca:mira#2"));
        Assert.True(WorldAnchors.Stock.IsStock);
        Assert.Equal(WorkSites.TownSites, WorldAnchors.Stock.Towns);
        Assert.Equal(WorkSites.PublicGates, WorldAnchors.Stock.PublicGates);
        Assert.Equal(WorkSites.MineSites, WorldAnchors.Stock.SitesOf(SkillKinds.Mine));
        Assert.Equal(CraftCareerRules.HomesFor(SkillKinds.Smith), WorldAnchors.Stock.SitesOf(SkillKinds.Smith));
    }

    [Fact]
    public void ACustomMapBeforeItsCatalog_HasNoPlace_AndHomesNobodyInBritannia()
    {
        // The test land has no nav catalog: nobody is sent to a Britannia spot on it.
        var land = TestMap.EnsureLand();
        var anchors = WorldAnchors.For(land.Name);
        var fixture = FacetIds.Prefix(land.Name, "connor");

        Assert.False(anchors.IsStock);
        Assert.Empty(anchors.Towns);
        Assert.Empty(anchors.PublicGates);
        Assert.True(WorkSites.HomedByTable(fixture));
        Assert.False(WorkSites.HomedByTable("Felucca:connor"));
        Assert.Equal(CharactersFile.DefaultSpawn, WorkSites.HomeFor(CharactersFile.DefaultSpawn, fixture, SkillKinds.Lumberjack));
    }

    [Fact]
    public void FromCatalog_EachBankIsATown_AndEachTownWorksItsNearestPlace()
    {
        var anchors = WorldAnchors.FromCatalog(Catalog(), [Gate], haven: null, WorkReach);

        Assert.False(anchors.IsStock);
        Assert.Equal(new[] { WestBank, EastBank, SouthBank }, Homes(anchors.Towns));
        Assert.Equal(new[] { Gate }, anchors.PublicGates);

        var wood = Assert.Single(anchors.SitesOf(SkillKinds.Lumberjack));
        Assert.Equal(WestBank, wood.Home);
        Assert.True(wood.Harvest.Contains(WestWood));

        // The rock lies past the reach of every town: no mine site, and a miner lives in a town.
        Assert.Empty(anchors.SitesOf(SkillKinds.Mine));
        Assert.Equal(anchors.Towns, anchors.HomeSites(SkillKinds.Mine));
        Assert.Equal(anchors.Towns, anchors.SitesOf(SkillKinds.Smith));
        Assert.Equal(anchors.Towns, anchors.HomeSites(SkillKinds.IdleWander));
        Assert.Equal(anchors.Towns, anchors.SitesOf(SkillKinds.Hunt));
    }

    [Fact]
    public void FromCatalog_TheTownsTakeOrderAndChaosInTurn()
    {
        var anchors = WorldAnchors.FromCatalog(Catalog(), [], haven: null, WorkReach);

        Assert.Equal(GuildType.Order, SideHomes.SideOf(anchors, WestBank));
        Assert.Equal(GuildType.Chaos, SideHomes.SideOf(anchors, EastBank));
        Assert.Equal(GuildType.Order, SideHomes.SideOf(anchors, SouthBank));
        Assert.All(SideHomes.For(anchors, GuildType.Chaos, anchors.Towns), town => Assert.Equal(EastBank, town.Home));
    }

    [Fact]
    public void FromCatalog_OneBank_HoldsNoSide_AndHomesBothSides()
    {
        var anchors = WorldAnchors.FromCatalog(new DestinationCatalog([Bank("Lone", WestBank)]), [], haven: null, WorkReach);

        Assert.Empty(anchors.TownSides);
        Assert.Equal(GuildType.Regular, SideHomes.SideOf(anchors, WestBank));
        Assert.Equal(anchors.Towns, SideHomes.For(anchors, GuildType.Order, anchors.Towns));
        Assert.Equal(anchors.Towns, SideHomes.For(anchors, GuildType.Chaos, anchors.Towns));
    }

    [Fact]
    public void FromCatalog_TheHavensBankIsNoTown_UnlessItIsTheOnlyOne()
    {
        var haven = new RedHaven(CustomFacet, [new Rectangle2D(SouthBank.X - 1, SouthBank.Y - 1, 3, 3)], SouthBank);
        var lone = new DestinationCatalog([Bank("Lone", SouthBank)]);

        Assert.Equal(new[] { WestBank, EastBank }, Homes(WorldAnchors.FromCatalog(Catalog(), [], haven, WorkReach).Towns));
        Assert.Equal(new[] { SouthBank }, Homes(WorldAnchors.FromCatalog(lone, [], haven, WorkReach).Towns));
    }

    [Fact]
    public void ACraftersRoutine_OnAMapWithNoRock_BuysItsStock()
    {
        var anchors = WorldAnchors.FromCatalog(Catalog(), [], haven: null, WorkReach);
        var steps = CraftCareerRules.CraftRoutine(SkillKinds.Smith, WestBank, anchors);

        Assert.Null(WorkSites.NearestSite(anchors, SkillKinds.Mine, WestBank));
        Assert.DoesNotContain(steps, step => step.Skill == SkillKinds.Mine);
        Assert.Contains(steps, step => step.Skill == SkillKinds.Smith);
    }

    [Fact]
    public void PickHaven_TheUnguardedBankFarthestFromTheOthers()
    {
        // The east bank lies 800 tiles from its nearest bank; the west and south banks 500.
        var haven = WorldAnchors.PickHaven(CustomFacet, Catalog(), static _ => false);

        Assert.NotNull(haven);
        Assert.Equal(CustomFacet, haven.Facet);
        Assert.Equal(EastBank, haven.Home);
        Assert.True(haven.Contains(EastBank.X + WorldAnchors.HavenRadius, EastBank.Y - WorldAnchors.HavenRadius));
        Assert.False(haven.Contains(EastBank.X + WorldAnchors.HavenRadius + 1, EastBank.Y));
    }

    [Fact]
    public void PickHaven_PassesOverAGuardedBank()
    {
        var haven = WorldAnchors.PickHaven(CustomFacet, Catalog(), at => at == EastBank);

        Assert.NotEqual(EastBank, haven.Home);
        Assert.Contains(haven.Home, new[] { WestBank, SouthBank });
    }

    [Fact]
    public void PickHaven_EveryBankGuarded_TakesTheUnguardedEntrance()
    {
        var haven = WorldAnchors.PickHaven(CustomFacet, Catalog(), at => at != Entrance);

        Assert.Equal(Entrance, haven.Home);
    }

    [Fact]
    public void PickHaven_OneBankAndNoEntrance_TakesThatBank_AndNoPlaceTakesNone()
    {
        var lone = new DestinationCatalog([Bank("Lone", WestBank)]);

        Assert.Equal(WestBank, WorldAnchors.PickHaven(CustomFacet, lone, static _ => true).Home);
        Assert.Null(WorldAnchors.PickHaven(CustomFacet, new DestinationCatalog([]), static _ => false));
        Assert.Null(WorldAnchors.PickHaven(CustomFacet, null, static _ => false));
    }

    [Fact]
    public void HavenFor_NoMapWithReds_IsBuccaneersDen()
    {
        var land = TestMap.EnsureLand();
        var closed = Config(land.Name, new MapToggle { Enabled = true, Count = 10, OpenPvp = false });
        var quiet = Config(land.Name, new MapToggle { Enabled = true, Count = 10, PkEnabled = false });

        Assert.Same(PkRules.BucsDen, WorldAnchors.HavenFor(null));
        Assert.Same(PkRules.BucsDen, WorldAnchors.HavenFor(closed));
        Assert.Same(PkRules.BucsDen, WorldAnchors.HavenFor(quiet));
    }

    [Fact]
    public void HavenFor_ARectangleInTheFile_IsTheHaven()
    {
        var land = TestMap.EnsureLand();
        var box = new AreaDefinition { X = 10, Y = 20, Width = 30, Height = 40 };
        var config = Config(land.Name, new MapToggle { Enabled = true, Count = 10, RedHaven = box });

        var haven = WorldAnchors.HavenFor(config);

        Assert.Equal(land.Name, haven.Facet);
        Assert.Equal(new Point3D(25, 40, 0), haven.Home);
        Assert.True(haven.Contains(10, 20));
        Assert.False(haven.Contains(40, 20));
    }

    private static DestinationCatalog Catalog() =>
        new(
        [
            Bank("West bank", WestBank),
            Bank("East bank", EastBank),
            Bank("South bank", SouthBank),
            Place("West wood", nameof(DestinationKind.Resource), ResourceKind.Lumber, WestWood),
            Place("Lost rock", nameof(DestinationKind.Resource), ResourceKind.Mine, LostRock),
            Place("Old Caves Entrance", nameof(DestinationKind.Dungeon), "Old Caves", Entrance)
        ]);

    private static Destination Bank(string name, Point3D at) => Place(name, nameof(DestinationKind.Bank), null, at);

    private static Destination Place(string name, string kind, string role, Point3D at) =>
        new() { Name = name, Kind = kind, Role = role, X = at.X, Y = at.Y, Z = at.Z };

    private static List<Point3D> Homes(IReadOnlyList<WorkSite> sites)
    {
        var homes = new List<Point3D>();

        for (var i = 0; i < sites.Count; i++)
        {
            homes.Add(sites[i].Home);
        }

        return homes;
    }

    private static CharactersConfiguration Config(string facet, MapToggle toggle) =>
        new() { Maps = new(StringComparer.OrdinalIgnoreCase) { [facet] = toggle } };
}
