namespace SosariaAI.Behaviour;

/// <summary>
/// When a PK hunter (<see cref="PkHunterRules"/>) rides out, the 1999 way: the anti-PK guilds
/// answered word of a red, a murder or a sighting, and spent the time between calls on their
/// other work. A hunter rides for a fresh red sighting it can get to, as a lawful fighter rides
/// for a fresh murder (<see cref="ConflictRules.BlueRides"/>), and with no word only in its
/// standing phases (<see cref="StandingPatrolShare"/>), so a few hunters still patrol the hot
/// spots. Live, every hunter phase rode out with or without word: 428 rides in 9.4 hours to the
/// hot spots and the Den, 164 of them to the Britain graveyard. Pure.
/// </summary>
public static class RedSightingRules
{
    /// <summary>The share of a hunter's phases it patrols the hot spots with no word of a red.</summary>
    public const double StandingPatrolShare = 0.1;

    /// <summary>
    /// A red sighting draws at most this many hunters, a pair: a murder draws more
    /// (<see cref="ConflictRules.MaxAnswerers"/>), since a sighting is only a red passing by.
    /// </summary>
    public const int SightingRiders = 2;

    private const int StandingSalt = 479;

    /// <summary>A sighting fewer than <see cref="SightingRiders"/> hunters ride to already draws one more.</summary>
    public static bool DrawsHunter(int answerers) => answerers < SightingRiders;

    /// <summary>This phase is one of the hunter's standing patrols (<see cref="StandingPatrolShare"/>), from the phase seed.</summary>
    public static bool PatrolsWithoutWord(int phaseSeed) => ChoiceSeed.Unit(phaseSeed, StandingSalt) < StandingPatrolShare;

    /// <summary>A hunter rides out for a murder or a red sighting to answer, or on a standing patrol.</summary>
    public static bool HunterRides(bool wordToAnswer, bool standingPhase) => wordToAnswer || standingPhase;
}
