using System;

namespace SosariaAI.Skills;

/// <summary>
/// The engine's carry limit (StaminaSystem): a player carries its body weight plus its load,
/// and past its MaxWeight plus a small allowance every step costs stamina, and a step with no
/// stamina left is blocked. Woodcutters cut to the backpack's own 400-stone cap, far past a
/// body's 200 or so: conan stood in the Britain south wood with 170 logs for two hours and
/// stalled every two minutes. Work stops before the next swing would pass the limit, and a
/// person already past it sheds the surplus, heaviest pieces first, down to the limit. Pure.
/// </summary>
public static class CarryRules
{
    /// <summary>
    /// True when the work stops: the load has reached its fill line of the limit, or one more
    /// swing like the heaviest so far would carry it past the limit.
    /// </summary>
    public static bool StopsWork(int carried, int maxWeight, double fillFraction, int swingStones) =>
        PackFill.IsAtOrAboveFraction(carried, maxWeight, fillFraction) ||
        carried + Math.Max(0, swingStones) > maxWeight;

    /// <summary>Stones above the limit itself, so no allowance is needed to walk.</summary>
    public static int SurplusStones(int carried, int maxWeight) => Math.Max(0, carried - maxWeight);

    /// <summary>Stones that still fit under the fill line of the limit.</summary>
    public static int RoomStones(int carried, int maxWeight, double fillFraction) =>
        Math.Max(0, (int)(maxWeight * fillFraction) - carried);

    /// <summary>Whole pieces of a stack that shed at least <paramref name="surplusStones"/>, never more than the stack.</summary>
    public static int UnitsToShed(int surplusStones, double unitWeight, int amount)
    {
        if (surplusStones <= 0 || unitWeight <= 0 || amount <= 0)
        {
            return 0;
        }

        return Math.Min(amount, (int)Math.Ceiling(surplusStones / unitWeight));
    }

    /// <summary>Orders stacks heaviest piece first, so the fewest pieces go.</summary>
    public static int HeaviestFirst(double unitWeightA, double unitWeightB) => unitWeightB.CompareTo(unitWeightA);
}
