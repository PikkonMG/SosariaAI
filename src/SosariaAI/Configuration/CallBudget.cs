using System;
using System.Collections.Generic;

namespace SosariaAI.Configuration;

/// <summary>
/// Rolling paid-call counters. A restart creates a new instance, so counts start at zero.
/// </summary>
public sealed class CallBudget
{
    public const int DefaultMaxPaidCallsPerDay = 10000;
    public const int DefaultMaxPaidCallsPerHour = 10000;

    private static readonly TimeSpan HourWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan DayWindow = TimeSpan.FromDays(1);

    private readonly Queue<DateTime> _hour = new();
    private readonly Queue<DateTime> _day = new();
    private readonly int _maxDay;
    private readonly int _maxHour;

    public CallBudget(int maxPaidCallsPerDay, int maxPaidCallsPerHour)
    {
        _maxDay = Math.Max(0, maxPaidCallsPerDay);
        _maxHour = Math.Max(0, maxPaidCallsPerHour);
    }

    public int PaidCallsToday => _day.Count;

    public int PaidCallsThisHour => _hour.Count;

    public bool CanSpend(DateTime now)
    {
        Prune(now);
        return _day.Count < _maxDay && _hour.Count < _maxHour;
    }

    public void Record(DateTime now)
    {
        Prune(now);
        _hour.Enqueue(now);
        _day.Enqueue(now);
    }

    private void Prune(DateTime now)
    {
        var hourAgo = now - HourWindow;
        var dayAgo = now - DayWindow;

        while (_hour.Count > 0 && _hour.Peek() <= hourAgo)
        {
            _hour.Dequeue();
        }

        while (_day.Count > 0 && _day.Peek() <= dayAgo)
        {
            _day.Dequeue();
        }
    }
}
