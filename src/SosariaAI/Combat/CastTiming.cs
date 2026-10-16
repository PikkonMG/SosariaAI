using System;

namespace SosariaAI.Combat;

/// <summary>
/// The engine's clock for a cast against the clock of the blows coming in. A player caster
/// loses any spell a blow lands on while the words are still being said (Spell.OnCasterHurt);
/// once the words are done and the target cursor is up, a blow ruins nothing. So a cast holds
/// when the soonest blow lands after its cast delay. Pre-AOS a first circle spell shrugs off
/// the blow altogether (Spell.Disturb), and the cast delay is one tick shorter and is not
/// slowed by Protection (MagerySpell.GetCastDelay).
/// </summary>
public static class CastTiming
{
    /// <summary>One cast tick (Spell.CastDelaySecondsPerTick).</summary>
    public const int CastTickMs = 250;

    /// <summary>Before AOS a magery spell takes two ticks at the first circle and one more per circle (MagerySpell.GetCastDelay).</summary>
    public const int PreAosCastBaseTicks = 1;

    /// <summary>From AOS on a magery spell takes three ticks at the first circle and one more per circle (MagerySpell.CastDelayBase).</summary>
    public const int AosCastBaseTicks = 2;

    /// <summary>From AOS on, Protection costs two points of cast speed (Spell.GetCastDelay).</summary>
    public const int ProtectionSlowMs = 2 * CastTickMs;

    /// <summary>Slack for the swing and step clocks landing a little early.</summary>
    public const int BlowMarginMs = 150;

    /// <summary>The blow time of a foe that cannot reach or swing: paralyzed or frozen.</summary>
    public const long NoBlowMs = long.MaxValue;

    public const int NoCircle = 0;
    public const int FirstCircle = 1;
    public const int TopCircle = 8;

    /// <summary>
    /// How long the words of a spell of this circle take in this era. Before AOS the engine
    /// returns the bare delay, so Protection slows nothing.
    /// </summary>
    public static int CastDelayMs(int circle, bool aos, bool slowedByProtection) =>
        aos
            ? (AosCastBaseTicks + circle) * CastTickMs + (slowedByProtection ? ProtectionSlowMs : 0)
            : (PreAosCastBaseTicks + circle) * CastTickMs;

    /// <summary>
    /// When this foe can land its next blow: after it walks into its weapon reach, and not
    /// before its swing clock comes up again. A held foe lands nothing.
    /// </summary>
    public static long BlowInMs(int distance, int reach, int stepMs, long swingReadyInMs, bool held)
    {
        if (held)
        {
            return NoBlowMs;
        }

        var walkMs = (long)Math.Max(0, distance - reach) * Math.Max(0, stepMs);
        return Math.Max(walkMs, Math.Max(0, swingReadyInMs));
    }

    /// <summary>
    /// When a foe that fights with spells, standing off its reach, can land its next blow: when
    /// its words end, if it is casting; while it is not casting, nothing is on its way. It does
    /// not walk in to punch, so it is not read as a fighter that will: every mage within a few
    /// tiles of a mounted mage read as a blow in a third of a second, and two mages traded Magic
    /// Arrows and nothing else. Whether it breaks a long spell with an arrow is its own choice,
    /// the gamble a player takes on every big cast.
    /// </summary>
    public static long SpellBlowInMs(long castLeftMs, bool held) =>
        held || castLeftMs <= 0 ? NoBlowMs : castLeftMs;

    /// <summary>True when an action that needs <paramref name="needMs"/> of peace finishes before the soonest blow.</summary>
    public static bool Holds(int needMs, long soonestBlowMs) =>
        soonestBlowMs == NoBlowMs || soonestBlowMs - BlowMarginMs >= needMs;

    /// <summary>
    /// The highest circle whose words finish before the soonest blow in this era. Before AOS
    /// the first circle shrugs off a blow, so it is always safe; from AOS on nothing may be safe.
    /// </summary>
    public static int SafeCircle(long soonestBlowMs, bool aos, bool slowedByProtection)
    {
        var safe = NoCircle;

        for (var circle = FirstCircle; circle <= TopCircle; circle++)
        {
            if (!Holds(CastDelayMs(circle, aos, slowedByProtection), soonestBlowMs))
            {
                break;
            }

            safe = circle;
        }

        return aos ? safe : Math.Max(safe, FirstCircle);
    }
}
