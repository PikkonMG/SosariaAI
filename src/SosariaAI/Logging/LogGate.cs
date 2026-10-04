using System;
using System.Collections.Generic;

namespace SosariaAI.Logging;

/// <summary>
/// Lets a line that a think or a gang member can repeat through once per event. The event is
/// the key: the first sight of it opens the gate, and every later sight within
/// <see cref="QuietGap"/> of the one before keeps it shut. A key that went quiet for the gap
/// is a new event. One red camp was logged 593 times in 78 minutes, once per member and think.
/// </summary>
public sealed class LogGate<TKey>(TimeSpan quietGap) where TKey : notnull
{
    /// <summary>Keys held before a sweep drops those that went quiet.</summary>
    public const int SweepAbove = 4096;

    private readonly Dictionary<TKey, DateTime> _lastSeen = new();
    private int _sweepAt = SweepAbove;

    public TimeSpan QuietGap { get; } = quietGap;

    /// <summary>True when <paramref name="key"/> starts a new event at <paramref name="now"/>; notes the sight either way.</summary>
    public bool Opens(TKey key, DateTime now)
    {
        var opens = IsOpen(key, now);
        Note(key, now);
        return opens;
    }

    /// <summary>
    /// True when <paramref name="key"/> went quiet for the gap, or was never seen. Notes nothing:
    /// a caller that counts only what it let through calls <see cref="Note"/> after it.
    /// </summary>
    public bool IsOpen(TKey key, DateTime now) => !_lastSeen.TryGetValue(key, out var last) || now - last >= QuietGap;

    /// <summary>Marks <paramref name="key"/> as seen at <paramref name="now"/>.</summary>
    public void Note(TKey key, DateTime now)
    {
        _lastSeen[key] = now;

        if (_lastSeen.Count > _sweepAt)
        {
            Sweep(now);
        }
    }

    /// <summary>Drops every key that went quiet; the next sweep waits until the gate doubles.</summary>
    private void Sweep(DateTime now)
    {
        var quiet = new List<TKey>();

        foreach (var (key, last) in _lastSeen)
        {
            if (now - last >= QuietGap)
            {
                quiet.Add(key);
            }
        }

        for (var i = 0; i < quiet.Count; i++)
        {
            _lastSeen.Remove(quiet[i]);
        }

        _sweepAt = Math.Max(SweepAbove, _lastSeen.Count * 2);
    }
}
