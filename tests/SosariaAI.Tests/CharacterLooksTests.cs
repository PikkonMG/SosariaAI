using SosariaAI.Spawning;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class CharacterLooksTests
{
    private const string ConnorName = "Connor";

    [Fact]
    public void Copies_AreMarked()
    {
        Assert.False(WorkSites.IsCopy("Felucca:mira"));
        Assert.True(WorkSites.IsCopy("Felucca:mira#1"));
    }

    [Fact]
    public void FixtureName_KeepsTheTemplateName()
    {
        Assert.Equal(ConnorName, CharacterLooks.FixtureName(ConnorName, "Felucca:connor"));
    }

    [Fact]
    public void FixtureName_MissingTemplate_UsesTheIdInTitleCase()
    {
        Assert.Equal(ConnorName, CharacterLooks.FixtureName(null, "Felucca:connor"));
        Assert.Equal(ConnorName, CharacterLooks.FixtureName("", "connor"));
    }

    [Fact]
    public void NeedsRename_HashNamesOnly()
    {
        Assert.True(CharacterLooks.NeedsRename("Connor#1"));
        Assert.True(CharacterLooks.NeedsRename(""));
        Assert.False(CharacterLooks.NeedsRename("Lena"));
        Assert.False(CharacterLooks.NeedsRename("Connor"));
    }
}
