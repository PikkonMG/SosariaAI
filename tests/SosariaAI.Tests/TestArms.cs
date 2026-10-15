using Server;
using Server.Items;
using SosariaAI.Combat;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Tests;

/// <summary>
/// Arms a test person the way its first day does: the combat pieces of its build's kit in its
/// pack, what its build fights with in hand (<see cref="WieldRules"/>) and a tunic on. Only an
/// armed person draws, is drawn on, or stands to a person's blow (<see cref="SpareKit.Armed"/>).
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

        // The tile data is not loaded in tests, so the weapon and the tunic are told where they go.
        if (!GearEquip.CanArm(character))
        {
            character.AddItem(OwnArm(character.Build));
        }

        if (GearScore.SlotRank(character, GearSlot.Chest) == GearScore.UnarmedScore)
        {
            character.AddItem(new LeatherChest { Layer = Layer.InnerTorso });
        }

        return character;
    }

    /// <summary>A caster's spellbook, else a one-handed weapon of the build's fighting skill.</summary>
    private static Item OwnArm(ResolvedBuild build)
    {
        if (WieldRules.HoldsSpellbook(build))
        {
            return new Spellbook();
        }

        Item weapon = build?.Trains(SkillName.Fencing) == true ? new Dagger()
            : build?.Trains(SkillName.Macing) == true ? new Mace()
            : new Katana();
        weapon.Layer = Layer.OneHanded;
        return weapon;
    }
}
