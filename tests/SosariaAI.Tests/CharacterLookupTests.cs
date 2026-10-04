using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class CharacterLookupTests
{
    [Fact]
    public void MatchRank_PersonaKey_FindsTheWornName()
    {
        Assert.True(CharacterLookup.MatchRank("Mira", "Patamon", "mira", "Felucca:mira") > CharacterLookup.RankNone);
        Assert.True(CharacterLookup.MatchRank("mira", "Patamon", "mira", "Felucca:mira") > CharacterLookup.RankNone);
        Assert.True(CharacterLookup.MatchRank("Patamon", "Patamon", "mira", "Felucca:mira") > CharacterLookup.RankNone);
        Assert.True(CharacterLookup.MatchRank("Bern", "Bernadette", "tam", "Felucca:tam") > CharacterLookup.RankNone);
        Assert.Equal(CharacterLookup.RankNone, CharacterLookup.MatchRank("Hal", "Patamon", "mira", "Felucca:mira"));
    }

    [Fact]
    public void LocalKey_StripsFacetAndReplica()
    {
        Assert.Equal("mira", CharacterLookup.LocalKey("Felucca:mira"));
        Assert.Equal("mira", CharacterLookup.LocalKey("Felucca:mira#2"));
        Assert.Equal("mira", CharacterLookup.PersonaKey("Felucca:mira", "mira"));
    }

    [Fact]
    public void PreferFacet_PrefersTheStaffMap()
    {
        Assert.True(CharacterLookup.PreferFacet("Felucca", "Felucca"));
        Assert.False(CharacterLookup.PreferFacet("Felucca", "Trammel"));
    }

    [Fact]
    public void ReplicaNumber_ReadsTheCopyIndex()
    {
        Assert.Equal(0, CharacterLookup.ReplicaNumber("Felucca:connor"));
        Assert.Equal(1, CharacterLookup.ReplicaNumber("Felucca:connor#1"));
        Assert.Equal(15, CharacterLookup.ReplicaNumber("Felucca:connor#15"));
    }

    [Fact]
    public void MatchRank_GoConnorPrefersTheFixtureOverACopyNamedLena()
    {
        var fixture = CharacterLookup.MatchRank("connor", "Connor", "connor", "Felucca:connor");
        var copy = CharacterLookup.MatchRank("connor", "Lena", "connor", "Felucca:connor#1");

        Assert.True(fixture > copy);
        Assert.True(copy > CharacterLookup.RankNone);
    }

    [Fact]
    public void MatchRank_ExactWornNameBeatsATemplateCopy()
    {
        var lena = CharacterLookup.MatchRank("Lena", "Lena", "connor", "Felucca:connor#1");
        var connor = CharacterLookup.MatchRank("Lena", "Connor", "connor", "Felucca:connor");

        Assert.True(lena > connor);
    }
}
