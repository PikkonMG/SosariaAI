using System;
using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Common;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Combat;

/// <summary>Self care in and out of a fight. Each remedy has its own cooldown.</summary>
public static partial class CombatBrain
{
    /// <summary>A heal or cure spell on oneself, at most this often.</summary>
    public const int SpellCareGapMs = 3000;

    /// <summary>Pre-AOS heal potions share a ten second delay; cure potions wait as long here.</summary>
    public const int PotionGapMs = 10000;

    public const int MillisecondsPerSecond = 1000;

    /// <summary>A build interval below this still waits this long between bandage attempts.</summary>
    public const double MinBandageGapSeconds = 1;

    /// <summary>Out of a fight: bandage, drink or cast when hurt or poisoned.</summary>
    public static void TendWounds(SosariaCharacter character)
    {
        if (character?.Deleted != false || !character.Alive)
        {
            return;
        }

        if (Memories.TryGetValue(character, out var known))
        {
            ResolvePendingCast(character, known);
        }

        if (SelfCareRules.NeedsCare(character.Poisoned, Vitals.HitsFraction(character), inFight: false))
        {
            Care(character, MemoryOf(character), inFight: false, CastTiming.TopCircle, spellsAllowed: true);
        }
    }

    /// <summary>
    /// True when this character's pack or spells hold a remedy for its wound right now, out of
    /// a fight: a bandage it can use, a potion, or a heal or cure spell it can cast.
    /// </summary>
    public static bool HasRemedy(SosariaCharacter character)
    {
        if (character?.Deleted != false || !character.Alive || character.Backpack == null)
        {
            return false;
        }

        var facts = FactsOf(
            character,
            MemoryOf(character),
            inFight: false,
            CastTiming.TopCircle,
            spellsAllowed: true,
            ignoreCooldowns: true
        );
        return SelfCareRules.HasRemedy(facts);
    }

    /// <summary>
    /// A tamer's heal or cure spell on its pet (<see cref="PetRules.PetSpell"/>), cast from where
    /// it stands, in a fight or out of one. False when no spell fits, the pet is out of the
    /// spell's reach or sight, or the last care spell is too recent.
    /// </summary>
    public static bool CastOnPet(SosariaCharacter tamer, BaseCreature pet)
    {
        if (tamer?.Deleted != false || pet?.Deleted != false || !pet.Alive || tamer.Spell != null ||
            !tamer.InRange(pet, PetRules.SpellReachTiles) || !tamer.InLOS(pet))
        {
            return false;
        }

        var memory = MemoryOf(tamer);
        var now = Core.TickCount;
        var wounded = pet.HitsMax > 0 && pet.Hits < pet.HitsMax * PetRules.VetBelowFraction;

        if (now - memory.NextSpellCareAt < 0 ||
            PetRules.PetSpell(pet.Poisoned, wounded, tamer.Skills.Magery.Value, tamer.Mana) is not { } kind)
        {
            return false;
        }

        memory.NextSpellCareAt = now + SpellCareGapMs;
        return BeginCast(tamer, memory, kind, pet);
    }

    /// <summary>
    /// A bandage on someone else the way a client does it: use the bandage, target the
    /// patient. The engine checks the range and runs the heal. False when no bandage is in
    /// the pack or the last one is still on.
    /// </summary>
    public static bool BandageOther(SosariaCharacter healer, Mobile patient)
    {
        var bandage = healer?.Backpack?.FindItemByType<Bandage>();

        if (bandage == null || patient?.Deleted != false || healer.Target != null ||
            BandageContext.GetContext(healer) != null)
        {
            return false;
        }

        var memory = MemoryOf(healer);
        var now = Core.TickCount;

        if (now - memory.NextBandageAt < 0)
        {
            return false;
        }

        memory.NextBandageAt = now + BandageGapMs(healer);
        VetBandage.Put(healer, bandage, patient);
        return true;
    }

