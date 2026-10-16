using Server;

namespace SosariaAI.Common;

/// <summary>How hurt and how winded a mobile is. Pure.</summary>
public static class Vitals
{
    /// <summary>The share a mobile with no hit point maximum counts as: unhurt.</summary>
    public const double FullHits = 1.0;

    /// <summary>Hits over maximum hits, or <see cref="FullHits"/> when the maximum is unknown.</summary>
    public static double HitsFraction(Mobile mobile) =>
        mobile is { HitsMax: > 0 } ? (double)mobile.Hits / mobile.HitsMax : FullHits;

    /// <summary>Stamina over maximum stamina, or <see cref="FullHits"/> when the maximum is unknown.</summary>
    public static double StaminaFraction(Mobile mobile) =>
        mobile is { StamMax: > 0 } ? (double)mobile.Stam / mobile.StamMax : FullHits;
}
