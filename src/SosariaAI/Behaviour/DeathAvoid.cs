using System;
using System.Collections.Generic;
using SosariaAI.Configuration;

namespace SosariaAI.Behaviour;

/// <summary>
/// A character that died in a place does not go back there until it is strong
/// again, and never twice in one day when it has already died twice there. It also keeps
/// off a hunt place where a close friend fell lately.
/// </summary>
public static class DeathAvoid
{
    public const int DeathsPerPlacePerDay = 2;
    public const int PowerMargin = 20;

    public static bool ShouldAvoid(
        string deathPlace,
        string targetPlace,
        DateTime diedAt,
        DateTime now,
        int hours,
        int deathsThereToday,
        int power,
        int areaDifficulty
    )
    {
        if (string.IsNullOrWhiteSpace(deathPlace) ||
            string.IsNullOrWhiteSpace(targetPlace) ||
            !string.Equals(deathPlace, targetPlace, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (deathsThereToday >= DeathsPerPlacePerDay && SameDay(diedAt, now))
        {
            return true;
        }

        var window = WindowHours(hours);

        if (diedAt == default || (now - diedAt).TotalHours >= window)
        {
            return false;
        }

        return power < areaDifficulty + PowerMargin;
    }

    /// <summary>The configured hours a death is kept away from; 0 or less uses the default.</summary>
    public static int WindowHours(int hours) => hours > 0 ? hours : CareerSettings.DefaultDeathAvoidHours;

    /// <summary>True when one of the places a close friend fell lately is this hunt ground or this dungeon.</summary>
    public static bool FriendFellAt(IReadOnlyList<string> friendDeathPlaces, string groundPlace, string dungeonPlace)
    {
        if (friendDeathPlaces == null)
        {
            return false;
        }

        for (var i = 0; i < friendDeathPlaces.Count; i++)
        {
            if (SamePlace(friendDeathPlaces[i], groundPlace) || SamePlace(friendDeathPlaces[i], dungeonPlace))
            {
                return true;
            }
        }

        return false;
    }

    public static int AfterDeath(string lastPlace, string place, int count, DateTime lastAt, DateTime now)
    {
        if (!string.Equals(lastPlace, place, StringComparison.OrdinalIgnoreCase) || !SameDay(lastAt, now))
        {
            return 1;
        }

        return count + 1;
    }

    private static bool SamePlace(string left, string right) =>
        !string.IsNullOrWhiteSpace(left) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool SameDay(DateTime left, DateTime right) =>
        left != default && right != default && left.Date == right.Date;
}