    /// <summary>
    /// Treats the wound or the poison with what the pack and the safe circle allow. True when
    /// the best care is a heal spell whose words would not finish before the next blow, so a
    /// caster should step clear first.
    /// </summary>
    private static bool Care(SosariaCharacter character, Memory memory, bool inFight, int safeCircle, bool spellsAllowed)
    {
        var pack = character.Backpack;

        if (pack == null || !SelfCareRules.NeedsCare(character.Poisoned, Vitals.HitsFraction(character), inFight))
        {
            return false;
        }

        var now = Core.TickCount;
        var facts = FactsOf(character, memory, inFight, safeCircle, spellsAllowed, ignoreCooldowns: false);
        var choice = SelfCareRules.Choose(facts);

        if (SelfCareRules.SpellOf(choice) is { } spell)
        {
            SelfCast(character, memory, spell, now);
            return false;
        }

        switch (choice)
        {
            case CareChoice.CurePotion:
            {
                Drink(character, memory, pack.FindItemByType<BaseCurePotion>(), now);
                break;
            }
            case CareChoice.HealPotion:
            {
                Drink(character, memory, pack.FindItemByType<BaseHealPotion>(), now);
                break;
            }
            case CareChoice.Bandage:
            {
                ApplyBandage(character, memory, pack.FindItemByType<Bandage>(), now);
                break;
            }
        }

        return SelfCareRules.WantsRoom(facts);
    }

    /// <summary>What the pack and the cooldowns allow. Only the remedies whose cooldown is up are looked for.</summary>
    private static CareFacts FactsOf(
        SosariaCharacter character,
        Memory memory,
        bool inFight,
        int safeCircle,
        bool spellsAllowed,
        bool ignoreCooldowns
    )
    {
        var pack = character.Backpack;
        var poisoned = character.Poisoned;
        var now = Core.TickCount;
        var healing = character.Skills.Healing.Value;
        var potionReady = ignoreCooldowns || now - memory.NextPotionAt >= 0;
        var bandageReady = ignoreCooldowns ||
                           now - memory.NextBandageAt >= 0 && character.Target == null &&
                           BandageContext.GetContext(character) == null;

        return new CareFacts(
            poisoned,
            Vitals.HitsFraction(character),
            inFight,
            safeCircle,
            spellsAllowed && (ignoreCooldowns ||
                              character.Spell == null && memory.CastTarget == null && now - memory.NextSpellCareAt >= 0),
            character.Skills.Magery.Value,
            character.Mana,
            potionReady,
            potionReady && poisoned && pack.FindItemByType<BaseCurePotion>() != null,
            potionReady && !poisoned && pack.FindItemByType<BaseHealPotion>() != null,
            bandageReady,
            bandageReady && healing > SelfCareRules.NoSkill && pack.FindItemByType<Bandage>() != null,
            healing,
            character.Skills.Anatomy.Value
        );
    }

    private static void SelfCast(SosariaCharacter character, Memory memory, SpellKind kind, long now)
    {
        memory.NextSpellCareAt = now + SpellCareGapMs;
        BeginCast(character, memory, kind, character);
    }

    /// <summary>The engine's own drink check stands: a free hand, and the heal potion delay.</summary>
    private static void Drink(SosariaCharacter character, Memory memory, BasePotion potion, long now)
    {
        memory.NextPotionAt = now + PotionGapMs;

        if (potion?.CanDrink(character) == true)
        {
            potion.Drink(character);
        }
    }

    /// <summary>A bandage on oneself the way a client does it: use the bandage, target self.</summary>
    private static void ApplyBandage(SosariaCharacter character, Memory memory, Bandage bandage, long now)
    {
        memory.NextBandageAt = now + BandageGapMs(character);

        if (bandage == null)
        {
            return;
        }

        VetBandage.Put(character, bandage, character);
    }

    /// <summary>The build's heal interval paces bandage attempts; the engine stops overlap itself.</summary>
    private static long BandageGapMs(SosariaCharacter character)
    {
        var seconds = character.Build?.HealInterval ?? SosariaCombat.DefaultHealIntervalSeconds;
        return (long)(Math.Max(seconds, MinBandageGapSeconds) * MillisecondsPerSecond);
    }
}
