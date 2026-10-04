using System;
using System.Collections.Generic;

namespace SosariaAI.Deliberation;

/// <summary>
/// A musing or Decide line that matches a recent spoken line is not said again.
/// Near repeats share enough words to be the same idea in different clothes.
/// </summary>
public static class SpokenRepeat
{
    public const int RememberedLines = 24;
    public const double NearRepeatOverlap = 0.50;
    public const int MinWordLength = 3;

    public static bool Matches(string candidate, IReadOnlyList<string> recent)
    {
        if (string.IsNullOrWhiteSpace(candidate) || recent == null || recent.Count == 0)
        {
            return false;
        }

        var left = Normalize(candidate);

        for (var i = 0; i < recent.Count; i++)
        {
            if (left.Equals(Normalize(recent[i]), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsNearRepeat(string candidate, IReadOnlyList<string> recent)
    {
        if (Matches(candidate, recent))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(candidate) || recent == null || recent.Count == 0)
        {
            return false;
        }

        var left = WordSet(candidate);

        if (left.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < recent.Count; i++)
        {
            var right = WordSet(recent[i]);

            if (right.Count == 0)
            {
                continue;
            }

            if (Overlap(left, right) >= NearRepeatOverlap)
            {
                return true;
            }
        }

        return false;
    }

    public static double Overlap(HashSet<string> left, HashSet<string> right)
    {
        if (left == null || right == null || left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var shared = 0;

        foreach (var word in left)
        {
            if (right.Contains(word))
            {
                shared++;
            }
        }

        var union = left.Count + right.Count - shared;
        return union == 0 ? 0 : (double)shared / union;
    }

    public static HashSet<string> WordSet(string line)
    {
        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = Normalize(line);

        if (string.IsNullOrEmpty(normalized))
        {
            return words;
        }

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length >= MinWordLength)
            {
                words.Add(parts[i]);
            }
        }

        return words;
    }

    public static string Normalize(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        var chars = line.Trim().ToLowerInvariant().ToCharArray();
        var written = 0;

        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsLetterOrDigit(chars[i]) || chars[i] == ' ')
            {
                chars[written++] = chars[i];
            }
        }

        return new string(chars, 0, written);
    }
}
