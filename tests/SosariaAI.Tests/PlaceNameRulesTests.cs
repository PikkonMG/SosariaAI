using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class PlaceNameRulesTests
{
    private const string Facet = "Felucca";
    private const int Close = 10;
    private const int Far = 1000;

    [Theory]
    [InlineData("Covetous", true)]
    [InlineData("Britain Cemetery", true)]
    [InlineData("AirElemental 1743-589", false)]
    [InlineData("[THB 45]", false)]
    [InlineData("In", false)]
    [InlineData("Out", false)]
    [InlineData("NPC: Lorekeeper Oolua", false)]
    [InlineData("Area C", false)]
    [InlineData("spike deathtraps", false)]
    [InlineData("from Tera-Keep-Teleporter", false)]
    [InlineData("Buccaneer's Den Docks", true)]
    [InlineData(null, false)]
    public void IsSayableMarker_RealNamesOnly(string name, bool sayable) =>
        Assert.Equal(sayable, PlaceNameRules.IsSayableMarker(name));

    [Fact]
    public void Spoken_RegionFirst_TheFacetIsNoPlace()
    {
        Assert.Equal("Despise", PlaceNameRules.Spoken("Despise", Facet, "Covetous", Close, "Britain", Close));
        Assert.Equal("Covetous", PlaceNameRules.Spoken(Facet, Facet, "Covetous", Close, "Britain", Close));
        Assert.Equal("Covetous", PlaceNameRules.Spoken(null, Facet, "Covetous", Close, "Britain", Close));
    }

    [Fact]
    public void Spoken_HandleOrFarLandmark_FallsToTheTown()
    {
        Assert.Equal("Britain", PlaceNameRules.Spoken(null, Facet, "AirElemental 1743-589", Close, "Britain", Close));
        Assert.Equal("Britain", PlaceNameRules.Spoken(null, Facet, "Covetous", PlaceNameRules.LandmarkRange + 1, "Britain", Close));
    }

    [Fact]
    public void Spoken_FarFromEverything_IsTheWild() =>
        Assert.Equal(PlaceNameRules.Wild, PlaceNameRules.Spoken(null, Facet, "Covetous", Far, "Britain", Far));
}
