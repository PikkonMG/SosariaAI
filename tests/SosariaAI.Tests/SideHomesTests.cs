using System.Collections.Generic;
using Server;
using Server.Guilds;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Social;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class SideHomesTests
{
    private const int Copies = 400;
    private const int TownFolk = 2000;

    /// <summary>Britain holds under one town person in five (two shares of thirteen).</summary>
    private const int BritainShareLimit = 5;

    /// <summary>Britain and Minoc weigh the same; the id hash may tip one a little.</summary>
    private const double TownShareSlack = 1.3;

    private static readonly int ChaosGuild = System.Array.FindIndex(
        GuildCatalog.All,
        record => record.Alignment == GuildType.Chaos
    );

    private static string CopyId(int i) => $"Felucca:warrior#{i}";

    [Fact]
    public void OrderAndChaos_NeverShareAHomeTown()
    {
        var order = new HashSet<Point3D>();
        var chaos = new HashSet<Point3D>();

        for (var i = 0; i < Copies; i++)
        {
            order.Add(WorkSites.HomeFor(CharactersFile.DefaultSpawn, CopyId(i), SkillKinds.Hunt, GuildType.Order));
            chaos.Add(WorkSites.HomeFor(CharactersFile.DefaultSpawn, CopyId(i), SkillKinds.Hunt, GuildType.Chaos));
        }

        Assert.True(order.Count > 1);
        Assert.True(chaos.Count > 1);
        Assert.False(order.Overlaps(chaos));
    }

    [Fact]
    public void BothSides_TogetherStillCoverEveryTown()
    {
        var homes = new HashSet<Point3D>();

        for (var i = 0; i < Copies; i++)
        {
            homes.Add(WorkSites.HomeFor(CharactersFile.DefaultSpawn, CopyId(i), SkillKinds.Hunt, GuildType.Order));
            homes.Add(WorkSites.HomeFor(CharactersFile.DefaultSpawn, CopyId(i), SkillKinds.Hunt, GuildType.Chaos));
        }

        var towns = new HashSet<Point3D>();

        for (var i = 0; i < WorkSites.TownSites.Length; i++)
        {
            towns.Add(WorkSites.TownSites[i].Home);
        }

        Assert.True(homes.SetEquals(towns));
    }

    [Fact]
    public void ARegularPerson_KeepsTheHomeItHadBefore()
    {
        for (var i = 0; i < Copies; i++)
        {
            Assert.Equal(
                WorkSites.HomeFor(CharactersFile.DefaultSpawn, CopyId(i), SkillKinds.Hunt),
                WorkSites.HomeFor(CharactersFile.DefaultSpawn, CopyId(i), SkillKinds.Hunt, GuildType.Regular)
            );
        }
    }

    [Fact]
    public void AFixture_KeepsItsAuthoredSpawnOnEitherSide()
    {
        Assert.Equal(
            CharactersFile.DefaultSpawn,
            WorkSites.HomeFor(CharactersFile.DefaultSpawn, "Felucca:connor", SkillKinds.Hunt, GuildType.Chaos)
        );
    }

    [Fact]
    public void AListWithNoTownOfTheSide_GivesThatSidesBankTowns()
    {
        var britainOnly = new[] { WorkSites.TownSites[0], WorkSites.TownSites[0] };

        Assert.Equal(britainOnly, SideHomes.For(GuildType.Order, britainOnly));
        Assert.All(
            SideHomes.For(GuildType.Chaos, britainOnly),
            site => Assert.Equal(GuildType.Chaos, SideHomes.SideOf(site.Home))
        );
    }

    [Theory]
    [InlineData(SkillKinds.Hunt)]
    [InlineData(SkillKinds.IdleWander)]
    [InlineData(SkillKinds.Lumberjack)]
    [InlineData(SkillKinds.Mine)]
    [InlineData(SkillKinds.Fish)]
    [InlineData(SkillKinds.Smith)]
    [InlineData(SkillKinds.Tailor)]
    [InlineData(SkillKinds.Carpentry)]
    [InlineData(SkillKinds.Fletch)]
    [InlineData(SkillKinds.Alchemy)]
    [InlineData(SkillKinds.Inscription)]
    [InlineData(SkillKinds.Tinker)]
    public void OrderAndChaos_NeverShareATown_AcrossEveryWork(string ownWork)
    {
        // Each work list once split its own towns by list order: Minoc was Order for hunters
        // and Chaos for town folk, so both sides met at the Minoc bank on a fresh world.
        var chaos = ChaosHomesOfEveryWork();

        for (var i = 0; i < Copies; i++)
        {
            var order = WorkSites.HomeFor(CharactersFile.DefaultSpawn, CopyId(i), ownWork, GuildType.Order);

            Assert.DoesNotContain(chaos, home => NavMetric.Chebyshev(order, home) <= SideHomes.TownReach);
        }
    }

    [Fact]
    public void AFixtureInAnOrderTown_NeverRollsChaos_AndItsBindAgrees()
    {
        // Bran, a fixture at the Britain bank, rolled Chaos and drew on the Order crowd in the
        // first second of a fresh world.
        var chaosSeen = false;

        for (var i = 0; i < Copies; i++)
        {
            var id = $"Felucca:fixture{i}";
            var homeSide = SideHomes.HomeSide(id, CharactersFile.BranSpawn);
            var settled = GuildCatalog.Settle(id, thief: false, fighter: true, GuildCatalog.None, homeSide: homeSide);

            Assert.Equal(GuildType.Order, homeSide);
            Assert.NotEqual(GuildType.Chaos, GuildCatalog.AlignmentOf(settled));
            Assert.Equal(GuildCatalog.AlignmentOf(settled), GuildCatalog.SideAtSpawn(id, thief: false, fighter: true, homeSide));
            chaosSeen |= GuildCatalog.SideAtSpawn(id, thief: false, fighter: true, GuildType.Regular) == GuildType.Chaos;
        }

        Assert.True(chaosSeen);
    }

    [Fact]
    public void ASavedChaosFixtureInAnOrderTown_LeavesChaosOnItsNextBind()
    {
        var id = FindChaosFixture();

        var settled = GuildCatalog.Settle(
            id,
            thief: false,
            fighter: true,
            ChaosGuild,
            homeSide: SideHomes.HomeSide(id, CharactersFile.BranSpawn)
        );

        Assert.NotEqual(GuildType.Chaos, GuildCatalog.AlignmentOf(settled));
    }

    [Fact]
    public void AHomeInNoTown_HoldsNoSide()
    {
        Assert.Equal(GuildType.Regular, SideHomes.SideOf(PkRules.BucsDenHaven));
        Assert.True(SideHomes.Allows(GuildType.Order, PkRules.BucsDenHaven));
        Assert.True(SideHomes.Allows(GuildType.Chaos, PkRules.BucsDenHaven));
        Assert.Equal(GuildType.Regular, SideHomes.HomeSide("Felucca:bran#3", CharactersFile.BranSpawn));
    }

    [Fact]
    public void ATownHoldsOneSide()
    {
        Assert.True(SideHomes.Allows(GuildType.Order, WorkSites.BritainTown));
        Assert.False(SideHomes.Allows(GuildType.Chaos, WorkSites.BritainTown));
        Assert.True(SideHomes.Allows(GuildType.Chaos, WorkSites.YewTown));
        Assert.False(SideHomes.Allows(GuildType.Order, WorkSites.YewTown));
        Assert.True(SideHomes.Allows(GuildType.Regular, WorkSites.YewTown));
    }

    [Fact]
    public void BritainHoldsNoMoreTownFolkThanTheOtherBigTowns()
    {
        // At four shares of fifteen Britain held 147 of 800 people on a fresh world.
        var britain = 0;
        var minoc = 0;

        for (var i = 0; i < TownFolk; i++)
        {
            var home = WorkSites.HomeFor(CharactersFile.DefaultSpawn, CopyId(i), SkillKinds.IdleWander);
            britain += home == WorkSites.BritainTown ? 1 : 0;
            minoc += home == WorkSites.MinocTown ? 1 : 0;
        }

        Assert.True(britain * BritainShareLimit <= TownFolk, $"Britain held {britain} of {TownFolk}");
        Assert.True(britain <= minoc * TownShareSlack, $"Britain {britain}, Minoc {minoc}");
    }

    [Fact]
    public void SideAtSpawn_IsTheSideTheFirstBindGives()
    {
        for (var i = 0; i < Copies; i++)
        {
            var id = CopyId(i);
            var settled = GuildCatalog.Settle(id, thief: false, fighter: true, GuildCatalog.None);

            Assert.Equal(
                GuildCatalog.AlignmentOf(settled),
                GuildCatalog.SideAtSpawn(id, thief: false, fighter: true, GuildType.Regular)
            );
        }
    }

    private static List<Point3D> ChaosHomesOfEveryWork()
    {
        string[] works =
        [
            SkillKinds.Hunt, SkillKinds.IdleWander, SkillKinds.Lumberjack, SkillKinds.Mine, SkillKinds.Fish,
            SkillKinds.Smith, SkillKinds.Tailor, SkillKinds.Carpentry, SkillKinds.Fletch, SkillKinds.Alchemy,
            SkillKinds.Inscription, SkillKinds.Tinker
        ];
        var homes = new List<Point3D>();

        foreach (var work in works)
        {
            for (var i = 0; i < Copies; i++)
            {
                homes.Add(WorkSites.HomeFor(CharactersFile.DefaultSpawn, CopyId(i), work, GuildType.Chaos));
            }
        }

        return homes;
    }

    private static string FindChaosFixture()
    {
        for (var i = 0; i < Copies; i++)
        {
            var id = $"Felucca:fixture{i}";

            if (GuildCatalog.SideAtSpawn(id, thief: false, fighter: true, GuildType.Regular) == GuildType.Chaos)
            {
                return id;
            }
        }

        throw new Xunit.Sdk.XunitException("no fixture id rolls Chaos");
    }
}
