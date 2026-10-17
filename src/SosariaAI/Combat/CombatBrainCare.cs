using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
using SosariaAI.Behaviour;
using SosariaAI.Common;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Spawning;

namespace SosariaAI.Combat;

/// <summary>
/// Self care in and out of a fight. Each remedy has its own cooldown; a care spell waits only
/// for the engine's own recovery (Mobile.NextSpellTime), so Heal goes twice in a row the way a
/// player said "In Mani" twice.
/// </summary>
public static partial class CombatBrain
{
    /// <summary>Pre-AOS heal potions share a ten second delay; cure potions wait as long here.</summary>
    public const int PotionGapMs = 10000;

    /// <summary>A refresh potion has no engine delay of its own (BaseRefreshPotion); one is drunk at most this often.</summary>
    public const int RefreshGapMs = 3000;

    public const int MillisecondsPerSecond = 1000;

    /// <summary>A build interval below this still waits this long between bandage attempts.</summary>
    public const double MinBandageGapSeconds = 1;

    private const string HealMateLine = "heal mate";
    private const string RaiseMateLine = "raise mate";
    private const string RefreshLine = "refresh";
    private const string ThrowLine = "throw explosion";
    private const string PouchLine = "trapped pouch";

    /// <summary>The spells self care may cast, for their stock (<see cref="CareFacts.Stock"/>).</summary>
    private static readonly SpellKind[] CareSpells = [SpellKind.Cure, SpellKind.Heal, SpellKind.GreaterHeal];

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
            versusPerson: false,
            CastTiming.TopCircle,
            spellsAllowed: true,
            ignoreCooldowns: true
        );
        return SelfCareRules.HasRemedy(facts);
    }

    /// <summary>
    /// A tamer's heal or cure spell on its pet (<see cref="PetRules.PetSpell"/>), cast from where
    /// it stands, in a fight or out of one. False when no spell fits, the pet is out of the
    /// spell's reach or sight, or the engine's spell recovery is not over.
    /// </summary>
    public static bool CastOnPet(SosariaCharacter tamer, BaseCreature pet) => CastHealOn(tamer, pet);

    /// <summary>
    /// A heal or cure spell on another body, a pet or a party mate, by the pet rules: from where
    /// the caster stands, within the spell's reach and sight, once the engine's spell recovery is
    /// over, and in a fight only when its words finish before the next blow (<paramref name="safeCircle"/>).
    /// </summary>
    public static bool CastHealOn(SosariaCharacter caster, Mobile patient, int safeCircle = CastTiming.TopCircle)
    {
        if (caster?.Deleted != false || patient?.Deleted != false || !patient.Alive || caster.Spell != null ||
            !caster.InRange(patient, PetRules.SpellReachTiles) || !caster.InLOS(patient))
        {
            return false;
        }

        if (Core.TickCount - caster.NextSpellTime < 0 ||
            PetRules.PetSpell(patient.Poisoned, PetRules.Wounded(patient.Hits, patient.HitsMax), caster.Skills.Magery.Value, caster.Mana)
                is not { } kind ||
            !SpellBook.Fits(SpellBook.EntryOf(kind), safeCircle) ||
            !BeginCast(caster, MemoryOf(caster), kind, patient))
        {
            return false;
        }

        RearmTankMage(caster);
        return true;
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
    /// Treats the wound or the poison with what the pack and the safe circle allow. In a fight
    /// with a person the bandage runs at any wound (<see cref="SelfCareRules.KeepsBandageRunning"/>);
    /// the pack is read for it only once the last bandage is done. True when the best care is a
    /// heal spell whose words would not finish before the next blow, so a caster should step
    /// clear first.
    /// </summary>
    private static bool Care(SosariaCharacter character, Memory memory, bool inFight, int safeCircle, bool spellsAllowed)
    {
        var pack = character.Backpack;
        var now = Core.TickCount;
        var hits = Vitals.HitsFraction(character);
        var versusPerson = inFight && People.IsLivingPlayer(character.Combatant);

        if (pack == null ||
            !SelfCareRules.NeedsCare(character.Poisoned, hits, inFight) &&
            !(SelfCareRules.KeepsBandageRunning(versusPerson, hits) && BandageFree(character, memory, now)) ||
            !SelfCareRules.TreatsNow(character.Hidden, inFight, character.Poisoned))
        {
            return false;
        }

        var facts = FactsOf(character, memory, inFight, versusPerson, safeCircle, spellsAllowed, ignoreCooldowns: false);
        var choice = SelfCareRules.Choose(facts);

        if (SelfCareRules.SpellOf(choice) is { } spell)
        {
            if (BeginCast(character, memory, spell, character))
            {
                RearmTankMage(character);
            }

            return false;
        }

        switch (choice)
        {
            case CareChoice.CurePotion:
            {
                memory.NextPotionAt = now + PotionGapMs;
                Drink(character, pack.FindItemByType<BaseCurePotion>());
                break;
            }
            case CareChoice.HealPotion:
            {
                memory.NextPotionAt = now + PotionGapMs;
                Drink(character, pack.FindItemByType<BaseHealPotion>());
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

    /// <summary>
    /// What the pack and the cooldowns allow. Only the remedies whose cooldown is up are looked
    /// for, and the reagents only when a spell may be cast: read for every hurt fighter out of a
    /// fight, the stock was a pack count each think for those who never cast.
    /// </summary>
    private static CareFacts FactsOf(
        SosariaCharacter character,
        Memory memory,
        bool inFight,
        bool versusPerson,
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
        var bandageReady = ignoreCooldowns || BandageFree(character, memory, now);
        var spellReady = spellsAllowed && (IsSpellFighter(character) || FightsAsTankMage(character)) &&
                         (ignoreCooldowns || character.Spell == null && memory.CastTarget == null && now - character.NextSpellTime >= 0);

        return new CareFacts(
            poisoned,
            Vitals.HitsFraction(character),
            inFight,
            versusPerson,
            safeCircle,
            spellReady,
            character.Skills.Magery.Value,
            character.Mana,
            potionReady,
            potionReady && poisoned && pack.FindItemByType<BaseCurePotion>() != null,
            potionReady && !poisoned && pack.FindItemByType<BaseHealPotion>() != null,
            bandageReady,
            bandageReady && healing > SelfCareRules.NoSkill && pack.FindItemByType<Bandage>() != null,
            healing,
            character.Skills.Anatomy.Value,
            !spellReady ? SpellStock.None
            : inFight ? memory.Kit.Stock
            : StockOf(pack, CareSpells, character)
        );
    }

    /// <summary>The last bandage is done and its pace is up, and no cursor is in the hands.</summary>
    private static bool BandageFree(SosariaCharacter character, Memory memory, long now) =>
        now - memory.NextBandageAt >= 0 && character.Target == null && BandageContext.GetContext(character) == null;

    /// <summary>
    /// A tank mage takes its weapon back up after a care spell: the engine cleared its hands
    /// for the words (Spell.ClearHandsOnCast). Before AOS a blow does not break the first
    /// circle (Spell.Disturb), so it heals where it stands and swings on.
    /// </summary>
    private static void RearmTankMage(SosariaCharacter character)
    {
        if (FightsAsTankMage(character))
        {
            RearmAfterCast(character);
        }
    }

    /// <summary>The engine's own drink check stands: a free hand, the heal potion delay, and a refresh only when winded.</summary>
    private static void Drink(SosariaCharacter character, BasePotion potion)
    {
        if (potion?.CanDrink(character) == true)
        {
            potion.Drink(character);
        }
    }

    /// <summary>
    /// A winded fighter drinks a refresh potion (<see cref="SelfCareRules.DrinksRefresh"/>), in a
    /// fight and on the run. The pack is searched only once the stamina is low.
    /// </summary>
    private static void TendStamina(SosariaCharacter character, Memory memory)
    {
        var now = Core.TickCount;

        if (!SelfCareRules.DrinksRefresh(Vitals.StaminaFraction(character), character.Hidden, now - memory.NextRefreshAt >= 0) ||
            character.Backpack?.FindItemByType<BaseRefreshPotion>() is not { } potion)
        {
            return;
        }

        memory.NextRefreshAt = now + RefreshGapMs;
        Drink(character, potion);

        if (LogsOnce(character, RefreshLine, character))
        {
            logger.Information("{Name} drinks a refresh potion at {Stam}/{StamMax} stamina", character.Name, character.Stam, character.StamMax);
        }
    }

    /// <summary>
    /// A paralyzed body opens a pouch it trapped with Magic Trap (<see cref="CombatItemRules.FreesFromParalyze"/>):
    /// the trap's blow frees it, in a fight and on the run. The pack is searched only while the
    /// body is held.
    /// </summary>
    private static void BreakParalyze(SosariaCharacter character)
    {
        if (!character.Paralyzed ||
            character.Backpack?.FindItemByType<Pouch>(true, static pouch => CombatItemRules.FreesFromParalyze(pouch.TrapType))
                is not { } pouch)
        {
            return;
        }

        character.Use(pouch);

        if (LogsOnce(character, PouchLine, character))
        {
            logger.Information("{Name} opens a trapped pouch to break a paralyze", character.Name);
        }
    }

    /// <summary>
    /// An explosion potion at a person held in place (<see cref="CombatItemRules.ThrowsExplosion"/>),
    /// the way a client does it: use the potion, target the foe. The engine's own pace between
    /// uses holds (Mobile.NextActionTime, set as the use packet sets it), and the throw is made
    /// only where the cursor takes it (reach, sight and line of sight, as Target.Invoke checks):
    /// a lit potion left in the pack blows up on its thrower.
    /// </summary>
    private static void TryThrowExplosion(SosariaCharacter character, Memory memory, Mobile foe)
    {
        var now = Core.TickCount;

        if (foe == null || foe.Map != character.Map || character.Hidden || character.Spell != null ||
            character.Target != null || now - character.NextActionTime < 0 || now - memory.NextThrowAt < 0 ||
            !CombatItemRules.ThrowsExplosion(
                People.IsLivingPlayer(foe),
                foe.Paralyzed || foe.Frozen,
                StyleOf(character, IsSpellFighter(character)) != CombatStyle.Melee,
                NavMetric.Chebyshev(character.Location, foe.Location),
                Core.ML
            ) ||
            !character.CanSee(foe) || !character.InLOS(foe) || !character.CanBeHarmful(foe, false) ||
            BlastHitsOthers(character, foe) ||
            character.Backpack?.FindItemByType<BaseExplosionPotion>() is not { } potion)
        {
            return;
        }

        character.NextActionTime = now + Mobile.ActionDelay;
        character.Use(potion);

        if (character.Target is not { } cursor)
        {
            return;
        }

        memory.NextThrowAt = now + CombatItemRules.FuseMs;
        cursor.Invoke(character, foe);

        if (LogsOnce(character, ThrowLine, foe))
        {
            logger.Information("{Name} throws an explosion potion at the held {Foe}", character.Name, foe.Name);
        }
    }

    /// <summary>
    /// True when a blast on the foe's tile would hurt someone beside it (BaseExplosionPotion.Explode:
    /// every body the thrower may harm, its party and guild aside), the foe and anyone fighting
    /// the thrower not counted.
    /// </summary>
    private static bool BlastHitsOthers(SosariaCharacter character, Mobile foe)
    {
        foreach (var mobile in foe.Map.GetMobilesInRange(foe.Location, CombatItemRules.BlastTiles))
        {
            if (mobile != foe && SpellHelper.ValidIndirectTarget(character, mobile) && character.CanBeHarmful(mobile, false) &&
                !IsFoe(character, mobile, out _))
            {
                return true;
            }
        }

        return false;
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
    /// Any other caster heals a friend deep in trouble while its own hits are safe
    /// (<see cref="HealerTacticsRules.HealsMates"/>). True when that took the think.
    /// </summary>
    private static bool TryHealerTactics(SosariaCharacter character, Memory memory, Mobile foe, int safeCircle)
    {
        var healer = character.PersonProfile.Class == PersonClass.Healer;

        return healer && TryRaiseMate(character, memory, foe) ||
               HealerTacticsRules.HealsMates(healer, Vitals.HitsFraction(character), character.Poisoned) &&
               TryHealMate(character, foe, safeCircle, healer);
    }

    /// <summary>
    /// The friends in reach of a heal spell, in sight: the caster's party, a red's gang mates, and
    /// the people fighting its foe that are no foe of its own. One a heal would make a blue caster
    /// a criminal for is left alone (<see cref="HealerTacticsRules.MayAid"/>). The worst off of
    /// them that needs it gets a heal or a cure (<see cref="CastHealOn"/>).
    /// </summary>
    private static bool TryHealMate(SosariaCharacter healer, Mobile foe, int safeCircle, bool healerClass)
    {
        if (healer.Map is not { } map || map == Map.Internal)
        {
            return false;
        }

        MateBodies.Clear();
        MateCares.Clear();
        var party = GameParty.Of(healer);
        var red = PkRules.IsRed(healer.Kills);

        foreach (var mobile in map.GetMobilesInRange(healer.Location, PetRules.SpellReachTiles))
        {
            if (mobile == healer || !People.IsLivingPlayer(mobile) ||
                !HealerTacticsRules.MayAid(healer.IsBeneficialCriminal(mobile), red) ||
                !(party != null && GameParty.Of(mobile) == party || IsGangMate(healer, mobile) ||
                  mobile.Combatant == foe && !IsFoe(healer, mobile, out _)))
            {
                continue;
            }

            MateBodies.Add(mobile);
            MateCares.Add(new MateCare(Vitals.HitsFraction(mobile), mobile.Poisoned));
        }

        var pick = HealerTacticsRules.Pick(MateCares, healerClass);

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

    /// <summary>A red of the same outlaw gang (<see cref="PkGangRules.SameGang"/>).</summary>
    private static bool IsGangMate(SosariaCharacter character, Mobile mobile) =>
        character.IsPk && mobile is SosariaCharacter { IsPk: true } mate && PkGangRules.SameGang(character.OutlawGang, mate.OutlawGang);

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
