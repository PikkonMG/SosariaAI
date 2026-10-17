using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Spawning;

namespace SosariaAI.Combat;

/// <summary>One carried stack: an item type name and how many.</summary>
public readonly record struct KitStack(string TypeName, int Amount);

/// <summary>
/// What a person carries in its pack on the day it first walks in: a purse by wealth,
/// bandages by skill, heal and cure potions once it is a veteran, and recall scrolls
/// when it has the gold and trains Magery for travel without casting in a fight. A person
/// with no Magery cannot read a recall scroll, so it carries none and walks. The runebook
/// comes with the marked runes in <see cref="Skills.RuneKit"/>, in every era. A person minded to
/// fight people (a red, or a fighter that duels) carries the PvP tools of the period from
/// journeyman on: explosion potions when it fights from range (<see cref="CombatItemRules.ThrowsExplosion"/>),
/// and pouches trapped with Magic Trap to break a paralyze when it trains Magery. Given once,
/// never refilled: spent supplies are bought again like any player's.
/// </summary>
public static class VeteranKit
{
    public const string Gold = "Gold";
    public const string Bandage = "Bandage";
    public const string HealPotion = "HealPotion";
    public const string GreaterHealPotion = "GreaterHealPotion";
    public const string CurePotion = "CurePotion";
    public const string GreaterCurePotion = "GreaterCurePotion";
    public const string RefreshPotion = "RefreshPotion";
    public const string RecallScroll = "RecallScroll";
    public const string RawRibs = "RawRibs";
    public const string ExplosionPotion = "ExplosionPotion";
    public const string GreaterExplosionPotion = "GreaterExplosionPotion";

    /// <summary>A kit pouch comes trapped with Magic Trap (<see cref="KitResolver"/>).</summary>
    public const string TrappedPouch = "Pouch";

    public const int PoorPurse = 20;
    public const int ModestPurse = 80;
    public const int ComfortablePurse = 180;
    public const int RichPurse = 320;
    public const int PurseSpreadPercent = 50;
    private const int PercentBase = 100;

    public const int ModestScrolls = 2;
    public const int ComfortableScrolls = 5;
    public const int RichScrolls = 10;

    /// <summary>Bandages a healer carries at each tier, novice first.</summary>
    public static readonly int[] BandagesByTier = [10, 20, 30, 45, 60, 80, 100];

    private const int PurseSalt = 801;

    /// <summary>A red, or a fighter (the role that duels), is minded to fight people.</summary>
    public static bool PvpMinded(bool red, CharacterRole? role) => red || role == CharacterRole.Fighter;

    public static List<KitStack> For(PersonProfile profile, ClassBuildTemplate template, string uniqueId, bool pvpMinded)
    {
        var pack = new List<KitStack>();

        if (profile == null || template == null)
        {
            return pack;
        }

        pack.Add(new KitStack(Gold, Purse(profile.Wealth, uniqueId)));

        // A healer bandages people; a tamer's vet skill bandages its pet from the same stack.
        if (template.Trains(SkillName.Healing) || template.Trains(SkillName.Veterinary))
        {
            pack.Add(new KitStack(Bandage, BandagesByTier[(int)profile.Tier]));
        }

        // A tamer walks in with a full pantry of meat for the first pet it tames; it buys the rest.
        if (template.Trains(SkillName.AnimalTaming))
        {
            pack.Add(new KitStack(RawRibs, PetFoodRules.PantryTarget));
        }

        AddPotions(pack, profile.Tier, template);

        if (pvpMinded)
        {
            AddPvpTools(pack, profile.Tier, template);
        }

        var scrolls = template.TravelMagic ? Scrolls(profile.Wealth, profile.Tier) : 0;

        if (scrolls > 0)
        {
            pack.Add(new KitStack(RecallScroll, scrolls));
        }

        return pack;
    }

    public static int Purse(PersonWealth wealth, string uniqueId)
    {
        var purse = wealth switch
        {
            PersonWealth.Poor => PoorPurse,
            PersonWealth.Modest => ModestPurse,
            PersonWealth.Comfortable => ComfortablePurse,
            _ => RichPurse
        };

        return purse + purse * PersonDice.Roll(uniqueId, PurseSalt, PurseSpreadPercent + 1) / PercentBase;
    }

    /// <summary>
    /// A fresh character walked; a saver kept a few scrolls for a quick way home; the
    /// rich kept a stack.
    /// </summary>
    public static int Scrolls(PersonWealth wealth, SkillTier tier) =>
        wealth switch
        {
            PersonWealth.Rich => RichScrolls,
            PersonWealth.Comfortable => ComfortableScrolls,
            PersonWealth.Modest when tier >= SkillTier.Journeyman => ModestScrolls,
            _ => 0
        };

    /// <summary>
    /// One more of each tool for each tier past apprentice; greater explosion potions once
    /// adept. A melee fighter would walk into its own blast, so it carries none.
    /// </summary>
    private static void AddPvpTools(List<KitStack> pack, SkillTier tier, ClassBuildTemplate template)
    {
        var tools = tier - SkillTier.Apprentice;

        if (tools <= 0)
        {
            return;
        }

        if (template.Style != CombatStyle.Melee)
        {
            pack.Add(new KitStack(tier >= SkillTier.Adept ? GreaterExplosionPotion : ExplosionPotion, tools));
        }

        if (template.Trains(SkillName.Magery))
        {
            pack.Add(new KitStack(TrappedPouch, tools));
        }
    }

    private static void AddPotions(List<KitStack> pack, SkillTier tier, ClassBuildTemplate template)
    {
        if (template.Caster)
        {
            // A mage heals with spells; a veteran still keeps a few bottles for a paralyse or a fizzle.
            var bottles = tier - SkillTier.Expert;

            if (bottles > 0)
            {
                pack.Add(new KitStack(GreaterHealPotion, bottles));
                pack.Add(new KitStack(GreaterCurePotion, bottles));
            }

            return;
        }

        var heals = tier - SkillTier.Apprentice;

        if (heals > 0)
        {
            pack.Add(new KitStack(tier >= SkillTier.Adept ? GreaterHealPotion : HealPotion, heals));
        }

        var cures = tier - SkillTier.Journeyman;

        if (cures > 0)
        {
            pack.Add(new KitStack(tier >= SkillTier.Master ? GreaterCurePotion : CurePotion, cures));
        }

        var refreshes = tier - SkillTier.Expert;

        if (refreshes > 0 && template.Armor == KitArmor.Heavy)
        {
            pack.Add(new KitStack(RefreshPotion, refreshes));
        }
    }
}
