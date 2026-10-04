using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class EraRulesTests
{
    private const string Despise = "Despise";
    private const string Khaldun = "Khaldun";
    private const string BlightedGroveAnyCase = "blighted grove";

    [Fact]
    public void FacetAllowed_Felucca_OnEveryExpansion()
    {
        Assert.True(EraRules.FacetAllowed(FacetNames.Felucca, Expansion.None));
        Assert.True(EraRules.FacetAllowed(FacetNames.Felucca, Expansion.T2A));
        Assert.True(EraRules.FacetAllowed(FacetNames.Felucca, Expansion.EJ));
    }

    [Fact]
    public void FacetAllowed_Trammel_FromRenaissance()
    {
        Assert.False(EraRules.FacetAllowed(FacetNames.Trammel, Expansion.T2A));
        Assert.True(EraRules.FacetAllowed(FacetNames.Trammel, Expansion.UOR));
        Assert.True(EraRules.FacetAllowed(FacetNames.Trammel, Expansion.EJ));
    }

    [Fact]
    public void FacetAllowed_LaterLands_MatchExpansionFloors()
    {
        Assert.False(EraRules.FacetAllowed(FacetNames.Ilshenar, Expansion.UOR));
        Assert.True(EraRules.FacetAllowed(FacetNames.Ilshenar, Expansion.UOTD));
        Assert.False(EraRules.FacetAllowed(FacetNames.Malas, Expansion.LBR));
        Assert.True(EraRules.FacetAllowed(FacetNames.Malas, Expansion.AOS));
        Assert.False(EraRules.FacetAllowed(FacetNames.Tokuno, Expansion.AOS));
        Assert.True(EraRules.FacetAllowed(FacetNames.Tokuno, Expansion.SE));
        Assert.False(EraRules.FacetAllowed(FacetNames.TerMur, Expansion.ML));
        Assert.True(EraRules.FacetAllowed(FacetNames.TerMur, Expansion.SA));
    }

    [Fact]
    public void FacetAllowed_UnknownName_IsFalse()
    {
        Assert.False(EraRules.FacetAllowed("Atlantis", Expansion.EJ));
        Assert.False(EraRules.FacetAllowed(null, Expansion.EJ));
    }

    [Fact]
    public void SkillAllowed_CurrentSkills_FromOriginalGame()
    {
        Assert.True(EraRules.SkillAllowed(SkillKinds.Lumberjack, Expansion.None));
        Assert.True(EraRules.SkillAllowed(SkillKinds.House, Expansion.T2A));
        Assert.True(EraRules.SkillAllowed(SkillKinds.Flee, Expansion.EJ));
        Assert.False(EraRules.SkillAllowed(null, Expansion.EJ));
        Assert.False(EraRules.SkillAllowed(" ", Expansion.EJ));
    }

    [Fact]
    public void SkillAllowed_Necro_FromAgeOfShadows()
    {
        Assert.False(EraRules.SkillAllowed(SkillKinds.Necro, Expansion.T2A));
        Assert.False(EraRules.SkillAllowed(SkillKinds.Necro, Expansion.UOTD));
        Assert.True(EraRules.SkillAllowed(SkillKinds.Necro, Expansion.AOS));
        Assert.True(EraRules.SkillAllowed(SkillKinds.Necro, Expansion.EJ));
    }

    [Theory]
    [InlineData(SkillName.Swords, Expansion.None)]
    [InlineData(SkillName.SpiritSpeak, Expansion.None)]
    [InlineData(SkillName.Necromancy, Expansion.AOS)]
    [InlineData(SkillName.Chivalry, Expansion.AOS)]
    [InlineData(SkillName.Focus, Expansion.AOS)]
    [InlineData(SkillName.Bushido, Expansion.SE)]
    [InlineData(SkillName.Ninjitsu, Expansion.SE)]
    [InlineData(SkillName.Spellweaving, Expansion.ML)]
    [InlineData(SkillName.Mysticism, Expansion.SA)]
    [InlineData(SkillName.Imbuing, Expansion.SA)]
    [InlineData(SkillName.Throwing, Expansion.SA)]
    public void SkillFloor_IsTheExpansionThatBroughtTheSkill(SkillName skill, Expansion floor) =>
        Assert.Equal(floor, EraRules.SkillFloor(skill));

    [Theory]
    [InlineData(EraRules.BlightedGrove, Expansion.ML)]
    [InlineData(EraRules.Sanctuary, Expansion.ML)]
    [InlineData(EraRules.ThePaintedCaves, Expansion.ML)]
    [InlineData(EraRules.TheHeartwood, Expansion.ML)]
    [InlineData(EraRules.TwistedWeald, Expansion.ML)]
    [InlineData(EraRules.YomotsuMines, Expansion.SE)]
    [InlineData(Despise, Expansion.None)]
    [InlineData(Khaldun, Expansion.None)]
    [InlineData(null, Expansion.None)]
    public void PlaceFloor_IsTheExpansionThatBroughtThePlace(string place, Expansion floor) =>
        Assert.Equal(floor, EraRules.PlaceFloor(place));

    [Fact]
    public void PlaceAllowed_BlightedGrove_WaitsForMondainsLegacy()
    {
        Assert.False(EraRules.PlaceAllowed(EraRules.BlightedGrove, Expansion.T2A));
        Assert.False(EraRules.PlaceAllowed(BlightedGroveAnyCase, Expansion.SE));
        Assert.True(EraRules.PlaceAllowed(EraRules.BlightedGrove, Expansion.ML));
        Assert.True(EraRules.PlaceAllowed(Despise, Expansion.T2A));
    }

    [Theory]
    [InlineData(EraBand.T2A, Expansion.LBR)]
    [InlineData(EraBand.ML, Expansion.ML)]
    [InlineData(EraBand.Modern, Expansion.EJ)]
    public void SkillFloor_LaterSkills_StayOutOfTheSecondAgeBand(EraBand band, Expansion last)
    {
        Assert.Equal(band, EraBands.Of(last));
        Assert.Equal(band != EraBand.T2A, EraRules.SkillFloor(SkillName.Chivalry) <= last);
        Assert.Equal(band == EraBand.Modern, EraRules.SkillFloor(SkillName.Mysticism) <= last);
    }
}
