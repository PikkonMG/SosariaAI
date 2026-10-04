using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class SightseeRulesTests
{
    private const string BritainBank = "Britain West Bank";
    private const string MinocBank = "Minoc Bank";
    private const string Despise = "Despise";
    private const int FirstSeed = 0;
    private const int SecondSeed = 1;

    [Fact]
    public void IsPlaceOfInterest_ShrineDungeonHuntBank()
    {
        Assert.True(LeisureRules.IsPlaceOfInterest(SightseeRules.KindShrine));
        Assert.False(LeisureRules.IsPlaceOfInterest(SightseeRules.KindDungeon));
        Assert.False(LeisureRules.IsPlaceOfInterest(SightseeRules.KindHunt));
        Assert.True(LeisureRules.IsPlaceOfInterest(SightseeRules.KindBank));
        Assert.True(LeisureRules.IsPlaceOfInterest(SightseeRules.KindTown));
        Assert.False(LeisureRules.IsPlaceOfInterest("Vendor"));
        Assert.False(LeisureRules.IsPlaceOfInterest("Resource"));
    }

    [Fact]
    public void PickUnseen_SkipsPlacesAlreadySeen()
    {
        var names = new[] { BritainBank, MinocBank, Despise };
        var seen = new[] { BritainBank };

        var pick = SightseeRules.PickUnseen(names, seen, FirstSeed);

        Assert.NotEqual(BritainBank, pick);
        Assert.Contains(pick, new[] { MinocBank, Despise });
    }

    [Fact]
    public void PickUnseen_Prefer_TakesTheNamedTown()
    {
        var names = new[] { BritainBank, MinocBank, Despise };
        Assert.Equal(MinocBank, SightseeRules.PickUnseen(names, [], FirstSeed, "Minoc"));
    }

    [Fact]
    public void PickUnseen_WhenAllSeen_PicksFromAll()
    {
        var names = new[] { BritainBank, MinocBank };
        var seen = new[] { BritainBank, MinocBank };

        var pick = SightseeRules.PickUnseen(names, seen, SecondSeed);

        Assert.Equal(MinocBank, pick);
    }

    [Fact]
    public void PickUnseen_Empty_IsNull()
    {
        Assert.Null(SightseeRules.PickUnseen([], [], FirstSeed));
        Assert.Null(SightseeRules.PickUnseen(null, null, FirstSeed));
    }

    [Fact]
    public void SameFacet_NeedsTheHomeGraph()
    {
        var graph = new NavGraph(FacetNames.Felucca, []);

        Assert.False(SightseeRules.SameFacet(null, FacetNames.Felucca));
        Assert.False(SightseeRules.SameFacet(graph, FacetNames.Trammel));
        Assert.True(SightseeRules.SameFacet(graph, FacetNames.Felucca));
    }

    [Fact]
    public void CanRoute_OnlyWithinOneComponent()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                new NavNode { Name = "home", X = 0, Y = 0, Z = 0, Connects = ["shrine"] },
                new NavNode { Name = "shrine", X = 10, Y = 0, Z = 0, Connects = ["home"] },
                new NavNode { Name = "island", X = 30, Y = 0, Z = 0, Connects = [] }
            ]
        );

        Assert.True(SightseeRules.CanRoute(graph, "home", "shrine"));
        Assert.False(SightseeRules.CanRoute(graph, "home", "island"));
        Assert.False(SightseeRules.CanRoute(graph, "home", null));
        Assert.False(SightseeRules.CanRoute(graph, null, "shrine"));
        Assert.False(SightseeRules.CanRoute(null, "home", "shrine"));
    }
}
