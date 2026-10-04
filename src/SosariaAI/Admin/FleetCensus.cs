using System;
using System.Collections.Generic;
using SosariaAI.Social;

namespace SosariaAI.Admin;

/// <summary>
/// What the census reads from one character. The world side fills it on the game
/// thread; the counts below never touch a mobile.
/// </summary>
public readonly record struct CharacterSample(
    string Facet,
    bool Red,
    bool Ghost,
    bool InCombat,
    bool InDungeon,
    bool SuppliesLow,
    uint PartyLeader,
    string Activity,
    string Job,
    string Place,
    int ChatCalls,
    int PlanCalls
)
{
    /// <summary>A party leader serial of zero means the character is in no party.</summary>
    public const uint NoParty = 0;
}

/// <summary>
/// Population counts for the staff panel and the status file: activity, job, place, facet,
/// state and model call counts of the characters in the world. Pure.
/// </summary>
public sealed class FleetCounts
{
    public const string UnknownKey = "unknown";

    private readonly HashSet<uint> _parties = [];

    public int Live { get; private set; }

    public int Reds { get; private set; }

    public int Ghosts { get; private set; }

    public int InCombat { get; private set; }

    public int InDungeons { get; private set; }

    /// <summary>
    /// Live people short of a supply they fight with. One run lost its dungeon crowd to this
    /// unseen: 408 of 500 stays ended at the door for "its supplies ran low".
    /// </summary>
    public int SuppliesLow { get; private set; }

    public long ChatCalls { get; private set; }

    public long PlanCalls { get; private set; }

    public int Parties => _parties.Count;

    public SortedDictionary<string, int> LiveByFacet { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> ByActivity { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> ByJob { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> ByPlace { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static FleetCounts Of(IEnumerable<CharacterSample> samples)
    {
        var counts = new FleetCounts();

        foreach (var sample in samples)
        {
            counts.Add(sample);
        }

        return counts;
    }

    public void Add(in CharacterSample sample)
    {
        ChatCalls += sample.ChatCalls;
        PlanCalls += sample.PlanCalls;
        Live++;
        Bump(LiveByFacet, sample.Facet);
        Bump(ByActivity, sample.Activity);
        Bump(ByJob, sample.Job);
        Bump(ByPlace, sample.Place);

        if (sample.Red)
        {
            Reds++;
        }

        if (sample.Ghost)
        {
            Ghosts++;
        }

        if (sample.InCombat)
        {
            InCombat++;
        }

        if (sample.InDungeon)
        {
            InDungeons++;
        }

        if (sample.SuppliesLow)
        {
            SuppliesLow++;
        }

        if (sample.PartyLeader != CharacterSample.NoParty)
        {
            _parties.Add(sample.PartyLeader);
        }
    }

    /// <summary>The largest counts first; a tie goes to the name that sorts first.</summary>
    public static List<KeyValuePair<string, int>> Top(IReadOnlyDictionary<string, int> counts, int limit)
    {
        var list = new List<KeyValuePair<string, int>>(counts ?? new Dictionary<string, int>());
        list.Sort(
            (a, b) =>
            {
                var byCount = b.Value.CompareTo(a.Value);
                return byCount != 0 ? byCount : string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
            }
        );

        if (limit >= 0 && list.Count > limit)
        {
            list.RemoveRange(limit, list.Count - limit);
        }

        return list;
    }

    private static void Bump(IDictionary<string, int> counts, string key)
    {
        var name = string.IsNullOrWhiteSpace(key) ? UnknownKey : key;
        counts[name] = counts.TryGetValue(name, out var count) ? count + 1 : 1;
    }
}

/// <summary>Deaths and murders from the shard journal inside a recent window. Pure.</summary>
public static class JournalTally
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public readonly record struct Result(int Deaths, int Murders, IReadOnlyList<ShardEvent> RecentMurders);

    /// <summary>
    /// A murder is a <see cref="ShardEventType.Pk"/> event: the actor died, the other named
    /// the killer. Any other death counts apart. The newest murders come first.
    /// </summary>
    public static Result Within(IReadOnlyList<ShardEvent> events, DateTime now, TimeSpan window, int murderLimit)
    {
        var deaths = 0;
        var murders = new List<ShardEvent>();

        if (events != null)
        {
            for (var i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                var age = now - evt.At;

                if (age < TimeSpan.Zero || age > window)
                {
                    continue;
                }

                if (evt.Type == ShardEventType.Pk)
                {
                    murders.Add(evt);
                }
                else if (evt.Type == ShardEventType.Death)
                {
                    deaths++;
                }
            }
        }

        murders.Sort((a, b) => b.At.CompareTo(a.At));
        var recent = murders.Count > murderLimit ? murders.GetRange(0, Math.Max(0, murderLimit)) : murders;
        return new Result(deaths, murders.Count, recent);
    }
}
