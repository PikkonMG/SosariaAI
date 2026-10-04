using System.Linq;
using SosariaAI.Admin;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class WorldSetupRulesTests
{
    [Fact]
    public void Steps_BuildTheWorldBeforeTheSpawners()
    {
        var steps = WorldSetupRules.Steps(EraBand.T2A, ["Felucca"], _ => true);
        var commands = steps.Select(step => step.Command).ToList();

        Assert.Equal(
            [
                WorldSetupRules.Decorate, WorldSetupRules.DoorGen, WorldSetupRules.SignGen,
                WorldSetupRules.TelGen, WorldSetupRules.MoonGen
            ],
            commands.Take(5)
        );
        Assert.Contains(new SetupStep(WorldSetupRules.ImportSpawners, "Data/Spawns/shared/felucca/*.json"), steps);
    }

    [Fact]
    public void Steps_SkipSpawnFoldersThatDoNotExist()
    {
        var steps = WorldSetupRules.Steps(EraBand.ML, ["Felucca", "Trammel"], folder => folder.EndsWith("/felucca"));

        Assert.Equal(WorldSetupRules.MLSpawnSets.Length, steps.Count(step => step.Command == WorldSetupRules.ImportSpawners));
        Assert.DoesNotContain(steps, step => step.Arguments.Contains("trammel"));
    }

    [Theory]
    [InlineData(EraBand.T2A, new[] { "shared" })]
    [InlineData(EraBand.ML, new[] { "shared", "uoml" })]
    [InlineData(EraBand.Modern, new[] { "shared", "post-uoml" })]
    public void SpawnSets_FollowTheEra(EraBand band, string[] sets)
    {
        var steps = WorldSetupRules.Steps(band, ["Trammel"], _ => true);
        var imports = steps.Where(step => step.Command == WorldSetupRules.ImportSpawners).Select(step => step.Arguments);

        Assert.Equal(sets, WorldSetupRules.SpawnSets(band));
        Assert.Equal(sets.Select(set => $"Data/Spawns/{set}/trammel/*.json"), imports);
    }

    [Fact]
    public void SecondAge_ImportsNoLaterSet()
    {
        var steps = WorldSetupRules.Steps(EraBand.T2A, ["Felucca", "Trammel"], _ => true);

        Assert.DoesNotContain(steps, step => step.Arguments.Contains(WorldSetupRules.UomlSpawnSet + "/"));
        Assert.DoesNotContain(steps, step => step.Arguments.Contains(WorldSetupRules.PostUomlSpawnSet));
    }

    [Fact]
    public void AwaitsSetup_OnlyABareWorld()
    {
        Assert.True(WorldSetupRules.AwaitsSetup(worldHasSpawners: false, worldHasBankers: false));
        Assert.False(WorldSetupRules.AwaitsSetup(worldHasSpawners: true, worldHasBankers: false));
        Assert.False(WorldSetupRules.AwaitsSetup(worldHasSpawners: false, worldHasBankers: true));
    }

    [Fact]
    public void Steps_BuildChampionSpawnsFromAgeOfShadowsOn()
    {
        Assert.DoesNotContain(
            WorldSetupRules.Steps(EraBand.T2A, ["Felucca"], _ => true),
            step => step.Command == WorldSetupRules.GenChamps
        );

        foreach (var band in new[] { EraBand.ML, EraBand.Modern })
        {
            var steps = WorldSetupRules.Steps(band, ["Felucca"], _ => true);
            Assert.Contains(steps, step => step.Command == WorldSetupRules.GenChamps);
        }
    }
}
