using System.Reflection;
using Server;
using Server.Items;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// The world loads every mobile before any item. While a character's load hook runs,
/// its items are blank: no parent, no map, no layer. The hook must not touch them.
/// </summary>
public class CharacterLoadHookTests
{
    private const uint CharacterSerial = 0x7301;
    private const uint SavedPackSerial = 0x40007301;
    private const string LoadHookName = "AfterDeserialization";

    static CharacterLoadHookTests() => Timer.Init(0);

    public CharacterLoadHookTests() => TestMap.EnsureLand();

    [Fact]
    public void LoadHook_WithBlankSavedBackpack_AddsNoSecondBackpack()
    {
        var character = LoadingCharacter(out var savedPack);

        RunLoadHook(character);

        Assert.Single(character.Items);
        Assert.Same(savedPack, character.Items[0]);
    }

    private static SosariaCharacter LoadingCharacter(out Backpack savedPack)
    {
        var character = new SosariaCharacter((Serial)CharacterSerial);
        character.DefaultMobileInit();
        character.Map = Map.Internal;
        savedPack = new Backpack((Serial)SavedPackSerial);
        character.Items.Add(savedPack);
        return character;
    }

    private static void RunLoadHook(SosariaCharacter character) =>
        typeof(SosariaCharacter)
            .GetMethod(LoadHookName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(character, null);
}
