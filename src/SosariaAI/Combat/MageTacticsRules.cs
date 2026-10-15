using System;

namespace SosariaAI.Combat;

/// <summary>How a caster that is losing gets out: by Recall, by Invisibility, or by holding still while hidden.</summary>
public enum EscapeMove
{
    None,
    Recall,
    Vanish,
    StayHidden
}

/// <summary>
/// How a 1999 mage fought a monster beyond picking the next attack (<see cref="SpellBook"/>):
/// it froze a melee foe with Paralyze before anything else and cast from range; with the mana
/// for both it said Explosion and then Energy Bolt or Flame Strike right after, so the delayed
/// blast and the bolt landed together; and when the fight turned it went invisible or
/// recalled out before its hits ran low, and meditated after. The burst is timed by the
/// engine's own clocks for the running era: the Explosion blast waits 2.5 seconds before AOS
/// and 3 after (ExplosionSpell.InternalTimer), and the next words wait out the cast recovery
/// (Spell.GetCastRecovery: 0.75 seconds before AOS, 6 / 4 seconds from AOS on). Pure.
/// </summary>
public static class MageTacticsRules
{
    /// <summary>The Explosion blast lands this long after its cursor before AOS.</summary>
    public const int PreAosExplosionDelayMs = 2500;

    /// <summary>The Explosion blast lands this long after its cursor from AOS on.</summary>
    public const int AosExplosionDelayMs = 3000;

    /// <summary>Before AOS the next spell may begin this long after the last one's words end (Spell.NextSpellDelay).</summary>
    public const int PreAosRecoveryMs = 750;

    /// <summary>From AOS on the cast recovery is six quarter seconds with no faster cast recovery (Spell.CastRecoveryBase).</summary>
    public const int AosRecoveryMs = 1500;

    /// <summary>Two blows that land this close together land as one burst.</summary>
    public const int BurstWindowMs = 750;

    /// <summary>A burst is not spent on a foe this close to falling: one attack finishes it.</summary>
    public const double BurstFoeHitsFraction = 0.25;

    /// <summary>A caster gets out this far above the hit line where it would run (<see cref="RetreatRules.Line"/>).</summary>
    public const double EscapeMargin = 0.15;

    /// <summary>A caster losing the trade of blows gets out below this share of its hits.</summary>
    public const double LosingEscapeFraction = 0.6;

    /// <summary>
    /// The first spell of the fight on a free melee foe is Paralyze, when it can be cast and
    /// fits the circle the footwork can buy. A held foe is walked away from
    /// (<see cref="CombatStance.ParalyzeThenKite"/>), so the rest is cast from range.
    /// </summary>
    public static bool OpensWithParalyze(bool opened, CombatStyle foeStyle, bool foeHeld, bool canParalyze, int planCircle) =>
        !opened && foeStyle == CombatStyle.Melee && !foeHeld && canParalyze && SpellBook.Fits(SpellBook.Paralyze, planCircle);

    /// <summary>The spell that follows Explosion: Flame Strike when the mana and Magery allow, else Energy Bolt, else none.</summary>
    public static SpellKind? BurstFollowUp(double magery, int manaAfterExplosion) =>
        SpellBook.CanCast(SpellBook.FlameStrike, magery, manaAfterExplosion) ? SpellKind.FlameStrike
        : SpellBook.CanCast(SpellBook.EnergyBolt, magery, manaAfterExplosion) ? SpellKind.EnergyBolt
        : null;

    /// <summary>How long after its cursor the Explosion blast lands in this era.</summary>
    public static int ExplosionDelayMs(bool aos) => aos ? AosExplosionDelayMs : PreAosExplosionDelayMs;

    /// <summary>How long after one spell's words end the next may begin in this era.</summary>
    public static int RecoveryMs(bool aos) => aos ? AosRecoveryMs : PreAosRecoveryMs;

    /// <summary>
    /// True when a spell of <paramref name="followCircle"/> said right after Explosion lands
    /// within <see cref="BurstWindowMs"/> of the blast: its words and the cast recovery against
    /// the blast's delay. Before AOS an Energy Bolt lands 250 ms after the blast and a Flame
    /// Strike 500 ms after; a Magic Arrow would land a full second before it.
    /// </summary>
    public static bool LandsTogether(int followCircle, bool aos) =>
        Math.Abs(CastTiming.CastDelayMs(followCircle, slowedByProtection: false) + RecoveryMs(aos) - ExplosionDelayMs(aos)) <=
        BurstWindowMs;

    /// <summary>
    /// A burst starts when Explosion and its follow-up can both be cast on the mana now, both
    /// fit the circle the footwork can buy, the two land together in this era, and the foe is
    /// not about to fall anyway.
    /// </summary>
    public static bool StartsBurst(double magery, int mana, int planCircle, double foeHitsFraction, bool aos) =>
        foeHitsFraction > BurstFoeHitsFraction &&
        SpellBook.CanCast(SpellBook.Explosion, magery, mana) &&
        SpellBook.Fits(SpellBook.Explosion, planCircle) &&
        BurstFollowUp(magery, mana - SpellBook.Explosion.Mana) is { } follow &&
        SpellBook.Fits(SpellBook.EntryOf(follow), planCircle) &&
        LandsTogether(SpellBook.EntryOf(follow).Circle, aos);

    /// <summary>
    /// A caster with something on it is losing: its hits are within <see cref="EscapeMargin"/>
    /// of the line where it would run, or the trade of blows goes against it and its hits are
    /// under <see cref="LosingEscapeFraction"/>. It gets out before the run would start, while
    /// the words still have time to finish.
    /// </summary>
    public static bool Losing(double hitsFraction, double retreatLine, FightOutlook outlook, int attackersOnSelf) =>
        attackersOnSelf > 0 &&
        (hitsFraction < retreatLine + EscapeMargin ||
         outlook == FightOutlook.Losing && hitsFraction < LosingEscapeFraction);

    /// <summary>
    /// The way out: a hidden caster recalls when the recall-out path allows it and else holds
    /// still, since a step or a spell shows it again; a losing one recalls when it may, else
    /// goes invisible when it can; otherwise nothing.
    /// </summary>
    public static EscapeMove Escape(bool hidden, bool losing, bool recallAllowed, bool canVanish) =>
        hidden ? recallAllowed ? EscapeMove.Recall : EscapeMove.StayHidden
        : !losing ? EscapeMove.None
        : recallAllowed ? EscapeMove.Recall
        : canVanish ? EscapeMove.Vanish
        : EscapeMove.None;
}
