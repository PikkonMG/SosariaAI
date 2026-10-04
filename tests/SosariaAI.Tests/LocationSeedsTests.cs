using System;
using System.IO;
using SosariaAI.Configuration;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class LocationSeedsTests
{
    private const string NestedLocationsJson =
        """
        {
          "name": "Felucca",
          "categories": [
            {
              "name": "Dungeons",
              "categories": [
                {
                  "name": "Covetous",
                  "locations": [
                    { "name": "Entrance", "location": [2499, 919, 0] }
                  ]
                }
              ],
              "locations": [
                { "name": "Surface Camp", "location": [100, 200, 5] }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Parse_NestedCategories_JoinsGroupPathAndUsesRootFacet()
    {
        var seeds = LocationSeeds.Parse(NestedLocationsJson);

        Assert.Equal(2, seeds.Count);
        Assert.Contains(
            new NamedSeed("Entrance", "Dungeons/Covetous", FacetNames.Felucca, 2499, 919, 0),
            seeds
        );
        Assert.Contains(
            new NamedSeed("Surface Camp", "Dungeons", FacetNames.Felucca, 100, 200, 5),
            seeds
        );
    }

    [Fact]
    public void Parse_EmptyJson_ReturnsEmpty()
    {
        Assert.Empty(LocationSeeds.Parse(null));
        Assert.Empty(LocationSeeds.Parse(""));
        Assert.Empty(LocationSeeds.Parse("   "));
        Assert.Empty(LocationSeeds.Parse("{}"));
    }

    [Fact]
    public void LoadFile_MissingFile_ReturnsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sosariaai-missing-{Guid.NewGuid():N}.json");

        Assert.False(File.Exists(path));
        Assert.Empty(LocationSeeds.LoadFile(path));
        Assert.Empty(LocationSeeds.LoadFile(null));
    }

    [Fact]
    public void LoadFile_ExistingFile_ParsesSeeds()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sosariaai-locations-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, NestedLocationsJson);
            var seeds = LocationSeeds.LoadFile(path);

            Assert.Equal(LocationSeeds.Parse(NestedLocationsJson), seeds);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
