using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Common;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Spawning;

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

    private const string HealMateLine = "heal mate";
    private const string RaiseMateLine = "raise mate";

    /// <summary>The friends a healer weighs this think and how each stands, reused think to think. World thread only.</summary>
    private static readonly List<Mobile> MateBodies = [];

    private static readonly List<MateCare> MateCares = [];

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
    public static bool CastOnPet(SosariaCharacter tamer, BaseCreature pet) => CastHealOn(tamer, pet);

    /// <summary>
    /// A heal or cure spell on another body, a pet or a party mate, by the pet rules: from where
    /// the caster stands, within the spell's reach and sight, not too soon after the last one,
    /// and in a fight only when its words finish before the next blow (<paramref name="safeCircle"/>).
    /// </summary>
    public static bool CastHealOn(SosariaCharacter caster, Mobile patient, int safeCircle = CastTiming.TopCircle)
    {
        if (caster?.Deleted != false || patient?.Deleted != false || !patient.Alive || caster.Spell != null ||
            !caster.InRange(patient, PetRules.SpellReachTiles) || !caster.InLOS(patient))
        {
            return false;
        }

        var memory = MemoryOf(caster);
        var now = Core.TickCount;
        if (now - memory.NextSpellCareAt < 0 ||
            PetRules.PetSpell(patient.Poisoned, PetRules.Wounded(patient.Hits, patient.HitsMax), caster.Skills.Magery.Value, caster.Mana)
                is not { } kind ||
            !SpellBook.Fits(SpellBook.EntryOf(kind), safeCircle))
        {
            return false;
        }

        memory.NextSpellCareAt = now + SpellCareGapMs;
        return BeginCast(caster, memory, kind, patient);
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

        if (pack == null || !SelfCareRules.NeedsCare(character.Poisoned, Vitals.HitsFraction(character), inFight) ||
            !SelfCareRules.TreatsNow(character.Hidden, inFight, character.Poisoned))
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
            spellsAllowed && IsSpellFighter(character) && (ignoreCooldowns ||
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

    /// <summary>
    /// A mage-healer (<see cref="HealerTacticsRules"/>) raises a fallen party mate when it can,
    /// else heals or cures the worst off of its friends in the fight, before it casts at the foe.
    /// True when that took the think.
    /// </summary>
    private static bool TryHealerTactics(SosariaCharacter character, Memory memory, Mobile foe, int safeCircle) =>
        character.PersonProfile.Class == PersonClass.Healer &&
        (TryRaiseMate(character, memory, foe) || TryHealMate(character, foe, safeCircle));

    /// <summary>
    /// The friends in reach of a heal spell, in sight: the healer's party, and the people fighting
    /// its foe that are no foe of its own. One a heal would make the healer a criminal for is left
    /// alone. The worst off of them that needs it gets a heal or a cure (<see cref="CastHealOn"/>).
    /// </summary>
    private static bool TryHealMate(SosariaCharacter healer, Mobile foe, int safeCircle)
    {
        if (healer.Map is not { } map || map == Map.Internal)
        {
            return false;
        }

        MateBodies.Clear();
        MateCares.Clear();
        var party = GameParty.Of(healer);

        foreach (var mobile in map.GetMobilesInRange(healer.Location, PetRules.SpellReachTiles))
        {
            if (mobile == healer || !People.IsLivingPlayer(mobile) || healer.IsBeneficialCriminal(mobile) ||
                !(party != null && GameParty.Of(mobile) == party || mobile.Combatant == foe && !IsFoe(healer, mobile, out _)))
            {
                continue;
            }

            MateBodies.Add(mobile);
            MateCares.Add(new MateCare(Vitals.HitsFraction(mobile), mobile.Poisoned));
        }

        var pick = HealerTacticsRules.Pick(MateCares);

        if (pick == HealerTacticsRules.NoPatient || !CastHealOn(healer, MateBodies[pick], safeCircle))
        {
            return false;
        }

        if (LogsOnce(healer, HealMateLine, MateBodies[pick]))
        {
            logger.Information(
                "{Name} heals {Mate} at {Hits}/{HitsMax} hits in the fight with {Foe}",
                healer.Name,
                MateBodies[pick].Name,
                MateBodies[pick].Hits,
                MateBodies[pick].HitsMax,
                foe.Name
            );
        }

        return true;
    }

    /// <summary>
    /// A fallen party mate in sight and near is raised with Resurrection when nothing fights the
    /// healer and the killer has left the ghost (<see cref="ResurrectOffer.Hindrance"/>), the way
    /// the raise out of a fight judges it: the healer walks to the ghost, then says the words. A
    /// ghost another helper works on is left to that one.
    /// </summary>
    private static bool TryRaiseMate(SosariaCharacter healer, Memory memory, Mobile foe)
    {
        if (GameParty.Of(healer) is not { } party)
        {
            return false;
        }

        for (var i = 0; i < party.Members.Count; i++)
        {
            if (party.Members[i].Mobile is not PlayerMobile { Deleted: false, Alive: false } ghost || ghost == healer ||
                ghost.Map != healer.Map || !healer.CanSee(ghost) || !healer.InLOS(ghost) || ResurrectOffer.HelperFor(ghost) != null ||
                !HealerTacticsRules.RaisesInFight(
                    ResurrectOffer.WillAid(healer, ghost, asked: true) && ResurrectOffer.MethodFor(healer) == AidMethod.Spell,
                    ResurrectOffer.Hindrance(healer, ghost) != null,
                    NavMetric.Chebyshev(healer.Location, ghost.Location)
                ))
            {
                continue;
            }

            return RaiseNow(healer, memory, ghost, foe);
        }

        return false;
    }

    /// <summary>Walks into the spell's one tile (ResurrectionSpell.TargetRange), waits out the recovery, then casts.</summary>
    private static bool RaiseNow(SosariaCharacter healer, Memory memory, Mobile ghost, Mobile foe)
    {
        if (!healer.InRange(ghost, ResurrectAid.CastRange))
        {
            return healer.Motor.MoveTo(ghost, ResurrectAid.CastRange);
        }

        if (!CastReady(healer, memory))
        {
            healer.Motor.Stop();
            return true;
        }

        if (!CastAid(healer, memory, SpellKind.Resurrection, ghost))
        {
            return false;
        }

        if (LogsOnce(healer, RaiseMateLine, ghost))
        {
            logger.Information("{Name} says the words of Resurrection over {Ghost} in the fight with {Foe}", healer.Name, ghost.Name, foe.Name);
        }

        return true;
    }
}
