using System;
using System.Collections.Generic;
using SosariaAI.Combat;

namespace SosariaAI.Navigation.Generation;

/// <summary>
/// Area threat from creature type names, on the ThreatRating scale.
/// Live stats come from lookup when supplied. A static table covers common names when lookup is missing.
/// A hunt ground or a dungeon floor is scored by what stands on it (<see cref="SpawnScore"/>).
/// </summary>
public static class AreaDifficulty
{
    public const int DefaultUnknown = 40;
    public const int TopHostileCount = 3;

    /// <summary>
    /// A ground's difficulty is the threat of the foe that this share of its standing spawn,
    /// counted by how many of each stand at once, lies at or below: the hard creature a fighter
    /// meets there again and again, not the rat by the door. Deceit's first floor holds forty
    /// skeletons, zombies and ghouls and two mummies, and scores its ghouls.
    /// </summary>
    public const double StrongShare = 0.8;

    /// <summary>
    /// The strongest creature of a ground weighs this share of its own threat, however few of
    /// it stand there: one ancient wyrm makes Destard's third floor a floor for groups, though
    /// its wyverns and serpents are the usual spawn, and one orc brute makes the third floor of
    /// the Orc Cave nasty.
    /// </summary>
    public const double BossShare = 0.3;

    private const int RatThreat = 8;
    private const int GiantSpiderThreat = 20;
    private const int SkeletonThreat = 25;
    private const int ZombieThreat = 30;
    private const int CorpserThreat = 35;
    private const int HarpyThreat = 40;
    private const int OrcThreat = 45;
    private const int OgreThreat = 70;
    private const int EttinThreat = 80;
    private const int GargoyleThreat = 85;
    private const int LichThreat = 90;
    private const int TrollThreat = 130;
    private const int DragonThreat = 220;
    private const int TableStrength = 0;
    private const int TableAverageDamage = 0;

    private static readonly Dictionary<string, int> FallbackThreat = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Rat"] = RatThreat,
        ["GiantSpider"] = GiantSpiderThreat,
        ["Skeleton"] = SkeletonThreat,
        ["Zombie"] = ZombieThreat,
        ["Corpser"] = CorpserThreat,
        ["Harpy"] = HarpyThreat,
        ["Orc"] = OrcThreat,
        ["Ogre"] = OgreThreat,
        ["Ettin"] = EttinThreat,
        ["Gargoyle"] = GargoyleThreat,
        ["Lich"] = LichThreat,
        ["Troll"] = TrollThreat,
        ["Dragon"] = DragonThreat
    };

    public static int AreaScore(IReadOnlyList<string> creatureNames, Func<string, HostileStats?> lookup = null)
    {
        if (creatureNames == null || creatureNames.Count == 0)
        {
            return 0;
        }

        var ranked = Rank(creatureNames, lookup);

        if (ranked.Count == 0)
        {
            return 0;
        }

        ranked.Sort(static (a, b) => b.Score.CompareTo(a.Score));
        return ScoreTaken(ranked, TopHostileCount);
    }

    /// <summary>
    /// The difficulty of a ground from what stands on it: the threat at <see cref="StrongShare"/>
    /// of its spawn counted by how many stand at once, or <see cref="BossShare"/> of its
    /// strongest creature when that is more. A name met twice counts once with both counts.
    /// Zero for no spawn.
    /// </summary>
    public static int SpawnScore(IReadOnlyList<SpawnCount> spawn, Func<string, HostileStats?> lookup = null)
    {
        var byName = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < (spawn?.Count ?? 0); i++)
        {
            var name = spawn[i].Creature?.Trim();

            if (!string.IsNullOrEmpty(name) && spawn[i].Count > 0)
            {
                byName[name] = byName.GetValueOrDefault(name) + spawn[i].Count;
            }
        }

        if (byName.Count == 0)
        {
            return 0;
        }

        var rated = new List<(int Threat, double Count)>(byName.Count);
        var total = 0.0;

        foreach (var (name, count) in byName)
        {
            rated.Add((ThreatRating.OfOne(ResolveStats(name, lookup)), count));
            total += count;
        }

        rated.Sort(static (a, b) => a.Threat.CompareTo(b.Threat));
        var strong = rated[^1].Threat;
        var counted = 0.0;

        for (var i = 0; i < rated.Count; i++)
        {
            counted += rated[i].Count;

            if (counted >= total * StrongShare)
            {
                strong = rated[i].Threat;
                break;
            }
        }

        return Math.Max(strong, (int)(rated[^1].Threat * BossShare));
    }

    private static List<RankedHostile> Rank(
        IReadOnlyList<string> creatureNames,
        Func<string, HostileStats?> lookup
    )
    {
        var ranked = new List<RankedHostile>();

        if (creatureNames == null || creatureNames.Count == 0)
        {
            return ranked;
        }

        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < creatureNames.Count; i++)
        {
            var name = creatureNames[i];

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            name = name.Trim();

            if (!unique.Add(name))
            {
                continue;
            }

            var stats = ResolveStats(name, lookup);
            ranked.Add(new RankedHostile(ThreatRating.Score([stats]), stats));
        }

        return ranked;
    }

    private static int ScoreTaken(List<RankedHostile> ranked, int takeCount)
    {
        var take = Math.Min(takeCount, ranked.Count);
        var hostiles = new HostileStats[take];

        for (var i = 0; i < take; i++)
        {
            hostiles[i] = ranked[i].Stats;
        }

        return ThreatRating.Score(hostiles);
    }

    private static HostileStats ResolveStats(string typeName, Func<string, HostileStats?> lookup)
    {
        if (lookup != null)
        {
            var live = lookup(typeName);

            if (live.HasValue)
            {
                return live.Value;
            }
        }

        if (FallbackThreat.TryGetValue(typeName, out var threat))
        {
            return FromTable(threat);
        }

        return FromTable(DefaultUnknown);
    }

    private static HostileStats FromTable(int threat) => new(threat, TableStrength, TableAverageDamage);

    private readonly record struct RankedHostile(int Score, HostileStats Stats);
}

public static class ResourceKind
{
    public const string Lumber = "Lumber";
    public const string Mine = "Mine";

    private const string ForestToken = "forest";
    private const string WoodToken = "wood";
    private const string YewToken = "yew";
    private const string MineToken = "mine";
    private const string OreToken = "ore";
    private const string MountainToken = "mountain";
    private const string MinocToken = "minoc";

    private static readonly string[] LumberTokens = [ForestToken, WoodToken, YewToken];
    private static readonly string[] MineTokens = [MineToken, OreToken, MountainToken, MinocToken];

    public static string FromGroup(string group)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            return null;
        }

        if (ContainsAny(group, LumberTokens))
        {
            return Lumber;
        }

        if (ContainsAny(group, MineTokens))
        {
            return Mine;
        }

        return null;
    }

    private static bool ContainsAny(string text, string[] tokens)
    {
        for (var i = 0; i < tokens.Length; i++)
        {
            if (ContainsToken(text, tokens[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsToken(string text, string token)
    {
        var index = 0;

        while (index <= text.Length - token.Length)
        {
            var found = text.IndexOf(token, index, StringComparison.OrdinalIgnoreCase);

            if (found < 0)
            {
                return false;
            }

            if (found == 0 || !char.IsLetter(text[found - 1]))
            {
                return true;
            }

            index = found + 1;
        }

        return false;
    }
}
