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
using SosariaAI.Navigation;
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
    private const string CrimeCastLine = "crime cast";
    private const string ReflectLine = "reflect";
    private const string WallOffLine = "wall off";
    private const string BurstCallLine = "burst call";
    private const string ChaseLine = "chase";

    // Weak keys: a deleted character's tactics go with it, as its fight memory does.
    private static readonly ConditionalWeakTable<SosariaCharacter, CasterTactics> Tactics = new();

    /// <summary>
    /// A caster's tactics for the fight it is in, beside the fight memory: whether it opened,
    /// the spell its burst still owes, when the fight began and since when it stands hidden,
    /// the foe it watches for a cure and whether it saw one, and its side steps since the last
    /// cast. A new fight (<see cref="Memory.FightId"/>) starts them afresh.
    /// </summary>
    private sealed class CasterTactics
    {
        public int FightId = -1;
        public long FightSince;
        public bool Opened;
        public SpellKind? FollowUp;
        public long HiddenSince;
        public long NextRecallLookAt;
        public Mobile CureWatch;
        public bool CureWatchPoisoned;
        public bool CureSeen;
        public int StrafeSteps;
        public int StrafeBudget;
        public bool StrafeLeft;
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
            tactics.CureWatch = null;
            tactics.CureWatchPoisoned = false;
            tactics.CureSeen = false;
            NewStrafeGap(tactics);
        }

        return tactics;
    }

    /// <summary>A new gap between casts: no side step taken yet, and a fresh budget of one or two (<see cref="FootworkRules.Strafes"/>).</summary>
    private static void NewStrafeGap(CasterTactics tactics)
    {
        tactics.StrafeSteps = 0;
        tactics.StrafeBudget = Utility.RandomMinMax(FootworkRules.MinStrafeSteps, FootworkRules.MaxStrafeSteps);
    }

    /// <summary>
    /// Watches the foe for a cure: words of Cure or Arch Cure, or poison gone from it since the
    /// last look (a spell or a potion). The watch starts afresh on another foe.
    /// </summary>
    private static void WatchForCure(CasterTactics tactics, Mobile foe)
    {
        if (tactics.CureWatch != foe)
        {
            tactics.CureWatch = foe;
            tactics.CureWatchPoisoned = false;
            tactics.CureSeen = false;
        }

        tactics.CureSeen |= foe.Spell is CureSpell or ArchCureSpell || tactics.CureWatchPoisoned && !foe.Poisoned;
        tactics.CureWatchPoisoned = foe.Poisoned;
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
    /// tactics call for (<see cref="TacticSpell"/>), nothing while it waits for its side's burst
    /// call, else the planned attack, picked up to the circle <see cref="FootworkRules.PlanCircle"/>
    /// gives. A foe running low (<see cref="FootworkRules.RunsLow"/>) caps the circle at the short
    /// words of the chase (<see cref="MageTacticsRules.ChaseCap"/>). A foe in the band with
    /// nothing that fits now gets a plan for open ground, so the footwork steps for the room. The
    /// plan stands until it is cast, cannot be, or no longer fits the plan circle.
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
        var tactics = TacticsOf(character, memory);
        WatchForCure(tactics, foe);

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

        var runsLow = RunsLow(foe, character);
        var openCircle = MageTacticsRules.ChaseCap(CastTiming.TopCircle, runsLow);
        var planCircle = MageTacticsRules.ChaseCap(
            FootworkRules.PlanCircle(distance, FootworkRules.BandMin(stance, held), MageKeepMax, safeCircle),
            runsLow
        );

        if (runsLow && LogsOnce(character, ChaseLine, foe))
        {
            logger.Information("{Name} chases {Foe} running low with short words", character.Name, foe.Name);
        }

        if (TacticSpell(character, memory, foe, held, planCircle, out var waitsForCall) is { } tactic)
        {
            return tactic;
        }

        if (waitsForCall)
        {
            return null;
        }

        if (memory.Planned is { } planned && StillWorthCasting(planned, magery, mana, held, foe.Poisoned, memory.Kit.Stock) &&
            SpellBook.Fits(SpellBook.EntryOf(planned), planCircle))
        {
            return planned;
        }

        var poisonFirst = MageTacticsRules.PoisonFirst(People.IsLivingPlayer(foe), foe.Poisoned, tactics.CureSeen);
        memory.Planned = PlanSpell(character, foe, distance, planCircle, memory.Kit.Stock, runsLow, poisonFirst) ??
                         PlanSpell(character, foe, distance, openCircle, memory.Kit.Stock, runsLow, poisonFirst);
        return memory.Planned;
    }

    /// <summary>
    /// A spell at or under <paramref name="circle"/>: Poison first on a person not seen to cure
    /// (<paramref name="poisonFirst"/>), Paralyze or Poison by chance, else a pick among the
    /// strongest attacks (<see cref="SpellBook"/>); a foe <paramref name="runsLow"/> gets only
    /// the attack. Null when nothing can be cast.
    /// </summary>
    private static SpellKind? PlanSpell(
        SosariaCharacter character,
        Mobile foe,
        int distance,
        int circle,
        SpellStock stock,
        bool runsLow,
        bool poisonFirst
    )
    {
        var magery = character.Skills.Magery.Value;
        var mana = character.Mana;
        var utility = runsLow
            ? null
            : SpellBook.PickUtility(
                magery,
                mana,
                distance,
                circle,
                foe.Paralyzed || foe.Frozen,
                foe.Poisoned,
                poisonFirst,
                Utility.RandomDouble(),
                Utility.RandomDouble(),
                stock
            );

        return utility ?? SpellBook.PickAttack(magery, mana, distance, circle, Utility.RandomDouble(), stock);
    }

    /// <summary>
    /// The spell a mage's tactics call for (<see cref="MageTacticsRules"/>): Dispel on a summoned
    /// foe it will likely unmake, the burst's follow-up while it is owed, Magic Reflection before
    /// the first blow on a person, the opener (<see cref="MageTacticsRules.Opener"/>), or
    /// Explosion to start a burst when the mana holds both spells, timed to land with its side's
    /// call (<see cref="GroupBurst"/>); null when none does. <paramref name="waitsForCall"/> is
    /// true when the burst waits for that call's window.
    /// </summary>
    private static SpellKind? TacticSpell(
        SosariaCharacter character,
        Memory memory,
        Mobile foe,
        bool held,
        int planCircle,
        out bool waitsForCall
    )
    {
        var tactics = TacticsOf(character, memory);
        var magery = character.Skills.Magery.Value;
        var mana = character.Mana;
        var stock = memory.Kit.Stock;
        var person = People.IsLivingPlayer(foe);
        waitsForCall = false;

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

        if (MageTacticsRules.RaisesReflect(
                !Core.AOS,
                person,
                tactics.Opened,
                character.MagicDamageAbsorb > 0,
                stock.Has(SpellKind.MagicReflect) && SpellBook.CanCast(SpellBook.MagicReflect, magery, mana),
                planCircle
            ))
        {
            return SpellKind.MagicReflect;
        }

        if (MageTacticsRules.Opener(
                tactics.Opened,
                person,
                StyleOfFoe(foe),
                held,
                stock.Has(SpellKind.Paralyze) && SpellBook.CanCast(SpellBook.Paralyze, magery, mana),
                stock.Has(SpellKind.Weaken) && SpellBook.CanCast(SpellBook.Weaken, magery, mana),
                stock.Has(SpellKind.MagicArrow) && SpellBook.CanCast(SpellBook.MagicArrow, magery, mana),
                planCircle,
                Utility.RandomDouble()
            ) is { } opener)
        {
            return opener;
        }

        if (!MageTacticsRules.StartsBurst(magery, mana, planCircle, Vitals.HitsFraction(foe), Core.AOS, stock))
        {
            return null;
        }

        waitsForCall = CallFor(character, foe, out _) == BurstCall.Early;
        return waitsForCall ? null : SpellKind.Explosion;
    }

    /// <summary>
    /// How an Explosion said now, landing at <paramref name="landsAt"/>, meets the burst call of
    /// this caster's side on <paramref name="foe"/> (<see cref="MageTacticsRules.Against"/>);
    /// <see cref="BurstCall.None"/> when there is no call.
    /// </summary>
    private static BurstCall CallFor(SosariaCharacter character, Mobile foe, out long landsAt)
    {
        landsAt = Core.TickCount + MageTacticsRules.ExplosionLandMs(Core.AOS);
        return GroupBurst.TryCall(character, foe, out var callAt) ? MageTacticsRules.Against(landsAt, callAt) : BurstCall.None;
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
            RetreatRules.Line(BaseLineOf(character), NerveOf(character, memory), attackers, CasterAttackers(memory)),
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

    /// <summary>
    /// A caster on the run from a foe that fights up close says Wall of Stone between them once
    /// it closes in (<see cref="MageTacticsRules.WallsOff"/>): the wall stands across the way for
    /// ten seconds and the chaser walks round it. The cursor goes on the tile toward the chaser
    /// when the words end (<see cref="WallSpot"/>). Read on the scan pace, from this scan's chaser.
    /// </summary>
    private static bool WallsOffChaser(SosariaCharacter character, Memory memory, bool scanned)
    {
        if (!scanned || character.Hidden || !IsSpellFighter(character) || !CastReady(character, memory) ||
            ChaserOnRunner(character, memory) is not { } chaser || StyleOfFoe(chaser) != CombatStyle.Melee)
        {
            return false;
        }

        ReadThreats(character, memory, chaser);

        if (!MageTacticsRules.WallsOff(
                chaserFightsUpClose: true,
                NavMetric.Chebyshev(character.Location, chaser.Location),
                SafeCircleNow(character, memory)
            ) ||
            !Readies(character, SpellKind.WallOfStone) || !CastAid(character, memory, SpellKind.WallOfStone, chaser))
        {
            return false;
        }

        if (LogsOnce(character, WallOffLine, chaser))
        {
            logger.Information("{Name} puts a Wall of Stone between itself and {Foe} on the run", character.Name, chaser.Name);
        }

        return true;
    }

    /// <summary>
    /// The tile for a wall between the caster and <paramref name="chaser"/>: one step from the
    /// caster toward it, while the chaser still stands <see cref="MageTacticsRules.WallMinTiles"/>
    /// or more off; null once it is too close for a wall between.
    /// </summary>
    private static Point3D? WallSpot(Mobile caster, Mobile chaser)
    {
        if (NavMetric.Chebyshev(caster.Location, chaser.Location) < MageTacticsRules.WallMinTiles)
        {
            return null;
        }

        var spot = caster.Location;
        Server.Movement.Movement.Offset(caster.GetDirectionTo(chaser), ref spot);
        return spot;
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
        !(kind is SpellKind.Paralyze or SpellKind.Weaken && foeHeld) &&
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
    /// it is for the commit window; too far walks in; inside the band it takes a side step or
    /// two (<see cref="Strafe"/>) and then stands. Only a cast ends the break-away
    /// (<see cref="CastAt"/>). Stepping out on its own clock, and ending the break-away whenever
    /// the band held for a moment, restarted the grace between every two casts, and mages ran
    /// the whole fight.
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
            return;
        }

        Strafe(character, memory, foe, stance, FootworkRules.BandMin(stance, held));
    }

    /// <summary>
    /// A side step inside the band between casts (<see cref="FootworkRules.Strafes"/>): a quarter
    /// turn off the way to the foe, the side of the last step first, onto a tile that keeps the
    /// band (<see cref="FootworkRules.KeepsBand"/>) and is no nearer the hostiles not on it yet.
    /// A mage that stood still between its words was the easy target of every swing and arrow.
    /// </summary>
    private static void Strafe(SosariaCharacter character, Memory memory, Mobile foe, CombatStance stance, int bandMin)
    {
        var tactics = TacticsOf(character, memory);

        if (!FootworkRules.Strafes(stance, tactics.StrafeSteps, tactics.StrafeBudget) || !CanWalk(character) ||
            !character.Motor.CanMoveNow)
        {
            return;
        }

        var toward = (int)(character.GetDirectionTo(foe) & Direction.Mask);
        var idleHere = EscapeRules.IdleFoesNear(memory.IdleFoes, character.Location);

        ReadOnlySpan<bool> sides = [tactics.StrafeLeft, !tactics.StrafeLeft];

        foreach (var left in sides)
        {
            var direction = (Direction)FootworkRules.SideStep(toward, left);
            var spot = character.Location;
            Server.Movement.Movement.Offset(direction, ref spot);

            if (!FootworkRules.KeepsBand(NavMetric.Chebyshev(spot, foe.Location), bandMin, MageKeepMax) ||
                EscapeRules.IdleFoesNear(memory.IdleFoes, spot) > idleHere || !character.Motor.DoMove(direction))
            {
                continue;
            }

            tactics.StrafeSteps++;
            tactics.StrafeLeft = left;
            return;
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
    /// Casts at the foe, or on itself for the ward or Magic Reflection. A cast bought by steps out of reach, and one
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
        var self = SpellBook.OnSelf(kind, Core.UOR);

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
    /// any other cast settles the debt. An Explosion that does not land with its side's call
    /// opens a call of its own for its side to land beside (<see cref="GroupBurst.Join"/>).
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

        if (opening && LogsOnce(character, OpenerLine, foe))
        {
            logger.Information("{Name} opens on {Foe} with {Spell}", character.Name, foe.Name, kind);
        }

        if (kind == SpellKind.MagicReflect && LogsOnce(character, ReflectLine, foe))
        {
            logger.Information("{Name} puts up Magic Reflection before it fights {Foe}", character.Name, foe.Name);
        }

        if (kind == SpellKind.Explosion)
        {
            NoteBurstCall(character, foe);
        }

        if (tactics.FollowUp is { } follow && LogsOnce(character, BurstLine, foe))
        {
            logger.Information("{Name} starts a burst on {Foe}: Explosion, then {Spell}", character.Name, foe.Name, follow);
        }
    }

    /// <summary>An Explosion just begun lands with its side's call, or opens one at its own landing (<see cref="GroupBurst"/>).</summary>
    private static void NoteBurstCall(SosariaCharacter character, Mobile foe)
    {
        if (CallFor(character, foe, out var landsAt) == BurstCall.Lands)
        {
            if (LogsOnce(character, BurstCallLine, foe))
            {
                logger.Information("{Name} times its Explosion on {Foe} to land with its side's burst", character.Name, foe.Name);
            }

            return;
        }

        GroupBurst.Join(character, foe, landsAt);
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

        var held = HeldWeapon(character);

        if (!spell.Cast())
        {
            return false;
        }

        NoteWeaponBeforeCast(character, held);
        NewStrafeGap(TacticsOf(character, memory));
        memory.CastTarget = target;
        memory.CastKind = kind;
        memory.CastWasCrime = IsCrime(character, kind, target);

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
                else if (SpellBook.TargetsGround(memory.CastKind))
                {
                    if (WallSpot(character, target) is { } spot)
                    {
                        cursor.Invoke(character, new LandTarget(spot, character.Map));
                    }
                    else
                    {
                        cursor.Cancel(character, TargetCancelType.Canceled);
                    }
                }
                else if (LawfulRules.BecameCrime(memory.CastWasCrime, IsCrime(character, memory.CastKind, target)))
                {
                    cursor.Cancel(character, TargetCancelType.Canceled);

                    if (LogsOnce(character, CrimeCastLine, target))
                    {
                        logger.Information(
                            "{Name} lets its {Spell} at {Target} go: it would land as a crime now",
                            character.Name,
                            memory.CastKind,
                            target.Name
                        );
                    }
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

    /// <summary>
    /// The spell on this target is a crime now: a blow on an innocent, or help to a gray or red.
    /// A spell on the ground touches no one.
    /// </summary>
    private static bool IsCrime(SosariaCharacter character, SpellKind kind, Mobile target) =>
        target != null && target != character && !SpellBook.TargetsGround(kind) &&
        (SpellBook.IsHarmful(kind) ? character.IsHarmfulCriminal(target) : character.IsBeneficialCriminal(target));

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
            SpellKind.Weaken => new WeakenSpell(caster),
            SpellKind.MagicReflect => new MagicReflectSpell(caster),
            SpellKind.WallOfStone => new WallOfStoneSpell(caster),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}
