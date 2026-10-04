using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;

namespace SosariaAI.Combat;

/// <summary>
/// The Second Age caps: 225 stat points with no stat above 100, and 700 skill points
/// with no skill above 100. Seven grandmaster skills fill a template. Later eras keep
/// the engine's totals (225 and 700) and its 125 ceiling for one stat. Every build a
/// character wears is fitted inside the caps of its era.
/// </summary>
public static class EraBuildCaps
{
    public const int StatTotalCap = 225;
    public const int StatCap = 100;

    /// <summary>ModernUO's stats.statMax default from Lord Blackthorn's Revenge on.</summary>
    public const int EngineStatCap = 125;
    public const int MinStat = 10;
    public const double SkillCap = 100;
    public const double SkillTotalCap = 700;

    /// <summary>The engine keeps skill totals in tenths of a point.</summary>
    public const int SkillTotalCapFixed = 7000;

    private const double TenthsPerPoint = 10;

    /// <summary>
    /// Clamps each skill to 0-100 and, when the sum is above 700, scales every skill down
    /// by one factor so the template keeps its shape.
    /// </summary>
    public static Dictionary<string, double> FitSkills(IReadOnlyDictionary<string, double> skills)
    {
        var fitted = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        if (skills == null)
        {
            return fitted;
        }

        var total = 0.0;

        foreach (var pair in skills)
        {
            var value = Math.Clamp(pair.Value, 0, SkillCap);
            fitted[pair.Key] = value;
            total += value;
        }

        if (total <= SkillTotalCap)
        {
            return fitted;
        }

        var factor = SkillTotalCap / total;

        foreach (var key in new List<string>(fitted.Keys))
        {
            fitted[key] = Math.Floor(fitted[key] * factor * TenthsPerPoint) / TenthsPerPoint;
        }

        return fitted;
    }

    /// <summary>The highest one stat may stand: 100 in the Second Age, the engine's 125 after it.</summary>
    public static int StatCapFor(EraBand band) => band == EraBand.T2A ? StatCap : EngineStatCap;

    /// <summary>
    /// Clamps each stat between 10 and the era's one-stat cap and takes any points above
    /// 225 from the stats in proportion to how far each stands above the floor.
    /// </summary>
    public static (int Strength, int Dexterity, int Intelligence) FitStats(
        int strength,
        int dexterity,
        int intelligence,
        EraBand band
    )
    {
        var cap = StatCapFor(band);
        var stats = new[]
        {
            Math.Clamp(strength, MinStat, cap),
            Math.Clamp(dexterity, MinStat, cap),
            Math.Clamp(intelligence, MinStat, cap)
        };
        var excess = stats[0] + stats[1] + stats[2] - StatTotalCap;

        if (excess > 0)
        {
            var room = stats[0] + stats[1] + stats[2] - MinStat * stats.Length;
            var taken = 0;

            for (var i = 0; i < stats.Length; i++)
            {
                var share = excess * (stats[i] - MinStat) / room;
                stats[i] -= share;
                taken += share;
            }

            for (var left = excess - taken; left > 0; left--)
            {
                stats[IndexOfLargest(stats)]--;
            }
        }

        return (stats[0], stats[1], stats[2]);
    }

    private static int IndexOfLargest(int[] values)
    {
        var best = 0;

        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] > values[best])
            {
                best = i;
            }
        }

        return best;
    }
}
