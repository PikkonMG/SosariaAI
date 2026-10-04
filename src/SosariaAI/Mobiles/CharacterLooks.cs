using System;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Combat;
using SosariaAI.Logging;
using SosariaAI.Skills;
using SosariaAI.Spawning;

namespace SosariaAI.Mobiles;

/// <summary>
/// A person's face, name and clothes. A fresh person gets its gender from its profile,
/// a natural skin and hair colour, a period hair cut and beard, a speech colour, a
/// player-style name nobody else wears, and the cloth of its class dyed from the dye
/// tub. A saved person keeps its face and clothes; only a clashing name changes.
/// </summary>
public static class CharacterLooks
{
    private static readonly ILogger logger = SosariaLog.For(typeof(CharacterLooks));

    public const int FirstSkinHue = 1002;
    public const int SkinHueCount = 57;
    public const int FirstHairHue = 1102;
    public const int HairHueCount = 48;
    public const int Bald = 0;
    public const int BeardPercent = 55;
    public const int DefaultSpeechPercent = 60;

    /// <summary>The client's own default speech colour.</summary>
    public const int DefaultSpeechHue = 0x3B2;

    private const int SkinSalt = 1201;
    private const int HairSalt = 1207;
    private const int HairHueSalt = 1213;
    private const int BeardSalt = 1217;
    private const int BeardStyleSalt = 1223;
    private const int SpeechSalt = 1229;
    private const int SpeechHueSalt = 1231;

    private static readonly int[] MaleHair =
    [
        0x203B, 0x203B, 0x203C, 0x203D, 0x2044, 0x2045, 0x2047, 0x2048, 0x204A, Bald
    ];

    private static readonly int[] FemaleHair =
    [
        0x203B, 0x203C, 0x203C, 0x203D, 0x2045, 0x2046, 0x2047, 0x2049, 0x204A
    ];

    private static readonly int[] Beards = [0x203E, 0x203F, 0x2040, 0x2041, 0x204B, 0x204C, 0x204D];

    /// <summary>Bright, readable speech colours players picked in the options.</summary>
    private static readonly int[] SpeechHues =
    [
        0x03, 0x0D, 0x13, 0x1C, 0x21, 0x30, 0x37, 0x3A, 0x44, 0x59, 0x62, 0x71
    ];

    public static bool NeedsRename(string worn) =>
        string.IsNullOrWhiteSpace(worn) || worn.Contains(CharacterLookup.ReplicaMark);

    /// <summary>A fixture wears its authored name, or its id in title case when it has none.</summary>
    public static string FixtureName(string templateName, string uniqueId)
    {
        if (!string.IsNullOrWhiteSpace(templateName))
        {
            return templateName;
        }

        var local = CharacterLookup.LocalKey(uniqueId);
        return string.IsNullOrWhiteSpace(local) ? local : char.ToUpperInvariant(local[0]) + local[1..];
    }

    public static void Apply(
        SosariaCharacter character,
        string uniqueId,
        string templateName,
        PersonProfile profile,
        bool alreadyInWorld
    )
    {
        if (character == null)
        {
            return;
        }

        var person = profile ?? PersonProfile.Default;

        // The world save owns an existing character's face and clothes.
        if (!alreadyInWorld)
        {
            Shape(character, person, uniqueId);
            Dress(character, person, uniqueId);
        }

        BindName(character, uniqueId, templateName, alreadyInWorld);
    }

    private static void Shape(SosariaCharacter character, PersonProfile person, string uniqueId)
    {
        character.Female = person.Female;
        character.Body = Race.Human.AliveBody(person.Female);
        character.Hue = FirstSkinHue + PersonDice.Roll(uniqueId, SkinSalt, SkinHueCount);
        character.HairItemID = PersonDice.Pick(uniqueId, HairSalt, person.Female ? FemaleHair : MaleHair);
        character.HairHue = FirstHairHue + PersonDice.Roll(uniqueId, HairHueSalt, HairHueCount);
        character.FacialHairItemID = !person.Female && PersonDice.Chance(uniqueId, BeardSalt, BeardPercent)
            ? PersonDice.Pick(uniqueId, BeardStyleSalt, Beards)
            : Bald;
        character.FacialHairHue = character.HairHue;
        character.SpeechHue = PersonDice.Chance(uniqueId, SpeechSalt, DefaultSpeechPercent)
            ? DefaultSpeechHue
            : PersonDice.Pick(uniqueId, SpeechHueSalt, SpeechHues);
    }

    /// <summary>
    /// A copy keeps a saved name nobody else wears. A fresh copy, a marked name or a
    /// name another person already wears gets the first free player-style name for its id.
    /// </summary>
    private static void BindName(SosariaCharacter character, string uniqueId, string templateName, bool alreadyInWorld)
    {
        if (!WorkSites.IsCopy(uniqueId))
        {
            var fixture = FixtureName(templateName, uniqueId);

            if (!string.IsNullOrWhiteSpace(fixture))
            {
                character.Name = fixture;
            }

            return;
        }

        var worn = character.Name;

        if (alreadyInWorld && !NeedsRename(worn) && !NameRegistry.IsTaken(worn, character))
        {
            NameRegistry.Claim(worn, character);
            return;
        }

        var name = PlayerNameRules.Pick(uniqueId, character.Female, candidate => NameRegistry.IsTaken(candidate, character));
        character.Name = name;
        NameRegistry.Claim(name, character);
    }

    private static void Dress(SosariaCharacter character, PersonProfile person, string uniqueId)
    {
        var outfit = OutfitRules.For(person, uniqueId, GearEquip.WearsBodyArmor(character));

        for (var i = 0; i < outfit.Count; i++)
        {
            var piece = KitResolver.Create(outfit[i].TypeName, name => AssemblyHandler.FindTypeByName(name));

            if (piece == null)
            {
                logger.Warning("Outfit type {Type} was not found", outfit[i].TypeName);
                continue;
            }

            piece.Hue = outfit[i].Hue;
            Wear(character, piece);
        }
    }

    // Armor from the kit owns its layer; cloth replaces only cloth.
    private static void Wear(SosariaCharacter character, Item piece)
    {
        var old = character.FindItemOnLayer(piece.Layer);

        if (old is BaseArmor)
        {
            piece.Delete();
            return;
        }

        old?.Delete();
        character.AddItem(piece);
    }
}
