using System;
using Server;
using Server.Items;
using Server.Spells;
using Server.Spells.Fifth;
using Server.Spells.First;
using Server.Spells.Fourth;
using Server.Spells.Second;
using Server.Spells.Seventh;
using Server.Spells.Sixth;
using Server.Spells.Third;
using Server.Targeting;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Combat;

/// <summary>
/// Casting through the real spell path: Cast() starts the words, and the target cursor
/// that comes up when they finish is answered on a later think. Reagents and mana are the
/// engine's to take, and it takes them only when the words finish (Spell.CheckSequence);
/// a broken cast costs time and the disturb recovery. The book must hold the spell.
/// </summary>
public static partial class CombatBrain
{
    /// <summary>A pure mage casts as often as the engine's recovery lets it.</summary>
    public const int MageCastGapMs = 1000;

    /// <summary>A tank mage mostly swings; it casts between swings now and then.</summary>
    public const int TankCastGapMs = 4000;

    /// <summary>
    /// A pure mage fights from range. It picks the spell it wants for open ground, casts it when
    /// the words finish before the next blow, takes a strong spell that fits in a window (the
    /// foe just swung, or is still closing), and otherwise steps clear. When the steps cannot buy
    /// the room it casts what fits where it stands. Between casts it keeps its band of distance.
    /// </summary>
    private static void FightAsMage(
        SosariaCharacter character,
        Memory memory,
        Mobile foe,
        int distance,
        CombatStance stance,
        int safeCircle,
        bool careWantsRoom
    )
    {
        // The words root the feet until they end (Spell.BlocksMovement).
        if (character.Spell != null)
        {
            return;
        }

        if (distance > SpellBook.Reach || !character.InLOS(foe))
        {
            EndKite(memory);
            Approach(character, memory, foe, distance > SpellBook.Reach ? MageKeepMax : MeleeRange);
            return;
        }

        if (TryBreakFoeCast(character, memory, foe))
        {
            return;
        }

        if (Wish(character, memory, foe, distance, stance) is not { } wish)
        {
            KeepBand(character, memory, foe, distance, stance, careWantsRoom);
            return;
        }

        var fits = SpellBook.Fits(SpellBook.EntryOf(wish), safeCircle);
        ExpireBreakAway(memory);

        switch (FootworkRules.Next(fits, KitingMs(memory), memory.Pinned, stance))
        {
            case Footwork.Act:
            {
                CastAt(character, memory, foe, wish, committed: false);
                break;
            }
            case Footwork.OpenGap:
            {
                if (stance is not (CombatStance.Press or CombatStance.Kite) ||
                    AttackThatFits(character, distance, safeCircle) is not { } window ||
                    !FootworkRules.TakesWindow(SpellBook.EntryOf(window).Circle) ||
                    !CastAt(character, memory, foe, window, committed: false))
                {
                    OpenGap(character, memory, foe, FootworkRules.RetreatSteps(memory.Threats.NearestDistance));
                }

                break;
            }
            case Footwork.Commit:
            {
                if (AttackThatFits(character, distance, safeCircle) is { } fallback)
                {
                    CastAt(character, memory, foe, fallback, committed: true, stance);
                }

                break;
            }
        }
    }

    /// <summary>
    /// The spell this mage wants next, given open ground: the ward in the Protect stance,
    /// Paralyze on a free foe in the ParalyzeThenKite stance, nothing while it heals or walks off
    /// a held foe, else the planned attack. The plan stands until it is cast or cannot be.
    /// </summary>
    private static SpellKind? Wish(SosariaCharacter character, Memory memory, Mobile foe, int distance, CombatStance stance)
    {
        if (!CastReady(character, memory))
        {
            return null;
        }

        var magery = character.Skills.Magery.Value;
        var mana = character.Mana;
        var held = foe.Paralyzed || foe.Frozen;

        switch (stance)
        {
            case CombatStance.Heal:
            {
                return null;
            }
            case CombatStance.Protect when !WardUp(character):
            {
                var ward = SpellBook.WardOf(Core.UOR);

                if (SpellBook.CanCast(SpellBook.EntryOf(ward), magery, mana))
                {
                    return ward;
                }

                break;
            }
            case CombatStance.ParalyzeThenKite:
            {
                if (!held)
                {
                    return SpellBook.CanCast(SpellBook.Paralyze, magery, mana) ? SpellKind.Paralyze : null;
                }

                if (distance < FootworkRules.BandMin(stance, foeHeld: true))
                {
                    return null;
                }

                break;
            }
        }

        if (memory.Planned is { } planned && StillWorthCasting(planned, magery, mana, held, foe.Poisoned))
        {
            return planned;
        }

        memory.Planned = SpellBook.PickUtility(
                             magery,
                             mana,
                             distance,
                             CastTiming.TopCircle,
                             held,
                             foe.Poisoned,
                             Utility.RandomDouble(),
                             Utility.RandomDouble()
                         ) ??
                         SpellBook.PickAttack(magery, mana, distance, CastTiming.TopCircle, Utility.RandomDouble());
        return memory.Planned;
    }

