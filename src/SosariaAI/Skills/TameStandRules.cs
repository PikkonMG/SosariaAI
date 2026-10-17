using System;
using SosariaAI.Combat;

namespace SosariaAI.Skills;

/// <summary>What a tamer holds an angry beast with before its next try.</summary>
public enum QuarryHold
{
    None,
    Calm,
    Paralyze
}

/// <summary>
/// How a tamer stands through the tame of a beast that bites back, on the engine's own rules
/// (Skills/AnimalTaming.cs). A beast the engine marks as one that angers on a try
/// (CanAngerOnTame: the dragon, the white wyrm, the nightmare) refuses nineteen tries in
/// twenty: the engine gives the skill clock back at once and sets the beast on the tamer. That
/// is no refusal of the beast, so the tamer tries again from where it stands
/// (<see cref="Angered"/>, <see cref="AngerRetry"/>). A beast calmed by Peacemaking that
/// angers is calm again two seconds later three times in four; a beast paralyzed at any round
/// of a try keeps 86 parts in a hundred of its skills on the tame instead of 90, and the odds
/// of the try stay the same. So a bard-tamer calms the angry beast first and a mage-tamer
/// paralyzes it when it cannot (<see cref="HoldFor"/>). A tamer the beast has worn below
/// <see cref="StepBackHitsFraction"/> steps three to five tiles back and heals
/// (<see cref="StepsBack"/>, <see cref="StepsAway"/>) until it stands at the heal line again
/// (<see cref="Recovered"/>). A tamer with no bandage and no heal spell left goes home
/// (<see cref="HealsSelf"/>): a tamer rode into a dragon with none of either and died.
/// </summary>
public static class TameStandRules
{
    /// <summary>Below this share of its hits a tamer the beast is on steps back to heal.</summary>
    public const double StepBackHitsFraction = 0.6;

    /// <summary>A tamer stepping back stands still to heal once the beast is this far off.</summary>
    public const int StepBackMinTiles = 3;

    /// <summary>A tamer stepping back goes at most this far from the beast: still in sight of the next try.</summary>
    public const int StepBackMaxTiles = 5;

    /// <summary>After an anger the tamer tries again this soon: players tried every one to three seconds.</summary>
    public static readonly TimeSpan AngerRetry = TimeSpan.FromSeconds(2);

    /// <summary>
    /// True when a try the engine gave back was the beast's anger, not a refusal: a beast that
    /// angers on a try, now on the tamer, and still wild.
    /// </summary>
    public static bool Angered(bool angersOnTame, bool onTamer, bool controlled) => angersOnTame && onTamer && !controlled;

    /// <summary>
    /// The hold on a beast that angers on a try and is on the tamer now, and is neither calm nor
    /// paralyzed: a song when the tamer can calm it (no cost to the pet), else Paralyze when the
    /// tamer can cast it (the pet keeps a little less of its skills).
    /// </summary>
    public static QuarryHold HoldFor(bool angersOnTame, bool onTamer, bool held, bool canCalm, bool canParalyze) =>
        !angersOnTame || !onTamer || held ? QuarryHold.None
        : canCalm ? QuarryHold.Calm
        : canParalyze ? QuarryHold.Paralyze
        : QuarryHold.None;

    /// <summary>True when the beast is on the tamer, free to hit it, and its hits are under <see cref="StepBackHitsFraction"/>.</summary>
    public static bool StepsBack(bool onTamer, bool held, double hitsFraction) =>
        onTamer && !held && hitsFraction < StepBackHitsFraction;

    /// <summary>The steps back from a beast this far off: out to <see cref="StepBackMaxTiles"/>, none from <see cref="StepBackMinTiles"/> on.</summary>
    public static int StepsAway(int distance) => distance < StepBackMinTiles ? StepBackMaxTiles - Math.Max(0, distance) : 0;

    /// <summary>A tamer that stepped back goes back to the beast at the heal line it tops up to out of a fight.</summary>
    public static bool Recovered(double hitsFraction) => hitsFraction >= SelfCareRules.RestHealFraction;

    /// <summary>
    /// True when the tamer can heal itself on the trip: a bandage and some Healing, or Heal or
    /// Greater Heal in its book with the reagents in its pack (<paramref name="healInHand"/>,
    /// <paramref name="greaterHealInHand"/>) and the Magery for it.
    /// </summary>
    public static bool HealsSelf(double healing, bool hasBandage, double magery, bool healInHand, bool greaterHealInHand) =>
        healing > SelfCareRules.NoSkill && hasBandage ||
        healInHand && magery >= SpellBook.Heal.MinMagery ||
        greaterHealInHand && magery >= SpellBook.GreaterHeal.MinMagery;
}
