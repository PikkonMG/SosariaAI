using System.Collections.Generic;
using Server;
using Server.Guilds;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using SosariaAI.Social;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class SideHomesTests
{
    private const int Copies = 400;

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
    public void AListWithOneTown_IsNotSplit()
    {
        var oneTown = new[] { WorkSites.TownSites[0], WorkSites.TownSites[0] };

        Assert.Equal(oneTown, SideHomes.For(GuildType.Order, oneTown));
        Assert.Equal(oneTown, SideHomes.For(GuildType.Chaos, oneTown));
    }

    [Fact]
    public void SideAtSpawn_IsTheSideTheFirstBindGives()
    {
        for (var i = 0; i < Copies; i++)
        {
            var id = CopyId(i);
            var settled = GuildCatalog.Settle(id, thief: false, fighter: true, GuildCatalog.None);

            Assert.Equal(GuildCatalog.AlignmentOf(settled), GuildCatalog.SideAtSpawn(id, thief: false, fighter: true));
        }
    }
}
