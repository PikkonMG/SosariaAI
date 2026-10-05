using SosariaAI.Skills;

namespace SosariaAI.Combat;

/// <summary>
/// A red's recall out of a fight it runs from, as players did: once the runner has broken
/// contact it says the words home to the Den. The words of Recall take the fourth circle's
/// cast delay, and a blow that lands before they end breaks them (Spell.OnCasterHurt), so the
/// runner casts only when every blow on it lands after the words. No recall in the heat of
/// battle (<see cref="TravelHeat"/>: a blow on or from a player in the last half minute) or while
/// fighting; the runner keeps running, hides if it can (<see cref="HidesOut"/>), and tries again
/// once the heat cools. A refusal that stays (no rune home, no scroll, charge or reagents) ends
/// the tries for the fight. Pure.
/// </summary>
public static class RecallOutRules
{
    /// <summary>Recall is a fourth circle spell (RecallSpell).</summary>
    public const int RecallCircle = 4;

    /// <summary>A recall out pays from any distance: the run is the reason, not the length of the trip.</summary>
    public const int MinTripTiles = RecallRules.NoRoadMinTripTiles;

    /// <summary>True when every blow on the runner lands after the words of a recall end.</summary>
    public static bool ContactBroken(long soonestBlowMs, bool slowedByProtection) =>
        CastTiming.Holds(CastTiming.CastDelayMs(RecallCircle, slowedByProtection), soonestBlowMs);

    /// <summary>A runner hides when its Hiding reaches this: below it the tries only reveal it.</summary>
    public const double HideOutMinHiding = 30;

    /// <summary>True when a runner tries to hide: out of its chasers' sight, or waiting out the heat of battle.</summary>
    public static bool HidesOut(double hiding) => hiding >= HideOutMinHiding;

    /// <summary>True when a recall refused for <paramref name="whyNot"/> is worth another try later in the run.</summary>
    public static bool TriesAgain(string whyNot) => whyNot != null && TravelSpells.Passes(whyNot);
}