    private static bool StillWorthCasting(SpellKind kind, double magery, int mana, bool foeHeld, bool foePoisoned) =>
        SpellBook.CanCast(SpellBook.EntryOf(kind), magery, mana) &&
        !(kind == SpellKind.Paralyze && foeHeld) &&
        !(kind == SpellKind.Poison && foePoisoned);

    /// <summary>The best attack whose words finish before the next blow, or null.</summary>
    private static SpellKind? AttackThatFits(SosariaCharacter character, int distance, int safeCircle) =>
        SpellBook.PickAttack(character.Skills.Magery.Value, character.Mana, distance, safeCircle, Utility.RandomDouble());

    /// <summary>
    /// Nothing to cast this tick: hold the band for the stance. Too near, or a heal that needs
    /// more room, steps clear; too far walks in; inside the band it stands.
    /// </summary>
    private static void KeepBand(
        SosariaCharacter character,
        Memory memory,
        Mobile foe,
        int distance,
        CombatStance stance,
        bool careWantsRoom
    )
    {
        if (stance == CombatStance.Hold)
        {
            EndKite(memory);
            return;
        }

        var held = foe.Paralyzed || foe.Frozen;

        if (distance < FootworkRules.BandMin(stance, held) || stance == CombatStance.Heal && careWantsRoom)
        {
            OpenGap(character, memory, foe, FootworkRules.RetreatSteps(memory.Threats.NearestDistance));
            return;
        }

        EndKite(memory);

        if (distance > MageKeepMax)
        {
            Approach(character, memory, foe, MageKeepMax);
        }
    }

    /// <summary>The cast gap and the engine's own recovery are both up, and no spell or cursor is in the hands.</summary>
    private static bool CastReady(SosariaCharacter character, Memory memory)
    {
        var now = Core.TickCount;

        return now - memory.NextCastAt >= 0 && now - character.NextSpellTime >= 0 &&
               character.Spell == null && character.Target == null;
    }

    /// <summary>
    /// A person saying a spell is broken by a Magic Arrow that lands before the words end. A
    /// monster's words cannot be broken (Spell.OnCasterHurt), so it is never tried on one.
    /// </summary>
    private static bool TryBreakFoeCast(SosariaCharacter character, Memory memory, Mobile foe)
    {
        if (!CastReady(character, memory) ||
            !FootworkRules.CanBreakCast(
                foe.Player,
                FoeCastLeftMs(foe),
                FootworkRules.ArrowBreakMs(SlowedByProtection(character))
            ) ||
            !SpellBook.CanCast(SpellBook.MagicArrow, character.Skills.Magery.Value, character.Mana) ||
            !CastAt(character, memory, foe, SpellKind.MagicArrow, committed: false))
        {
            return false;
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} throws a Magic Arrow to break {Foe}'s spell", character.Name, foe.Name);
        }

