using System;
using System.IO;
using System.Linq;
using SosariaAI.Configuration;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class SpawnerSeedsTests
{
    private const string TrollName = "Troll";
    private const string OrcName = "Orc";
    private const string OrcCaptainName = "OrcCaptain";
    private const int FirstX = 1425;
    private const int FirstY = 1695;
    private const int FirstZ = 0;
    private const int FirstHomeRange = 0;
    private const int SecondX = 1507;
    private const int SecondY = 1579;
    private const int SecondZ = 20;
    private const int SecondHomeRange = 5;
    private const int NestedX = 200;
    private const int NestedY = 300;
    private const int NestedZ = 10;
    private const int NestedHomeRange = 8;
    private const int ExpectedTwoSpawnerCount = 2;
    private const int ExpectedThreeSpawnerCount = 3;
    private const int ExpectedTwoCreatureCount = 2;

    private const string TwoSpawnersJson = """
        [
          {
            "$type": "Spawner",
            "location": [1425, 1695, 0],
            "map": "Felucca",
            "homeRange": 0,
            "entries": [ { "name": "Troll", "maxCount": 1, "probability": 100 } ]
          },
          {
            "$type": "Spawner",
            "location": [1507, 1579, 20],
            "map": "Trammel",
            "homeRange": 5,
            "entries": [
              { "name": "Orc", "maxCount": 1, "probability": 100 },
              { "name": "OrcCaptain", "maxCount": 1, "probability": 50 }
            ]
          }
        ]
        """;

    private const string EmptyAndNullNamesJson = """
        [
          null,
          {
            "$type": "Spawner",
            "location": [1425, 1695, 0],
            "map": "Felucca",
            "homeRange": 0,
            "entries": [
              { "name": "", "maxCount": 1, "probability": 100 },
              { "name": "   ", "maxCount": 1, "probability": 100 },
              null,
              { "name": "Troll", "maxCount": 1, "probability": 100 }
            ]
          }
        ]
        """;

    private const string NestedSpawnerJson = """
        [
          {
            "$type": "Spawner",
            "location": [200, 300, 10],
            "map": "Felucca",
            "homeRange": 8,
            "entries": [ { "name": "Orc", "maxCount": 1, "probability": 100 } ]
          }
        ]
        """;

    [Fact]
    public void Parse_TwoSpawners_ReadsLocationMapHomeRangeAndNames()
    {
        var seeds = SpawnerSeeds.Parse(TwoSpawnersJson);

        Assert.Equal(ExpectedTwoSpawnerCount, seeds.Count);

        Assert.Equal(FacetNames.Felucca, seeds[0].Map);
        Assert.Equal(FirstX, seeds[0].X);
        Assert.Equal(FirstY, seeds[0].Y);
        Assert.Equal(FirstZ, seeds[0].Z);
        Assert.Equal(FirstHomeRange, seeds[0].HomeRange);
        Assert.Equal(TrollName, Assert.Single(seeds[0].CreatureNames));

        Assert.Equal(FacetNames.Trammel, seeds[1].Map);
        Assert.Equal(SecondX, seeds[1].X);
        Assert.Equal(SecondY, seeds[1].Y);
        Assert.Equal(SecondZ, seeds[1].Z);
        Assert.Equal(SecondHomeRange, seeds[1].HomeRange);
        Assert.Equal(ExpectedTwoCreatureCount, seeds[1].CreatureNames.Count);
        Assert.Equal(OrcName, seeds[1].CreatureNames[0]);
        Assert.Equal(OrcCaptainName, seeds[1].CreatureNames[1]);
    }

    [Fact]
    public void Parse_IgnoresEmptyAndNullNames()
    {
        var seed = Assert.Single(SpawnerSeeds.Parse(EmptyAndNullNamesJson));

        Assert.Equal(FacetNames.Felucca, seed.Map);
        Assert.Equal(FirstX, seed.X);
        Assert.Equal(FirstY, seed.Y);
        Assert.Equal(FirstZ, seed.Z);
        Assert.Equal(FirstHomeRange, seed.HomeRange);
        Assert.Equal(TrollName, Assert.Single(seed.CreatureNames));
    }

    [Fact]
    public void LoadDirectory_MissingDirectory_ReturnsEmpty()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"sosariaai-spawners-missing-{Guid.NewGuid():N}");

        Assert.False(Directory.Exists(missing));
        Assert.Empty(SpawnerSeeds.LoadDirectory(missing));
        Assert.Empty(SpawnerSeeds.LoadDirectory(null));
        Assert.Empty(SpawnerSeeds.LoadDirectory(string.Empty));
    }

    [Fact]
    public void LoadDirectory_ConcatenatesNestedJsonFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sosariaai-spawners-{Guid.NewGuid():N}");
        var nested = Path.Combine(directory, "nested");

        try
        {
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(directory, "a.json"), TwoSpawnersJson);
            File.WriteAllText(Path.Combine(nested, "c.json"), NestedSpawnerJson);
            File.WriteAllText(Path.Combine(directory, "ignore.txt"), "not json");

            var seeds = SpawnerSeeds.LoadDirectory(directory);

            Assert.Equal(ExpectedThreeSpawnerCount, seeds.Count);
            Assert.Equal(TrollName, seeds[0].CreatureNames[0]);
            Assert.Equal(OrcCaptainName, seeds[1].CreatureNames[1]);
            Assert.Equal(OrcName, seeds[2].CreatureNames[0]);
            Assert.Equal(NestedX, seeds[2].X);
            Assert.Equal(NestedY, seeds[2].Y);
            Assert.Equal(NestedZ, seeds[2].Z);
            Assert.Equal(NestedHomeRange, seeds[2].HomeRange);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Banker", true)]
    [InlineData("Healer", true)]
    [InlineData("Blacksmith", true)]
    [InlineData("CustomHairstylist", true)]
    [InlineData("Bowyer", true)]
    [InlineData("Mapmaker", true)]
    [InlineData("Jeweler", true)]
    [InlineData("Tanner", true)]
    [InlineData("Armorer", true)]
    [InlineData("TavernKeeper", true)]
    [InlineData("Cartographer", true)]
    [InlineData("Fletcher", true)]
    [InlineData("TownHealer", true)]
    [InlineData("Troll", false)]
    [InlineData("Orc", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsVendorName_MatchesVendorTokens(string name, bool expected) =>
        Assert.Equal(expected, SpawnerSeeds.IsVendorName(name));

    [Fact]
    public void Parse_NullJson_ReturnsEmpty()
    {
        Assert.Empty(SpawnerSeeds.Parse(null));
        Assert.Empty(SpawnerSeeds.Parse(string.Empty));
        Assert.Empty(SpawnerSeeds.Parse("[]"));
    }

    [Fact]
    public void Parse_KeepsTheCountAndEachEntrysCapAndWeight()
    {
        var seed = Assert.Single(SpawnerSeeds.Parse(DestardDragonsJson));

        Assert.Equal(DestardCount, seed.Count);
        Assert.Equal(new[] { new SpawnEntry("Dragon", 1, 100), new SpawnEntry("Drake", 2, 100) }, seed.Entries);
    }

    [Fact]
    public void Standing_FillsTheCountByWeight_AndACappedEntryPassesItsShareOn()
    {
        // Three at once, one dragon at most and two drakes: exactly one and two stand.
        var standing = SpawnerSeeds.Standing(Assert.Single(SpawnerSeeds.Parse(DestardDragonsJson)));

        Assert.Equal(new[] { new SpawnCount("Dragon", 1), new SpawnCount("Drake", 2) }, standing);
    }

    [Fact]
    public void Standing_AMixedSpawnerSharesItsFewAmongMany()
    {
        var mixed = new SpawnerSeed(
            "Felucca", 0, 0, 0, 0, ["Orc", "Troll", "Ettin", "Ogre"], MixedCount,
            [new SpawnEntry("Orc", MixedCount, 100), new SpawnEntry("Troll", MixedCount, 100), new SpawnEntry("Ettin", MixedCount, 100), new SpawnEntry("Ogre", MixedCount, 300)]
        );

        var standing = SpawnerSeeds.Standing(mixed);

        Assert.Equal(MixedCount, standing.Sum(entry => entry.Count), 6);
        Assert.Equal(MixedCount * 0.5, standing.Single(entry => entry.Creature == "Ogre").Count, 6);
        Assert.Equal(MixedCount / 6.0, standing.Single(entry => entry.Creature == "Orc").Count, 6);
    }

    [Fact]
    public void Standing_NoCountKeepsOneOfEach_AndNoEntriesStandNothing()
    {
        var bare = new SpawnerSeed("Felucca", 0, 0, 0, 0, ["Orc"], 0, [new SpawnEntry("Orc", 0, 0)]);

        Assert.Equal(new[] { new SpawnCount("Orc", SpawnerSeeds.DefaultCount) }, SpawnerSeeds.Standing(bare));
        Assert.Empty(SpawnerSeeds.Standing(new SpawnerSeed("Felucca", 0, 0, 0, 0, [])));
    }

    private const int DestardCount = 3;
    private const int MixedCount = 6;

    private const string DestardDragonsJson = """
        [
          {
            "$type": "Spawner",
            "location": [5247, 956, -40],
            "map": "Felucca",
            "count": 3,
            "homeRange": 30,
            "entries": [
              { "name": "Dragon", "maxCount": 1, "probability": 100 },
              { "name": "Drake", "maxCount": 2, "probability": 100 }
            ]
          }
        ]
        """;
}
