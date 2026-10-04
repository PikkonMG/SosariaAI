using System;
using System.Collections.Generic;

namespace SosariaAI.Social;

/// <summary>What the minute sync does with one pair of guilds.</summary>
public enum WarSync
{
    /// <summary>Both books agree.</summary>
    None,

    /// <summary>The save kept a war the fresh registry does not know: the registry takes it.</summary>
    Keep,

    /// <summary>The registry holds a war the engine lacks: the engine declares it.</summary>
    Declare,

    /// <summary>The feud went quiet: the engine's war ends too.</summary>
    End
}

/// <summary>A war starts from real aggression and expires when the feud goes quiet.</summary>
public sealed class GuildWarRegistry
{
    public static readonly TimeSpan WarDuration = TimeSpan.FromHours(6);

    /// <summary>
    /// One pair's sync step. On the first pass after a boot an engine war is kept; after it the
    /// registry wins both ways.
    /// </summary>
    public static WarSync SyncStep(bool engineWar, bool registryWar, bool seeding)
    {
        if (engineWar == registryWar)
        {
            return WarSync.None;
        }

        if (engineWar)
        {
            return seeding ? WarSync.Keep : WarSync.End;
        }

        return WarSync.Declare;
    }

    /// <summary>
    /// A blow that starts or feeds a war between two guilds: not one by a red nor on one. A
    /// murder is a murder and a blow on a red is the law, not a feud of the guilds. Every red's
    /// hit on a blue warred its guild with the blue's for six hours, and the Chaos zerg, about one
    /// red in five, was soon at war with nearly every guild: 1,030 guild war draws an hour.
    /// </summary>
    public static bool FeedsWar(bool attackerRed, bool defenderRed) => !attackerRed && !defenderRed;

    private readonly Dictionary<(int First, int Second), DateTime> _wars = [];

    public bool RecordAggression(int attackerGuild, int defenderGuild, DateTime now)
    {
        if (!ValidPair(attackerGuild, defenderGuild))
        {
            return false;
        }

        var pair = Pair(attackerGuild, defenderGuild);
        var started = !_wars.TryGetValue(pair, out var until) || until <= now;
        _wars[pair] = now + WarDuration;
        return started;
    }

    public bool AtWar(int firstGuild, int secondGuild, DateTime now)
    {
        if (!ValidPair(firstGuild, secondGuild))
        {
            return false;
        }

        var pair = Pair(firstGuild, secondGuild);

        if (!_wars.TryGetValue(pair, out var until))
        {
            return false;
        }

        if (until > now)
        {
            return true;
        }

        _wars.Remove(pair);
        return false;
    }

    private static bool ValidPair(int first, int second) =>
        first >= 0 && second >= 0 && first != second;

    private static (int First, int Second) Pair(int first, int second) =>
        first < second ? (first, second) : (second, first);
}