        return true;
    }

    /// <summary>
    /// Casts at the foe, or on itself for the ward. A cast bought by steps out of reach, and one
    /// cast where it stands because the steps could not buy the room, are logged to be counted.
    /// A cast where it stands keeps the break-away clock, so the stand ends and the steps resume.
    /// </summary>
    private static bool CastAt(
        SosariaCharacter character,
        Memory memory,
        Mobile foe,
        SpellKind kind,
        bool committed,
        CombatStance stance = CombatStance.Press
    )
    {
        var self = kind == SpellBook.WardOf(Core.UOR);

        if (!self && !character.CanBeHarmful(foe, false) || !BeginCast(character, memory, kind, self ? character : foe))
        {
            return false;
        }

        memory.NextCastAt = Core.TickCount + MageCastGapMs;

        if (memory.Planned == kind)
        {
            memory.Planned = null;
        }

        if (committed)
        {
            CastTally.NoteStood();

            // Pinned, a mage casts each Magic Arrow where it stands (a blow cannot break the
            // first circle before AOS); the tally counts every one, the log says it once a stand.
            if (LogsOnce(character, ShakeLine, foe))
            {
                logger.Information(
                    "{Name} cannot shake {Foe} ({Why}) and casts {Spell} where it stands",
                    character.Name,
                    foe.Name,
                    CommitReason(memory, stance),
                    kind
                );
            }
        }
        else if (memory.KitingSince != 0)
        {
            CastTally.NoteSteppedClear();

            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Name} stepped clear of {Foe} to cast {Spell}", character.Name, foe.Name, kind);
            }
        }

        if (!committed)
        {
            EndKite(memory);
        }

        return true;
    }

    /// <summary>Any character with Magery and a spellbook fights with spells as well.</summary>
    private static bool IsSpellFighter(SosariaCharacter character) =>
        SpellBook.IsCaster(character.Skills.Magery.Value) && Spellbook.FindRegular(character) != null;

    /// <summary>The weapon in either hand, or null when the hands hold none.</summary>
    public static BaseWeapon HeldWeapon(Mobile mobile) =>
        mobile.FindItemOnLayer(Layer.OneHanded) as BaseWeapon ??
        mobile.FindItemOnLayer(Layer.TwoHanded) as BaseWeapon;

    /// <summary>
    /// A weapon fighter that also casts: a Paralyze or Poison by chance, else a pick among the
    /// strongest attacks, all only from the circles whose words finish before the next blow. Toe
    /// to toe a Magic Arrow costs a weapon swing for a few points, so it swings instead.
    /// </summary>
    private static bool TryCastAttack(
        SosariaCharacter character,
        Memory memory,
        Mobile foe,
        int distance,
        int gapMs,
        int safeCircle
    )
    {
        if (!CastReady(character, memory) || distance > SpellBook.Reach ||
            !character.InLOS(foe) || !character.CanBeHarmful(foe, false))
        {
            return false;
        }

        memory.NextCastAt = Core.TickCount + gapMs;
        var magery = character.Skills.Magery.Value;
        var mana = character.Mana;
        var kind = SpellBook.PickUtility(
                       magery,
                       mana,
                       distance,
                       safeCircle,
                       foe.Paralyzed || foe.Frozen,
                       foe.Poisoned,
                       Utility.RandomDouble(),
                       Utility.RandomDouble()
                   ) ??
                   SpellBook.PickAttack(magery, mana, distance, safeCircle, Utility.RandomDouble());

        if (kind is not { } picked || picked == SpellKind.MagicArrow && distance <= MeleeRange)
        {
            return false;
        }

        return BeginCast(character, memory, picked, foe);
    }

    private static bool BeginCast(SosariaCharacter character, Memory memory, SpellKind kind, Mobile target)
    {
        if (character.Spell != null || character.Target != null ||
            Spellbook.Find(character, SpellBook.EntryOf(kind).SpellId) == null)
        {
            return false;
        }

        var spell = NewSpell(kind, character);

        if (!SpellCasting.HasReagents(character.Backpack, spell.Info))
        {
            return false;
        }

        // Casting clears the hands; a tank mage takes its weapon back when the spell is done.
        var weapon = HeldWeapon(character);

        if (!spell.Cast())
        {
            return false;
        }

        memory.CastTarget = target;
        memory.CastKind = kind;

        // Only a spell that raises a cursor shows whether a blow broke it, so only those count.
        if (SpellBook.RaisesCursor(kind, Core.UOR))
        {
            memory.CastsThisFight++;
            CastTally.NoteBegun(SpellBook.EntryOf(kind).Circle);
        }

        if (weapon != null)
        {
            memory.RearmWeapon = weapon;
        }

        return true;
    }

    /// <summary>
    /// Answers the target cursor of a spell that finished its words, cancels it when the
    /// target is gone, counts a spell that ended with no cursor as broken by a blow, and
    /// re-arms a tank mage once the spell is over.
    /// </summary>
    private static void ResolvePendingCast(SosariaCharacter character, Memory memory)
    {
        var target = memory.CastTarget;

        if (target != null)
        {
            if (character.Spell is Spell { State: SpellState.Sequencing } && character.Target is { } cursor)
            {
                memory.CastTarget = null;

                if (target.Deleted || !target.Alive || target.Map != character.Map)
                {
                    cursor.Cancel(character, TargetCancelType.Canceled);
                }
                else
                {
                    cursor.Invoke(character, target);
                }
            }
            else if (character.Spell == null)
            {
                memory.CastTarget = null;

                if (SpellBook.RaisesCursor(memory.CastKind, Core.UOR))
                {
                    NoteInterrupt(character, memory);
                }
            }
        }

        var weapon = memory.RearmWeapon;

        if (weapon == null || character.Spell != null || memory.CastTarget != null)
        {
            return;
        }

        memory.RearmWeapon = null;

        if (!weapon.Deleted && weapon.IsChildOf(character.Backpack) && HeldWeapon(character) == null)
        {
            GearEquip.EquipTool(character, weapon);
        }
    }

    private static Spell NewSpell(SpellKind kind, Mobile caster) =>
        kind switch
        {
            SpellKind.MagicArrow => new MagicArrowSpell(caster),
            SpellKind.Harm => new HarmSpell(caster),
            SpellKind.Fireball => new FireballSpell(caster),
            SpellKind.Lightning => new LightningSpell(caster),
            SpellKind.MindBlast => new MindBlastSpell(caster),
            SpellKind.EnergyBolt => new EnergyBoltSpell(caster),
            SpellKind.Explosion => new ExplosionSpell(caster),
            SpellKind.FlameStrike => new FlameStrikeSpell(caster),
            SpellKind.Poison => new PoisonSpell(caster),
            SpellKind.Paralyze => new ParalyzeSpell(caster),
            SpellKind.Heal => new HealSpell(caster),
            SpellKind.Cure => new CureSpell(caster),
            SpellKind.GreaterHeal => new GreaterHealSpell(caster),
            SpellKind.ReactiveArmor => new ReactiveArmorSpell(caster),
            SpellKind.Protection => new ProtectionSpell(caster),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}
