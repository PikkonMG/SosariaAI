using Server;
using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// T2A poisoning, the engine's way: a poison potion from the pack onto a one-handed bladed or
/// piercing weapon, or onto food. The potion goes at the try, and the engine's check runs in
/// the potion's own skill window (a lesser poison 0 to 60). A poisoner with no potion, or
/// nothing it may coat, has no work: it practised on nothing and failed "the skill had
/// nothing to work on" 198 times in one night.
/// </summary>
public static class PoisonRules
{
    public const string Kind = SkillKinds.Poison;
    public const int PoisonChargesBase = 18;
    public const int PoisonChargesPerLevel = 2;
    public const int PotionConsumeAmount = 1;

    public static int ChargesFor(int level) =>
        PoisonChargesBase - level * PoisonChargesPerLevel;

    /// <summary>The engine's pre-AOS rule: only a one-handed slashing or piercing weapon takes poison.</summary>
    public static bool IsPoisonable(Layer layer, WeaponType type) =>
        layer == Layer.OneHanded && type is WeaponType.Slashing or WeaponType.Piercing;

    /// <summary>A poison potion and a weapon or food it may coat, all at hand.</summary>
    public static bool HasWork(SosariaCharacter character) =>
        People.InWorld(character) && FindPotion(character) != null && FindCoatable(character) != null;

    public static BasePoisonPotion FindPotion(SosariaCharacter character)
    {
        var packed = character?.Backpack?.FindItemByType<BasePoisonPotion>();
        return packed is { Deleted: false } ? packed : null;
    }

    /// <summary>The weapon in hand or in the pack that takes poison, else food in the pack, else null.</summary>
    public static Item FindCoatable(SosariaCharacter character) =>
        (Item)FindWeapon(character) ?? character?.Backpack?.FindItemByType<Food>();

    public static BaseWeapon FindWeapon(SosariaCharacter character)
    {
        if (character == null)
        {
            return null;
        }

        if (character.FindItemOnLayer(Layer.OneHanded) is BaseWeapon { Deleted: false } held && IsPoisonable(held.Layer, held.Type))
        {
            return held;
        }

        foreach (var item in character.Backpack?.Items ?? [])
        {
            if (item is BaseWeapon { Deleted: false } packed && IsPoisonable(packed.Layer, packed.Type))
            {
                return packed;
            }
        }

        return null;
    }

    /// <summary>Coats the weapon or the food with the poison, as the engine's timer does on a passed check.</summary>
    public static bool Apply(Item target, Poison poison, Mobile poisoner)
    {
        if (poison == null)
        {
            return false;
        }

        switch (target)
        {
            case BaseWeapon weapon:
            {
                weapon.Poison = poison;
                weapon.PoisonCharges = ChargesFor(poison.Level);
                return true;
            }
            case Food food:
            {
                food.Poison = poison;
                food.Poisoner = poisoner;
                return true;
            }
            default:
            {
                return false;
            }
        }
    }

    public static void ConsumePotion(SosariaCharacter character, BasePoisonPotion potion)
    {
        if (character == null || potion == null || potion.Deleted)
        {
            return;
        }

        potion.Consume(PotionConsumeAmount);
        character.AddToBackpack(new Bottle());
    }
}
