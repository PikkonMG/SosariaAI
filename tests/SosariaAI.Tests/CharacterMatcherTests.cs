using System.Collections.Generic;
using SosariaAI.Configuration;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class CharacterMatcherTests
{
    private const int PlanSize = 2;

    [Fact]
    public void TakeMatch_ExactIdWins()
    {
        var remaining = Plan();

        var match = CharacterMatcher.TakeMatch("mira", FacetNames.Felucca, remaining);

        Assert.Equal("mira", match.Value.UniqueId);
        Assert.Single(remaining);
        Assert.Equal("connor", remaining[0].UniqueId);
    }

    [Fact]
    public void TakeMatch_QualifiedIdWinsWithinFacet()
    {
        var remaining = Plan();

        var match = CharacterMatcher.TakeMatch("Felucca:mira", FacetNames.Felucca, remaining);

        Assert.Equal("mira", match.Value.TemplateId);
        Assert.Single(remaining);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ghost")]
    public void TakeMatch_MissingOrUnknownId_Idles(string characterId)
    {
        var remaining = Plan();

        var match = CharacterMatcher.TakeMatch(characterId, FacetNames.Felucca, remaining);

        Assert.Null(match);
        Assert.Equal(PlanSize, remaining.Count);
    }

    private static List<SpawnPlanEntry> Plan() =>
    [
        new("connor", "connor"),
        new("mira", "mira")
    ];
}
