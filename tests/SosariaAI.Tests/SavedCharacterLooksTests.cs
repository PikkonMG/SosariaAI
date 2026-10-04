using Server;
using Server.Items;
using SosariaAI.Mobiles;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

[CollectionDefinition("Saved character looks", DisableParallelization = true)]
public class SavedCharacterLooksCollection;

[Collection("Saved character looks")]
public class SavedCharacterLooksTests
{
    static SavedCharacterLooksTests() => Timer.Init(0);

    public SavedCharacterLooksTests() => TestMap.EnsureInternal();

    [Fact]
    public void Rebind_KeepsSavedNameFaceAndDyedClothes()
    {
        var character = SavedCharacter();
        var robe = new Robe((Serial)0x40000001) { Hue = 123, Layer = Layer.OuterTorso };
        character.Items.Add(robe);

        for (var restart = 0; restart < 3; restart++)
        {
            CharacterLooks.Apply(character, "Felucca:mira#1", "Mira", PersonProfile.Default, alreadyInWorld: true);
        }

        Assert.Equal("Known friend", character.Name);
        Assert.True(character.Female);
        Assert.Equal(401, character.Body.BodyID);
        Assert.Equal(1020, character.Hue);
        Assert.Equal(0x203D, character.HairItemID);
        Assert.Equal(1120, character.HairHue);
        Assert.Same(robe, Assert.Single(character.Items));
        Assert.Equal(123, robe.Hue);
    }

    [Fact]
    public void Rebind_LeavesUndyedLeatherUndyed()
    {
        // Leather took no dye in the Second Age; a saved chest stays as it was made.
        var character = SavedCharacter();
        var chest = new LeatherChest((Serial)0x40000002) { Layer = Layer.InnerTorso };
        character.Items.Add(chest);

        CharacterLooks.Apply(character, "Felucca:mira#1", "Mira", PersonProfile.Default, alreadyInWorld: true);

        Assert.Equal(0, chest.Hue);
        Assert.Same(chest, Assert.Single(character.Items));
        Assert.Equal("Known friend", character.Name);
    }

    [Fact]
    public void Rebind_GhostKeepsGhostBody()
    {
        var character = SavedCharacter();
        character.Body = 403;
        CharacterLooks.Apply(character, "Felucca:mira#1", "Mira", PersonProfile.Default, alreadyInWorld: true);
        Assert.Equal(403, character.Body.BodyID);
    }

    private static SosariaCharacter SavedCharacter()
    {
        var character = new SosariaCharacter((Serial)1);
        character.DefaultMobileInit();
        character.Name = "Known friend";
        character.Female = true;
        character.Body = 401;
        character.Hue = 1020;
        character.HairItemID = 0x203D;
        character.HairHue = 1120;
        return character;
    }
}
