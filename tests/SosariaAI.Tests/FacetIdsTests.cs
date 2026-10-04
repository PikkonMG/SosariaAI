using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class FacetIdsTests
{
    [Fact]
    public void QualifyParty_PrefixesIdLeaderAndMembers()
    {
        var source = new PartyDefinition
        {
            Id = CharactersFile.PartyGraveyardCrew,
            Leader = PersonasFile.BranId,
            Members = [PersonasFile.BranId, PersonasFile.SelaId, PersonasFile.TamId]
        };

        var qualified = FacetIds.QualifyParty(FacetNames.Felucca, source);

        Assert.Equal("Felucca:graveyard-crew", qualified.Id);
        Assert.Equal("Felucca:bran", qualified.Leader);
        Assert.Equal(["Felucca:bran", "Felucca:sela", "Felucca:tam"], qualified.Members);
        Assert.Equal(CharactersFile.PartyGraveyardCrew, source.Id);
        Assert.Equal(PersonasFile.BranId, source.Leader);
    }

    [Fact]
    public void QualifyParty_TwoFacets_DoNotCollide()
    {
        var source = new PartyDefinition
        {
            Id = CharactersFile.PartyGraveyardCrew,
            Leader = PersonasFile.BranId,
            Members = [PersonasFile.BranId]
        };

        var felucca = FacetIds.QualifyParty(FacetNames.Felucca, source);
        var trammel = FacetIds.QualifyParty(FacetNames.Trammel, source);

        Assert.Equal("Felucca:graveyard-crew", felucca.Id);
        Assert.Equal("Trammel:graveyard-crew", trammel.Id);
        Assert.NotEqual(felucca.Leader, trammel.Leader);
    }

    [Fact]
    public void Prefix_DoesNotDoublePrefix()
    {
        var once = FacetIds.Prefix(FacetNames.Felucca, "bran");
        Assert.Equal("Felucca:bran", once);
        Assert.Equal("Felucca:bran", FacetIds.Prefix(FacetNames.Felucca, once));
    }
}
