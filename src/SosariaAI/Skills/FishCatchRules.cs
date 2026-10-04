using System;

namespace SosariaAI.Skills;

/// <summary>What else a shore catch brings up besides the fish.</summary>
public enum ShoreFind
{
    None,

    /// <summary>A corked bottle with an SOS note in it.</summary>
    Bottle,

    /// <summary>A sodden treasure map.</summary>
    Parchment
}

/// <summary>
/// Odd finds on a shore or pier line. The engine's fishing brings up bottles and sodden maps
/// only in deep open water, from a boat; a fisherman on a pier never sees one. These shore
/// odds sit on top of the engine's catch and stay off in deep water, where the engine's own
/// table rules. The items themselves are the engine's. Pure.
/// </summary>
public static class FishCatchRules
{
    public const int PerMille = 1000;

    public const double BottleMinSkill = 60.0;
    public const double ParchmentMinSkill = 75.0;

    /// <summary>Out of a thousand catches: rare enough that a pier sees one now and then, not every session.</summary>
    public const int BottlePerMille = 2;

    public const int ParchmentPerMille = 1;

    public static ShoreFind Roll(double fishing, bool deepWater, int roll)
    {
        if (deepWater)
        {
            return ShoreFind.None;
        }

        var dice = Math.Abs(roll % PerMille);

        if (fishing >= BottleMinSkill && dice < BottlePerMille)
        {
            return ShoreFind.Bottle;
        }

        return fishing >= ParchmentMinSkill && dice < BottlePerMille + ParchmentPerMille
            ? ShoreFind.Parchment
            : ShoreFind.None;
    }
}
