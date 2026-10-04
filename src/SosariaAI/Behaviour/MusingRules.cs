using System;
using SosariaAI.Common;
using SosariaAI.Configuration;

namespace SosariaAI.Behaviour;

/// <summary>
/// Unprompted speech is rare and only follows a new event. Silence is the normal state.
/// </summary>
public static class MusingRules
{
    public const int DefaultIntervalMinutes = CharactersConfiguration.DefaultMusingIntervalMinutes;
    public const int SpeakChancePercent = 25;
    public static readonly TimeSpan WrittenLineRest = TimeSpan.FromHours(1);

    public static TimeSpan IntervalFromMinutes(int minutes) =>
        TimeSpan.FromMinutes(minutes > 0 ? minutes : DefaultIntervalMinutes);

    public static bool HasNewEvent(string pendingEvent, string lastEvent)
    {
        if (string.IsNullOrWhiteSpace(pendingEvent))
        {
            return false;
        }

        return !string.Equals(pendingEvent.Trim(), lastEvent?.Trim(), StringComparison.Ordinal);
    }

    public static bool ShouldAttempt(
        string pendingEvent,
        string lastEvent,
        DateTime lastAttempt,
        DateTime now,
        TimeSpan interval
    ) =>
        HasNewEvent(pendingEvent, lastEvent) && TimeRules.Rested(lastAttempt, now, interval);

    public static bool PassesSpeakChance(int rollPercent) =>
        rollPercent >= 0 && rollPercent < SpeakChancePercent;

    public static bool MayUseWrittenLine(DateTime lastWritten, DateTime now) =>
        TimeRules.Rested(lastWritten, now, WrittenLineRest);

    public static string Noticed(string name) => $"I noticed {name} nearby.";

    public static string Killed(string name) => $"I killed {name}.";

    public static string DiedNearby(string name) => $"{name} died nearby.";

    public static string Sold(string vendor, int gold) =>
        $"I sold goods to {vendor} for {gold} gold.";

    public static string Bought(string goods, int gold) => $"I bought a {goods} off a player vendor for {gold} gold.";

    public static string AmbitionMoved(string description) => $"My ambition moved: {description}.";

    public static string Arrived(string place) => $"I arrived at {place}.";

    public static string DayTurned(string part) => $"The day has turned to {part}.";
}
