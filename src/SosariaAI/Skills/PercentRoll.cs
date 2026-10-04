using System;

namespace SosariaAI.Skills;

/// <summary>A roll out of a hundred, as the skills' odds are written. Pure.</summary>
public static class PercentRoll
{
    public const int Scale = 100;

    /// <summary>True when <paramref name="roll"/>, any whole number, lands under <paramref name="percent"/> out of <see cref="Scale"/>.</summary>
    public static bool Under(int roll, int percent) => Math.Abs(roll % Scale) < percent;
}
