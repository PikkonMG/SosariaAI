using SosariaAI.Configuration;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class WorldDataSeedsTests
{
    [Fact]
    public void ParseTeleporters_BackTrue_KeepsOneRecord()
    {
        const string json = """
            [
              {
                "src": { "map": "Felucca", "loc": [512, 1559, 0] },
                "dst": { "map": "Trammel", "loc": [5394, 127, 5] },
                "back": true
              },
              {
                "src": { "map": "Felucca", "loc": [311, 786, -24] },
                "dst": { "map": "Felucca", "loc": [314, 784, 0] },
                "back": false
              }
            ]
            """;

        var links = WorldDataSeeds.ParseTeleporters(json);

        Assert.Equal(2, links.Count);

        Assert.Equal(FacetNames.Felucca, links[0].SrcMap);
        Assert.Equal(512, links[0].Sx);
        Assert.Equal(1559, links[0].Sy);
        Assert.Equal(0, links[0].Sz);
        Assert.Equal(FacetNames.Trammel, links[0].DstMap);
        Assert.Equal(5394, links[0].Dx);
        Assert.Equal(127, links[0].Dy);
        Assert.Equal(5, links[0].Dz);
        Assert.True(links[0].Back);

        Assert.Equal(FacetNames.Felucca, links[1].SrcMap);
        Assert.Equal(311, links[1].Sx);
        Assert.Equal(786, links[1].Sy);
        Assert.Equal(-24, links[1].Sz);
        Assert.Equal(FacetNames.Felucca, links[1].DstMap);
        Assert.Equal(314, links[1].Dx);
        Assert.Equal(784, links[1].Dy);
        Assert.Equal(0, links[1].Dz);
        Assert.False(links[1].Back);
    }

    [Fact]
    public void ParseRegions_UsesGoLocationWhenPresent()
    {
        const string json = """
            [
              {
                "$type": "TownRegion",
                "Map": "Felucca",
                "Name": "Britain",
                "Area": [{"x1": 0, "y1": 0, "x2": 100, "y2": 80}],
                "Entrance": {"x": 7, "y": 8, "z": 9},
                "GoLocation": {"x": 1495, "y": 1629, "z": 10}
              }
            ]
            """;

        var seeds = WorldDataSeeds.ParseRegions(json);

        Assert.Single(seeds);
        Assert.Equal(WorldDataSeeds.TownRegionToken, seeds[0].Type);
        Assert.Equal(FacetNames.Felucca, seeds[0].Map);
        Assert.Equal("Britain", seeds[0].Name);
        Assert.Equal(1495, seeds[0].X);
        Assert.Equal(1629, seeds[0].Y);
        Assert.Equal(10, seeds[0].Z);
        Assert.True(WorldDataSeeds.IsTown(seeds[0].Type));
        Assert.False(WorldDataSeeds.IsDungeon(seeds[0].Type));
    }

    [Fact]
    public void ParseRegions_UsesAreaCentreWhenGoLocationAndEntranceAreMissing()
    {
        const int x1 = 1330;
        const int y1 = 1991;
        const int x2 = 1343;
        const int y2 = 2004;

        const string json = """
            [
              {
                "$type": "GuardedRegion",
                "Map": "Felucca",
                "Name": "Moongates",
                "Area": [{"x1": 1330, "y1": 1991, "x2": 1343, "y2": 2004}]
              }
            ]
            """;

        var seeds = WorldDataSeeds.ParseRegions(json);

        Assert.Single(seeds);
        Assert.Equal("GuardedRegion", seeds[0].Type);
        Assert.Equal(FacetNames.Felucca, seeds[0].Map);
        Assert.Equal("Moongates", seeds[0].Name);
        Assert.Equal((x1 + x2) / 2, seeds[0].X);
        Assert.Equal((y1 + y2) / 2, seeds[0].Y);
        Assert.Equal(WorldDataSeeds.AreaCentreZ, seeds[0].Z);
        Assert.False(WorldDataSeeds.IsTown(seeds[0].Type));
        Assert.False(WorldDataSeeds.IsDungeon(seeds[0].Type));
    }

    [Fact]
    public void ParseRegions_UsesEntranceWhenGoLocationIsMissing()
    {
        const string json = """
            [
              {
                "$type": "DungeonRegion",
                "Map": "Felucca",
                "Name": "Sanctuary",
                "Area": [{"x1": 6144, "y1": 0, "x2": 6399, "y2": 255}],
                "Entrance": {"x": 762, "y": 1645, "z": 0}
              }
            ]
            """;

        var seeds = WorldDataSeeds.ParseRegions(json);

        Assert.Single(seeds);
        Assert.Equal(WorldDataSeeds.DungeonRegionToken, seeds[0].Type);
        Assert.Equal(FacetNames.Felucca, seeds[0].Map);
        Assert.Equal("Sanctuary", seeds[0].Name);
        Assert.Equal(762, seeds[0].X);
        Assert.Equal(1645, seeds[0].Y);
        Assert.Equal(0, seeds[0].Z);
        Assert.True(WorldDataSeeds.IsDungeon(seeds[0].Type));
        Assert.False(WorldDataSeeds.IsTown(seeds[0].Type));
    }

    [Fact]
    public void IsTown_WhenTypeContainsTownRegion()
    {
        Assert.True(WorldDataSeeds.IsTown("TownRegion"));
        Assert.True(WorldDataSeeds.IsTown("Server.Regions.TownRegion"));
        Assert.False(WorldDataSeeds.IsTown("DungeonRegion"));
        Assert.False(WorldDataSeeds.IsTown("GuardedRegion"));
        Assert.False(WorldDataSeeds.IsTown(null));
        Assert.False(WorldDataSeeds.IsTown(string.Empty));
    }

    [Fact]
    public void IsDungeon_WhenTypeContainsDungeonRegion()
    {
        Assert.True(WorldDataSeeds.IsDungeon("DungeonRegion"));
        Assert.True(WorldDataSeeds.IsDungeon("Server.Regions.DungeonRegion"));
        Assert.False(WorldDataSeeds.IsDungeon("TownRegion"));
        Assert.False(WorldDataSeeds.IsDungeon("GuardedRegion"));
        Assert.False(WorldDataSeeds.IsDungeon(null));
        Assert.False(WorldDataSeeds.IsDungeon(string.Empty));
    }

    [Fact]
    public void OnFacet_MatchesTheFacetAnyCase_AndNoMapIsOnNone()
    {
        Assert.True(WorldDataSeeds.OnFacet("felucca", FacetNames.Felucca));
        Assert.False(WorldDataSeeds.OnFacet(FacetNames.Trammel, FacetNames.Felucca));
        Assert.False(WorldDataSeeds.OnFacet(null, FacetNames.Felucca));
        Assert.False(WorldDataSeeds.OnFacet(string.Empty, FacetNames.Felucca));
    }
}
