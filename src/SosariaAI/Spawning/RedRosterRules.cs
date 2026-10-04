using System;
using System.Collections.Generic;
using SosariaAI.Combat;
using SosariaAI.Configuration;

namespace SosariaAI.Spawning;

/// <summary>
/// Who rides red, and with whom. A red is an armed fighter who has lived a while: a warrior,
/// fencer, archer, mage or a hybrid of them, never a miner, a crafter, a thief or a tamer,
/// and one who casts, even if only to travel, so it can recall off Buccaneer's Den to the
/// roads it hunts. The copy's own rolled job and class decide, not the roster template it
/// was made from: a copy of a swordsman template can roll a miner. Every red rides with at
/// least one other: a red alone on the roads starts nothing. Pure.
/// </summary>
public static class RedRosterRules
{
    /// <summary>The fewest reds a facet needs before its loners pair off; a single red rides alone.</summary>
    public const int MinRedsToPair = PkGangRules.MinGangSize;

    /// <summary>
    /// A person may ride red: a fighter by job, whose class casts or keeps travel magic, of
    /// at least the tier that carries marked runes.
    /// </summary>
    public static bool MayRideRed(string job, bool caster, bool travelMagic, SkillTier tier) =>
        job == PersonJobs.Fighter && (caster || travelMagic) && tier >= Skills.RuneKit.EstablishedTier;

    /// <summary>
    /// The gangs with no red left alone: the loners <see cref="PkGangRules"/> leaves pair off
    /// into gangs of their own, in order, and an odd last one joins the pair before it, or the
    /// last gang when it is the only loner. Gang numbers stay those of the gangs formed first.
    /// </summary>
    public static int[] RideTogether(IReadOnlyList<int> gangs)
    {
        var count = gangs?.Count ?? 0;
        var result = new int[count];
        var sizes = new Dictionary<int, int>();

        for (var i = 0; i < count; i++)
        {
            result[i] = gangs[i];
            sizes[gangs[i]] = sizes.GetValueOrDefault(gangs[i]) + 1;
        }

        var loners = new List<int>();
        var formed = PkGangRules.NoGang;

        for (var i = 0; i < count; i++)
        {
            if (gangs[i] == PkGangRules.NoGang)
            {
                continue;
            }

            if (sizes[gangs[i]] < PkGangRules.MinGangSize)
            {
                loners.Add(i);
            }
            else if (gangs[i] > formed)
            {
                formed = gangs[i];
            }
        }

        if (loners.Count == 0 || count < MinRedsToPair)
        {
            return result;
        }

        if (loners.Count == 1)
        {
            result[loners[0]] = formed == PkGangRules.NoGang ? gangs[loners[0]] : formed;
            return result;
        }

        var pairs = loners.Count / PkGangRules.MinGangSize;

        for (var k = 0; k < loners.Count; k++)
        {
            result[loners[k]] = formed + 1 + Math.Min(k / PkGangRules.MinGangSize, pairs - 1);
        }

        return result;
    }

    /// <summary>How many gangs, and how many reds still ride alone, in a set of gangs.</summary>
    public static (int Gangs, int Alone) Count(IReadOnlyList<int> gangs)
    {
        var sizes = new Dictionary<int, int>();

        for (var i = 0; i < (gangs?.Count ?? 0); i++)
        {
            if (gangs[i] != PkGangRules.NoGang)
            {
                sizes[gangs[i]] = sizes.GetValueOrDefault(gangs[i]) + 1;
            }
        }

        var alone = 0;

        foreach (var size in sizes.Values)
        {
            if (size < PkGangRules.MinGangSize)
            {
                alone++;
            }
        }

        return (sizes.Count - alone, alone);
    }
}
