using System;
using System.Runtime.CompilerServices;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
using Server.Spells.Eighth;
using Server.Spells.Fifth;
using Server.Spells.First;
using Server.Spells.Fourth;
using Server.Spells.Second;
using Server.Spells.Seventh;
using Server.Spells.Sixth;
using Server.Spells.Third;
using Server.Targeting;
using SosariaAI.Common;
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

    /// <summary>A caster on its way out asks whether a recall would take at most this often.</summary>
    public const int RecallLookGapMs = 1000;

    private const string OpenerLine = "opener";
    private const string DispelLine = "dispel";
    private const string BurstLine = "burst";
    private const string VanishLine = "vanish";
    private const string RecallOutLine = "recall out";
    private const string LeaveHiddenLine = "leave hidden";

    // Weak keys: a deleted character's tactics go with it, as its fight memory does.
    private static readonly ConditionalWeakTable<SosariaCharacter, CasterTactics> Tactics = new();

    /// <summary>
    /// A caster's tactics for the fight it is in, beside the fight memory: whether it opened,
    /// the spell its burst still owes, when the fight began and since when it stands hidden. A
    /// new fight (<see cref="Memory.FightId"/>) starts them afresh.
    /// </summary>
    private sealed class CasterTactics
    {
        public int FightId = -1;
        public long FightSince;
        public bool Opened;
        public SpellKind? FollowUp;
        public long HiddenSince;
        public long NextRecallLookAt;
    }

    private static CasterTactics TacticsOf(SosariaCharacter character, Memory memory)
    {
        var tactics = Tactics.GetValue(character, static _ => new CasterTactics());

        if (tactics.FightId != memory.FightId)
        {
            tactics.FightId = memory.FightId;
            tactics.FightSince = Core.TickCount;
            tactics.Opened = false;
            tactics.FollowUp = null;
            tactics.HiddenSince = 0;
            tactics.NextRecallLookAt = 0;
        }

        return tactics;
    }

    /// <summary>
    /// A pure mage fights from range. It picks the spell it wants (what fits now while the foe
    /// stands in its band, see <see cref="FootworkRules.PlanCircle"/>), casts it when the words
    /// finish before the next blow, takes a strong spell that fits in a window (the foe just
    /// swung, or is still closing), and otherwise steps clear. When the steps cannot buy the
    /// room it casts what fits where it stands. Between casts it keeps its band of distance.
    /// Its class's tactics come first (<see cref="TryCasterTactics"/>): a tamer's moves in its
    /// pets' fight, the way out of a fight it is losing, and a healer's care of its friends.
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
        if (character.Spell != null || TryCasterTactics(character, memory, foe, distance, stance, safeCircle))
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

        if (Wish(character, memory, foe, distance, stance, safeCircle) is not { } wish)
        {
            KeepBand(character, memory, foe, distance, stance, careWantsRoom);
            return;
        }

        switch (FootworkNow(memory, SpellBook.Fits(SpellBook.EntryOf(wish), safeCircle), stance))
        {
            case Footwork.Act:
            {
                CastAt(character, memory, foe, wish, committed: false);
                break;
            }
            case Footwork.OpenGap:
            {
                if (stance is not (CombatStance.Press or CombatStance.Kite) ||
                    AttackThatFits(character, memory, distance, safeCircle) is not { } window ||
                    !FootworkRules.TakesWindow(SpellBook.EntryOf(window).Circle) ||
                    !CastAt(character, memory, foe, window, committed: false))
                {
                    StepOut(character, memory, foe);
                }

                break;
            }
            case Footwork.Commit:
            {
                if (AttackThatFits(character, memory, distance, safeCircle) is { } fallback)
                {
                    CastAt(character, memory, foe, fallback, committed: true, stance);
                }

                break;
            }
        }
    }

    /// <summary>
    /// The spell this mage wants next: the ward in the Protect stance, Paralyze on a free foe in
    /// the ParalyzeThenKite stance, nothing while it heals or walks off a held foe, else what its
    /// tactics call for (<see cref="TacticSpell"/>), else the planned attack, picked up to the circle <see cref="FootworkRules.PlanCircle"/> gives. A
    /// foe in the band with nothing that fits now gets a plan for open ground, so the footwork
    /// steps for the room. The plan stands until it is cast, cannot be, or no longer fits the
    /// plan circle.
    /// </summary>
    private static SpellKind? Wish(
        SosariaCharacter character,
        Memory memory,
        Mobile foe,
        int distance,
        CombatStance stance,
        int safeCircle
    )
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

                if (SpellBook.CanCast(SpellBook.EntryOf(ward), magery, mana) && memory.Kit.Stock.Has(ward))
                {
                    return ward;
                }

                break;
            }
            case CombatStance.ParalyzeThenKite:
            {
                if (!held)
                {
                    return SpellBook.CanCast(SpellBook.Paralyze, magery, mana) && memory.Kit.Stock.Has(SpellKind.Paralyze)
                        ? SpellKind.Paralyze
                        : null;
                }

                if (distance < FootworkRules.BandMin(stance, foeHeld: true))
                {
                    return null;
                }

                break;
            }
        }

        var planCircle = FootworkRules.PlanCircle(distance, FootworkRules.BandMin(stance, held), MageKeepMax, safeCircle);

        if (TacticSpell(character, memory, foe, held, planCircle) is { } tactic)
        {
            return tactic;
        }

        if (memory.Planned is { } planned && StillWorthCasting(planned, magery, mana, held, foe.Poisoned, memory.Kit.Stock) &&
            SpellBook.Fits(SpellBook.EntryOf(planned), planCircle))
        {
            return planned;
        }

        memory.Planned = PlanSpell(character, foe, distance, planCircle, memory.Kit.Stock) ??
                         PlanSpell(character, foe, distance, CastTiming.TopCircle, memory.Kit.Stock);
        return memory.Planned;
    }

    /// <summary>
    /// A spell at or under <paramref name="circle"/>: Paralyze or Poison by chance, else a pick
    /// among the strongest attacks (<see cref="SpellBook"/>). Null when nothing can be cast.
    /// </summary>
    private static SpellKind? PlanSpell(SosariaCharacter character, Mobile foe, int distance, int circle, SpellStock stock)
    {
        var magery = character.Skills.Magery.Value;
        var mana = character.Mana;

        return SpellBook.PickUtility(
                   magery,
                   mana,
                   distance,
                   circle,
                   foe.Paralyzed || foe.Frozen,
                   foe.Poisoned,
                   Utility.RandomDouble(),
                   Utility.RandomDouble(),
                   stock
               ) ??
               SpellBook.PickAttack(magery, mana, distance, circle, Utility.RandomDouble(), stock);
    }

    /// <summary>
    /// The spell a mage's tactics call for (<see cref="MageTacticsRules"/>): Dispel on a summoned
    /// foe it will likely unmake, the burst's follow-up while it is owed, Paralyze to open on a
    /// free melee foe, or Explosion to start a burst when the mana holds both spells; null when
    /// none does.
    /// </summary>
    private static SpellKind? TacticSpell(SosariaCharacter character, Memory memory, Mobile foe, bool held, int planCircle)
    {
        var tactics = TacticsOf(character, memory);
        var magery = character.Skills.Magery.Value;
        var mana = character.Mana;

        if (foe is BaseCreature { IsDispellable: true } summon &&
            MageTacticsRules.Dispels(
                MageTacticsRules.DispelChance(magery, summon.DispelDifficulty, summon.DispelFocus),
                memory.Kit.Stock.Has(SpellKind.Dispel) && SpellBook.CanCast(SpellBook.Dispel, magery, mana),
                planCircle
            ))
        {
            return SpellKind.Dispel;
        }

        if (tactics.FollowUp is { } follow)
        {
            if (SpellBook.CanCast(SpellBook.EntryOf(follow), magery, mana) && memory.Kit.Stock.Has(follow))
            {
                return follow;
            }

            tactics.FollowUp = null;
        }

        if (MageTacticsRules.OpensWithParalyze(
                tactics.Opened,
                StyleOfFoe(foe),
                held,
                memory.Kit.Stock.Has(SpellKind.Paralyze) && SpellBook.CanCast(SpellBook.Paralyze, magery, mana),
                planCircle
            ))
        {
            return SpellKind.Paralyze;
        }

        return MageTacticsRules.StartsBurst(magery, mana, planCircle, Vitals.HitsFraction(foe), Core.AOS, memory.Kit.Stock)
            ? SpellKind.Explosion
            : null;
    }

    /// <summary>
    /// What a caster does before its next attack: a tamer works its pets' fight, a caster that is
    /// losing or hidden gets out (<see cref="TryEscape"/>), and a healer keeps its friends up. True
    /// when that took the think.
    /// </summary>
    private static bool TryCasterTactics(
        SosariaCharacter character,
        Memory memory,
        Mobile foe,
        int distance,
        CombatStance stance,
        int safeCircle
    )
    {
        var tactics = TacticsOf(character, memory);
        tactics.HiddenSince = !character.Hidden ? 0 : tactics.HiddenSince == 0 ? Core.TickCount : tactics.HiddenSince;

        return TryTamerTactics(character, memory, tactics, foe, distance, stance, safeCircle) ||
               TryEscape(character, memory, tactics, foe, stance, safeCircle) ||
               TryHealerTactics(character, memory, foe, safeCircle);
    }

    /// <summary>
    /// A caster that is losing (<see cref="MageTacticsRules.Losing"/>) or stands hidden gets out
    /// (<see cref="MageTacticsRules.Escape"/>): it recalls home when the recall-out path allows
    /// it, else goes invisible, and holds still while hidden and a recall may still come
    /// (<see cref="MageTacticsRules.WaitsForRecall"/>); with none to wait for it leaves the
    /// fight on foot, and the run holds still while it stays hidden (<see cref="HidesFromChasers"/>).
    /// A monster drops a foe it cannot see at its next swing (Mobile.CheckCombatTime), so the
    /// caster leaves the blows before the line where it would run, and meditates after the fight
    /// (<see cref="TendMana"/>).
    /// </summary>
    private static bool TryEscape(
        SosariaCharacter character,
        Memory memory,
        CasterTactics tactics,
        Mobile foe,
        CombatStance stance,
        int safeCircle
    )
    {
        var hidden = character.Hidden;
        var attackers = memory.Picture.Attackers;
        var losing = !hidden && MageTacticsRules.Losing(
            Vitals.HitsFraction(character),
            RetreatRules.Line(BaseLineOf(character), NerveOf(character, memory), attackers),
            memory.Outlook,
            attackers
        );

        if (!hidden && !losing)
        {
            return false;
        }

        var recallAllowed = RecallsOut(character, memory, tactics);
        var waitsForRecall = MageTacticsRules.WaitsForRecall(
            memory.RecallOutOver,
            tactics.HiddenSince == 0 ? 0 : Core.TickCount - tactics.HiddenSince
        );

        switch (MageTacticsRules.Escape(hidden, losing, recallAllowed, waitsForRecall, losing && CanVanish(character, memory)))
        {
            case EscapeMove.Recall:
            {
                return RecallOut(character, foe);
            }
            case EscapeMove.Vanish:
            {
                return TryVanish(character, memory, foe, stance, safeCircle);
            }
            case EscapeMove.StayHidden:
            {
                character.Motor.Stop();
                return true;
            }
            case EscapeMove.Leave:
            {
                LeaveHidden(character, memory, foe);
                return true;
            }
            default:
            {
                return false;
            }
        }
    }

    /// <summary>
    /// True when a recall home would take now by the recall-out path (<see cref="RecallOutRules"/>):
    /// every blow lands after the words, and a rune, the means and the place allow it
    /// (<see cref="RecallRules.WhyNoRecall"/>: no recall that strands a pet, none in the heat of
    /// battle). Asked at most every <see cref="RecallLookGapMs"/>; a refusal that stays ends the tries for the fight.
    /// </summary>
    private static bool RecallsOut(SosariaCharacter character, Memory memory, CasterTactics tactics)
    {
        var now = Core.TickCount;

        if (memory.RecallOutOver || now - tactics.NextRecallLookAt < 0 ||
            !RecallOutRules.ContactBroken(memory.Threats.SoonestBlowMs, Core.AOS, SlowedByProtection(character)))
        {
            return false;
        }

        tactics.NextRecallLookAt = now + RecallLookGapMs;
        var whyNot = RecallRules.WhyNoRecall(character, character.HomeSpot, RecallOutRules.MinTripTiles);
        memory.RecallOutOver = !RecallOutRules.TriesAgain(whyNot);
        return whyNot == null;
    }

    /// <summary>
    /// A hidden caster with no recall to wait for leaves the fight on foot: a run decided by its
    /// own escape, as a flee Jev ordered is, so blows that barely hurt do not turn it round.
    /// </summary>
    private static void LeaveHidden(SosariaCharacter character, Memory memory, Mobile foe)
    {
        BreakOff(character, memory, foe, ordered: true, FleeSpan());

        if (LogsOnce(character, LeaveHiddenLine, foe))
        {
            logger.Information("{Name} has no recall out and leaves {Foe} on foot while hidden", character.Name, foe.Name);
        }
    }

    /// <summary>Says the words home; the travel spell answers its own cursor (<see cref="TravelSpells"/>).</summary>
    private static bool RecallOut(SosariaCharacter character, Mobile foe)
    {
        if (!RecallRules.TryRecallToward(character, character.HomeSpot, RecallOutRules.MinTripTiles))
        {
            return false;
        }

        character.Motor.Stop();

        if (LogsOnce(character, RecallOutLine, foe))
        {
            logger.Information(
                "{Name} recalls home out of the fight with {Foe} at {Hits}/{HitsMax} hits",
                character.Name,
                foe.Name,
                character.Hits,
                character.HitsMax
            );
        }

        return true;
    }

    /// <summary>The cast gap and recovery are up, and the book, Magery, mana and reagents hold Invisibility.</summary>
    private static bool CanVanish(SosariaCharacter character, Memory memory) =>
        CastReady(character, memory) && Readies(character, SpellKind.Invisibility);

    /// <summary>The Magery and mana cast the spell, the book holds it and the pack holds its reagents.</summary>
    private static bool Readies(SosariaCharacter character, SpellKind kind)
    {
        var entry = SpellBook.EntryOf(kind);

        return SpellBook.CanCast(entry, character.Skills.Magery.Value, character.Mana) &&
               Spellbook.Find(character, entry.SpellId) != null &&
               character.Backpack is { } pack && HasReagents(pack, kind, character);
    }

    /// <summary>
    /// Invisibility on itself, by the footwork rules: cast when its words finish before the next
    /// blow, else a step clear for the room. False when the steps cannot buy it.
    /// </summary>
    private static bool TryVanish(SosariaCharacter character, Memory memory, Mobile foe, CombatStance stance, int safeCircle)
    {
        switch (FootworkNow(memory, SpellBook.Fits(SpellBook.Invisibility, safeCircle), stance))
        {
            case Footwork.Act:
            {
                if (!CastAid(character, memory, SpellKind.Invisibility, character))
                {
                    return false;
                }

                EndKite(memory);

                if (LogsOnce(character, VanishLine, foe))
                {
                    logger.Information(
                        "{Name} goes invisible to lose {Foe} at {Hits}/{HitsMax} hits",
                        character.Name,
                        foe.Name,
                        character.Hits,
                        character.HitsMax
                    );
                }

                return true;
            }
            case Footwork.OpenGap:
            {
                StepOut(character, memory, foe);
                return true;
            }
            default:
            {
                return false;
            }
        }
    }

    /// <summary>
    /// A caster on the run casts Invisibility once its lead lets the words finish before the
    /// next blow (<see cref="SafeCircleNow"/>): the chasers lose it, where a run alone dragged
    /// them along until it was caught. The blows are read now, from this scan's chasers: the
    /// read the fight left behind told a runner well clear that a blow was on it, or one caught
    /// that it was clear.
    /// </summary>
    private static bool VanishesOnTheRun(SosariaCharacter character, Memory memory, bool scanned)
    {
        if (!scanned || character.Hidden || !IsSpellFighter(character) ||
            !SpellBook.CanCast(SpellBook.Invisibility, character.Skills.Magery.Value, character.Mana) ||
            ChaserOnRunner(character, memory) is not { } chaser)
        {
            return false;
        }

        ReadThreats(character, memory, chaser);

        if (!SpellBook.Fits(SpellBook.Invisibility, SafeCircleNow(character, memory)) ||
            !CastAid(character, memory, SpellKind.Invisibility, character))
        {
            return false;
        }

        if (LogsOnce(character, VanishLine, chaser))
        {
            logger.Information("{Name} goes invisible on the run from {Foe}", character.Name, chaser.Name);
        }

        return true;
    }

    /// <summary>A spell that helps, on itself, a friend, a pet or a ghost. The cast gap holds as for an attack.</summary>
    private static bool CastAid(SosariaCharacter character, Memory memory, SpellKind kind, Mobile target)
    {
        if (!BeginCast(character, memory, kind, target))
        {
            return false;
        }

        memory.NextCastAt = Core.TickCount + MageCastGapMs;
        return true;
    }

    private static bool StillWorthCasting(SpellKind kind, double magery, int mana, bool foeHeld, bool foePoisoned, SpellStock stock) =>
        SpellBook.CanCast(SpellBook.EntryOf(kind), magery, mana) && stock.Has(kind) &&
        !(kind == SpellKind.Paralyze && foeHeld) &&
        !(kind == SpellKind.Poison && foePoisoned);

    /// <summary>The best attack whose words finish before the next blow, or null.</summary>
    private static SpellKind? AttackThatFits(SosariaCharacter character, Memory memory, int distance, int safeCircle) =>
        SpellBook.PickAttack(
            character.Skills.Magery.Value,
            character.Mana,
            distance,
            safeCircle,
            Utility.RandomDouble(),
            memory.Kit.Stock
        );

    /// <summary>
    /// Nothing to cast this tick: hold the band for the stance. Too near, or a heal that needs
    /// more room, steps clear by the footwork rules, so once the grace runs out it stands where
    /// it is for the commit window; too far walks in; inside the band it stands. Only a cast
    /// ends the break-away (<see cref="CastAt"/>). Stepping out on its own clock, and ending
    /// the break-away whenever the band held for a moment, restarted the grace between every
    /// two casts, and mages ran the whole fight.
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
            return;
        }

        var held = foe.Paralyzed || foe.Frozen;

        if (distance < FootworkRules.BandMin(stance, held) || stance == CombatStance.Heal && careWantsRoom)
        {
            if (FootworkNow(memory, holds: false, stance) == Footwork.OpenGap)
            {
                StepOut(character, memory, foe);
            }
            else
            {
                character.Motor.Stop();
            }

            return;
        }

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
                FootworkRules.ArrowBreakMs(Core.AOS, SlowedByProtection(character))
            ) ||
            !SpellBook.CanCast(SpellBook.MagicArrow, character.Skills.Magery.Value, character.Mana) ||
            !memory.Kit.Stock.Has(SpellKind.MagicArrow) ||
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

        NoteTactic(character, memory, foe, kind, self);

        if (committed)
        {
            CastTally.NoteStood();

            // Pinned, a mage casts each Magic Arrow where it stands (a blow cannot break the
            // first circle before AOS); the tally counts every one, the log says it once a stand.
            NoteStand(character, memory, foe, stance, $"casts {kind}");
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

    /// <summary>
    /// Books a cast for the tactics: the first harmful one opens the fight, and Explosion owes
    /// its follow-up (<see cref="MageTacticsRules.BurstFollowUp"/>) on the mana its words leave;
    /// any other cast settles the debt.
    /// </summary>
    private static void NoteTactic(SosariaCharacter character, Memory memory, Mobile foe, SpellKind kind, bool self)
    {
        var tactics = TacticsOf(character, memory);
        var opening = !self && !tactics.Opened;
        tactics.Opened |= !self;
        tactics.FollowUp = kind == SpellKind.Explosion
            ? MageTacticsRules.BurstFollowUp(character.Skills.Magery.Value, character.Mana - SpellBook.Explosion.Mana, memory.Kit.Stock)
            : null;

        if (kind == SpellKind.Dispel && LogsOnce(character, DispelLine, foe))
        {
            logger.Information("{Name} casts Dispel on the summoned {Foe}", character.Name, foe.Name);
        }

        if (opening && kind == SpellKind.Paralyze && LogsOnce(character, OpenerLine, foe))
        {
            logger.Information("{Name} opens on {Foe} with Paralyze", character.Name, foe.Name);
        }

        if (tactics.FollowUp is { } follow && LogsOnce(character, BurstLine, foe))
        {
            logger.Information("{Name} starts a burst on {Foe}: Explosion, then {Spell}", character.Name, foe.Name, follow);
        }
    }

    /// <summary>
    /// Only a caster build fights and heals with spells: a mage, healer, bard or tamer with the
    /// magery and a spellbook. A fighter keeps magery to travel; its spells in a fight put its
    /// weapon in the pack at every cast (the engine clears the hands), and dexxers of the period
    /// fought with the weapon, bandages and potions.
    /// </summary>
    private static bool IsSpellFighter(SosariaCharacter character) =>
        character.Build?.Style == CombatStyle.Mage &&
        SpellBook.IsCaster(character.Skills.Magery.Value) && Spellbook.FindRegular(character) != null;

    /// <summary>The weapon in either hand, or null when the hands hold none.</summary>
    public static BaseWeapon HeldWeapon(Mobile mobile) =>
        mobile.FindItemOnLayer(Layer.OneHanded) as BaseWeapon ??
        mobile.FindItemOnLayer(Layer.TwoHanded) as BaseWeapon;

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

        return true;
    }

    /// <summary>
    /// Answers the target cursor of a spell that finished its words, cancels it when the
    /// target is gone, and counts a spell that ended with no cursor as broken by a blow.
    /// </summary>
    private static void ResolvePendingCast(SosariaCharacter character, Memory memory)
    {
        var target = memory.CastTarget;

        if (target != null)
        {
            if (character.Spell is Spell { State: SpellState.Sequencing } && character.Target is { } cursor)
            {
                memory.CastTarget = null;

                if (target.Deleted || target.Alive == SpellBook.RaisesDead(memory.CastKind) || target.Map != character.Map)
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

                // A broken Explosion lands nothing for its follow-up to land beside.
                if (memory.CastKind == SpellKind.Explosion)
                {
                    TacticsOf(character, memory).FollowUp = null;
                }
            }
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
            SpellKind.Invisibility => new InvisibilitySpell(caster),
            SpellKind.Resurrection => new ResurrectionSpell(caster),
            SpellKind.Dispel => new DispelSpell(caster),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}
