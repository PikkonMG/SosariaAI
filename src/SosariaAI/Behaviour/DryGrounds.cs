using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Behaviour;

/// <summary>
/// Hunt grounds a fighter found empty lately. A shared spawner refills in five to ten
/// minutes, so the fighter hunts somewhere else for a while instead of walking back to the
/// same bare field. Kept per fighter, in memory only.
/// </summary>
public static class DryGrounds
{
    public static readonly TimeSpan DryFor = TimeSpan.FromMinutes(20);

    /// <summary>Past this many notes the ones that ran out are dropped.</summary>
    public const int PruneAt = 4096;

    private static readonly Dictionary<(uint Fighter, Point2D Ground), DateTime> Until = new();

    public static void Note(uint fighter, Point2D ground, DateTime now)
    {
        if (Until.Count >= PruneAt)
        {
            var spent = new List<(uint, Point2D)>();

            foreach (var (key, until) in Until)
            {
                if (now >= until)
                {
                    spent.Add(key);
                }
            }

            for (var i = 0; i < spent.Count; i++)
            {
                Until.Remove(spent[i]);
            }
        }

        Until[(fighter, ground)] = now + DryFor;
    }

    public static bool IsDry(uint fighter, Point2D ground, DateTime now) =>
        Until.TryGetValue((fighter, ground), out var until) && now < until;
}
