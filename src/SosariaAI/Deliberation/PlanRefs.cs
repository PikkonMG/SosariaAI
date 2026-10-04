using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SosariaAI.Deliberation;

public static class PlanRefs
{
    public const string DestPrefix = "dest:";
    public const string PersonPrefix = "person:";
    public const string ItemPrefix = "item:";

    private static readonly Regex CoordinatePattern = new(
        @"^\s*-?\d+\s*,\s*-?\d+(\s*,\s*-?\d+)?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

    public static string DestinationOf(string targetRef)
    {
        var value = Normalize(targetRef);

        if (value.StartsWith(DestPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return value[DestPrefix.Length..];
        }

        return null;
    }

    public static string PersonOf(string targetRef)
    {
        var value = Normalize(targetRef);

        if (value.StartsWith(PersonPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return value[PersonPrefix.Length..];
        }

        return null;
    }

    public static string ItemOf(string targetRef)
    {
        var value = Normalize(targetRef);

        if (value.StartsWith(ItemPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return value[ItemPrefix.Length..];
        }

        return null;
    }

    public static bool LooksLikeCoordinates(string targetRef)
    {
        if (string.IsNullOrWhiteSpace(targetRef))
        {
            return false;
        }

        return CoordinatePattern.IsMatch(targetRef);
    }

    public static bool IsKnown(
        string targetRef,
        IReadOnlyList<string> destinations,
        IReadOnlyList<string> people,
        IReadOnlyList<string> items
    )
    {
        if (string.IsNullOrWhiteSpace(targetRef))
        {
            return true;
        }

        if (LooksLikeCoordinates(targetRef))
        {
            return false;
        }

        var dest = DestinationOf(targetRef);

        if (dest != null)
        {
            return Contains(destinations, dest);
        }

        var person = PersonOf(targetRef);

        if (person != null)
        {
            return Contains(people, person);
        }

        var item = ItemOf(targetRef);

        if (item != null)
        {
            return Contains(items, item);
        }

        return Contains(destinations, targetRef) || Contains(people, targetRef) || Contains(items, targetRef);
    }

    public static string Normalize(string targetRef)
    {
        if (string.IsNullOrWhiteSpace(targetRef))
        {
            return string.Empty;
        }

        return targetRef.Trim();
    }

    private static bool Contains(IReadOnlyList<string> values, string token)
    {
        if (values == null || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        for (var i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var slash = values[i]?.IndexOf(':') ?? -1;

            if (slash > 0 &&
                string.Equals(values[i][(slash + 1)..], token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (slash > 0 &&
                string.Equals(values[i][..slash], token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
