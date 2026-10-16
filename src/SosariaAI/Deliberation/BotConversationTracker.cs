using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Deliberation;

public sealed class BotConversationTracker
{
    /// <summary>
    /// A pair or a speaker silent this long is forgotten and starts fresh, so the maps hold only
    /// the people talking lately. The rests and quiets callers wait out are minutes long.
    /// </summary>
    public static readonly TimeSpan ForgetAfter = TimeSpan.FromDays(1);

    /// <summary>How often a write looks for pairs and speakers to forget.</summary>
    public static readonly TimeSpan SweepEvery = TimeSpan.FromHours(1);

    private readonly Dictionary<(uint First, uint Second), PairState> _pairs = new();
    private readonly Dictionary<uint, DateTime> _speakerLast = new();
    private readonly Dictionary<string, DateTime> _areaLast = new(StringComparer.Ordinal);
    private DateTime _lastSweep;

    public bool CanGreet(Serial first, Serial second, DateTime now, TimeSpan cooldown) =>
        CanGreet(first, second, now, cooldown, pairRest: cooldown, maxExchanges: int.MaxValue);

    /// <summary>
    /// A pair may trade another line until it reaches the cap; then it rests first. Below the cap,
    /// the pair waits out the cooldown between lines.
    /// </summary>
    public bool CanGreet(
        Serial first,
        Serial second,
        DateTime now,
        TimeSpan cooldown,
        TimeSpan pairRest,
        int maxExchanges
    )
    {
        var key = PairKey(first, second);

        if (!_pairs.TryGetValue(key, out var state))
        {
            return true;
        }

        if (state.Count >= maxExchanges)
        {
            return now - state.Last >= pairRest;
        }

        return now - state.Last >= cooldown;
    }

    public void Record(Serial first, Serial second, DateTime now, TimeSpan pairRest)
    {
        Sweep(now);
        var key = PairKey(first, second);

        if (!_pairs.TryGetValue(key, out var state) || now - state.Last >= pairRest)
        {
            _pairs[key] = new PairState(1, now);
            return;
        }

        _pairs[key] = new PairState(state.Count + 1, now);
    }

    public void RecordGreeting(Serial first, Serial second, DateTime now)
    {
        Record(first, second, now, pairRest: TimeSpan.MaxValue);
        RecordSpeakerGreet(first, now);
    }

    public bool SpeakerMayGreet(Serial speaker, DateTime now, TimeSpan quiet)
    {
        if (!_speakerLast.TryGetValue(speaker.Value, out var last))
        {
            return true;
        }

        return now - last >= quiet;
    }

    public void RecordSpeakerGreet(Serial speaker, DateTime now)
    {
        Sweep(now);
        _speakerLast[speaker.Value] = now;
    }

    public bool AreaMayGreet(string area, DateTime now, TimeSpan gap)
    {
        if (string.IsNullOrEmpty(area) || !_areaLast.TryGetValue(area, out var last))
        {
            return true;
        }

        return now - last >= gap;
    }

    public void RecordAreaGreet(string area, DateTime now)
    {
        if (!string.IsNullOrEmpty(area))
        {
            _areaLast[area] = now;
        }
    }

    private void Sweep(DateTime now)
    {
        if (now - _lastSweep < SweepEvery)
        {
            return;
        }

        _lastSweep = now;
        List<(uint, uint)> quietPairs = null;
        List<uint> quietSpeakers = null;

        foreach (var (pair, state) in _pairs)
        {
            if (now - state.Last >= ForgetAfter)
            {
                (quietPairs ??= []).Add(pair);
            }
        }

        foreach (var (speaker, last) in _speakerLast)
        {
            if (now - last >= ForgetAfter)
            {
                (quietSpeakers ??= []).Add(speaker);
            }
        }

        for (var i = 0; quietPairs != null && i < quietPairs.Count; i++)
        {
            _pairs.Remove(quietPairs[i]);
        }

        for (var i = 0; quietSpeakers != null && i < quietSpeakers.Count; i++)
        {
            _speakerLast.Remove(quietSpeakers[i]);
        }
    }

    private static (uint First, uint Second) PairKey(Serial first, Serial second) =>
        first.Value <= second.Value ? (first.Value, second.Value) : (second.Value, first.Value);

    private readonly record struct PairState(int Count, DateTime Last);
}
