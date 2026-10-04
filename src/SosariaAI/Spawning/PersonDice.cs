namespace SosariaAI.Spawning;

/// <summary>
/// Seeded dice for one person. <see cref="WorkSites.StableRoll"/> adds the salt in a
/// straight line, so two rolls of one person differ by a fixed step and a tier roll
/// would follow the class roll. This mixes the id and the salt so every purpose rolls
/// on its own, and the same id always rolls the same numbers after a reboot.
/// </summary>
public static class PersonDice
{
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
    private const ulong MixFirst = 0xBF58476D1CE4E5B9UL;
    private const ulong MixSecond = 0x94D049BB133111EBUL;
    private const int FirstShift = 30;
    private const int SecondShift = 27;
    private const int FinalShift = 31;
    private const int PercentSides = 100;

    /// <summary>A number in 0 to <paramref name="sides"/> - 1. One side or fewer always gives 0.</summary>
    public static int Roll(string uniqueId, int salt, int sides)
    {
        if (sides <= 1)
        {
            return 0;
        }

        var mixed = (ulong)WorkSites.StableIndex(uniqueId) * GoldenGamma ^ (ulong)salt * MixFirst;
        mixed = (mixed ^ (mixed >> FirstShift)) * MixFirst;
        mixed = (mixed ^ (mixed >> SecondShift)) * MixSecond;
        mixed ^= mixed >> FinalShift;
        return (int)(mixed % (ulong)sides);
    }

    public static int Percent(string uniqueId, int salt) => Roll(uniqueId, salt, PercentSides);

    public static bool Chance(string uniqueId, int salt, int percent) => Percent(uniqueId, salt) < percent;

    /// <summary>An index into weights, chosen in proportion to each weight.</summary>
    public static int Weighted(string uniqueId, int salt, params int[] weights)
    {
        var total = 0;

        for (var i = 0; i < weights.Length; i++)
        {
            total += weights[i] > 0 ? weights[i] : 0;
        }

        if (total <= 0)
        {
            return 0;
        }

        var roll = Roll(uniqueId, salt, total);

        for (var i = 0; i < weights.Length; i++)
        {
            var weight = weights[i] > 0 ? weights[i] : 0;

            if (roll < weight)
            {
                return i;
            }

            roll -= weight;
        }

        return weights.Length - 1;
    }

    public static T Pick<T>(string uniqueId, int salt, T[] values) =>
        values[Roll(uniqueId, salt, values.Length)];
}
