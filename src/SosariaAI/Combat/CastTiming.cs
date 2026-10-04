using System;

namespace SosariaAI.Combat;

/// <summary>
/// The engine's clock for a cast against the clock of the blows coming in. A player caster
/// loses any spell a blow lands on while the words are still being said (Spell.OnCasterHurt);
/// once the words are done and the target cursor is up, a blow ruins nothing. So a cast holds
/// when the soonest blow lands after its cast delay. Pre-AOS a first circle spell shrugs off
/// the blow altogether (Spell.Disturb).
/// </summary>
public static class CastTiming
{
    /// <summary>One cast tick (Spell.CastDelaySecondsPerTick).</summary>
    public const int CastTickMs = 250;

    /// <summary>A magery spell takes three ticks at the first circle and one more per circle (MagerySpell.CastDelayBase).</summary>
    public const int CastBaseTicks = 2;

    /// <summary>From UOR on, Protection costs two points of cast speed (Spell.GetCastDelay).</summary>
    public const int ProtectionSlowMs = 2 * CastTickMs;

    /// <summary>Slack for the swing and step clocks landing a little early.</summary>
    public const int BlowMarginMs = 150;

    /// <summary>The blow time of a foe that cannot reach or swing: paralyzed or frozen.</summary>
    public const long NoBlowMs = long.MaxValue;

    public const int NoCircle = 0;
    public const int FirstCircle = 1;
    public const int TopCircle = 8;

    /// <summary>How long the words of a spell of this circle take.</summary>
    public static int CastDelayMs(int circle, bool slowedByProtection) =>
        (CastBaseTicks + circle) * CastTickMs + (slowedByProtection ? ProtectionSlowMs : 0);

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

    /// <summary>True when an action that needs <paramref name="needMs"/> of peace finishes before the soonest blow.</summary>
    public static bool Holds(int needMs, long soonestBlowMs) =>
        soonestBlowMs == NoBlowMs || soonestBlowMs - BlowMarginMs >= needMs;

    /// <summary>
    /// The highest circle whose words finish before the soonest blow. When the first circle
    /// shrugs off a blow (pre-AOS) it is always safe; otherwise nothing may be safe.
    /// </summary>
    public static int SafeCircle(long soonestBlowMs, bool slowedByProtection, bool firstCircleShrugsHits)
    {
        var safe = NoCircle;

        for (var circle = FirstCircle; circle <= TopCircle; circle++)
        {
            if (!Holds(CastDelayMs(circle, slowedByProtection), soonestBlowMs))
            {
                break;
            }

            safe = circle;
        }

        return firstCircleShrugsHits ? Math.Max(safe, FirstCircle) : safe;
    }
}
