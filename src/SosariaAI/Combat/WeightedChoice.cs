using System;
using System.Collections.Generic;

namespace SosariaAI.Combat;

public static class WeightedChoice
{
    public static string Pick(IReadOnlyList<(string Id, int Weight)> choices, Func<int, int> next)
    {
        if (choices == null || choices.Count == 0)
        {
            return null;
        }

        var total = 0;

        for (var i = 0; i < choices.Count; i++)
        {
            total += Math.Max(0, choices[i].Weight);
        }

        if (total <= 0 || next == null)
        {
            return choices[0].Id;
        }

        var roll = next(total);
        var acc = 0;

        for (var i = 0; i < choices.Count; i++)
        {
            acc += Math.Max(0, choices[i].Weight);

            if (roll < acc)
            {
                return choices[i].Id;
            }
        }

        return choices[0].Id;
    }

    /// <summary>
    /// The index a roll in [0, 1) lands on when each entry weighs its share of the total.
    /// Entries of zero or less never win. -1 when nothing weighs anything.
    /// </summary>
    public static int Index(IReadOnlyList<double> weights, double unit)
    {
        var total = 0.0;

        for (var i = 0; i < (weights?.Count ?? 0); i++)
        {
            total += Math.Max(0, weights[i]);
        }

        if (total <= 0)
        {
            return -1;
        }

        var roll = Math.Clamp(unit, 0, 1) * total;
        var last = -1;
        var acc = 0.0;

        for (var i = 0; i < weights.Count; i++)
        {
            if (weights[i] <= 0)
            {
                continue;
            }

            last = i;
            acc += weights[i];

            if (roll < acc)
            {
                return i;
            }
        }

        return last;
    }
}
