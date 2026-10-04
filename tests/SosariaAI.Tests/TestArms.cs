using Server;
using Server.Items;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Tests;

/// <summary>
/// Arms a test person the way its first day does: the combat pieces of its build's kit in its
/// pack, and a weapon in hand. Only an armed person draws, is drawn on, or stands to a person's
/// blow (<see cref="SpareKit.Armed"/>).
/// </summary>
internal static class TestArms
{
    internal static SosariaCharacter Arm(SosariaCharacter character)
    {
        if (character.Backpack == null)
        {
            character.AddItem(new Backpack());
        }

        foreach (var name in character.Build?.Kit ?? [])
        {
            if (KitOnceRules.IsCombatKitPiece(name) &&
                KitResolver.Create(name, typeName => AssemblyHandler.FindTypeByName(typeName)) is { } piece)
            {
                character.AddToBackpack(piece);
            }
        }

        // The tile data is not loaded in tests, so the dagger is told which hand it goes in.
        if (!GearEquip.CanArm(character))
        {
            character.AddItem(new Dagger { Layer = Layer.OneHanded });
        }

        return character;
    }
}
