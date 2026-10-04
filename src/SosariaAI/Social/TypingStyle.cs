using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;

namespace SosariaAI.Social;

/// <summary>
/// Types a line the way one person types: case, short forms, the end of the line, a rare
/// slip of the fingers, a laugh on the end, a stretched word. Pure: the same line, profile
/// and seed always give the same text.
/// A word with a digit is never touched, so prices and amounts stay exact. A guarded line
/// (a scripted, trade or combat line) also keeps every word with a capital letter, so names
/// and item names stay as written, and it gets no slip, laugh, stretch or shouted word.
/// </summary>
public static class TypingStyle
{
    public const int PercentSides = 100;
    public const int MinTypoLength = 4;
    public const int MinShoutLength = 3;
    public const int EmphasisRepeat = 2;
    public const int RareCapsPercent = 15;

    private const char Space = ' ';
    private const char FullStop = '.';
    private const char Bang = '!';
    private const char Question = '?';
    private const string LineEndMarks = ".!?";
    private const string Ellipsis = "...";
    private const string DoubleBang = "!!";
    private const string DoubleQuestion = "??";
    private const string ThankWord = "thank";
    private const string YouWord = "you";

    // Typo edits keep the first and the last letter of the word.
    private const int TypoEdgeLetters = 1;
    private const int SwapWidth = 2;

    private const int TypoSalt = 11;
    private const int TypoWordSalt = 13;
    private const int TypoSpotSalt = 17;
    private const int EmphasisSalt = 19;
    private const int ShoutSalt = 23;
    private const int TailSalt = 29;

    private static readonly Dictionary<string, string> CommonShortForms = new(StringComparer.Ordinal)
    {
        ["you"] = "u",
        ["your"] = "ur",
        ["please"] = "plz",
        ["okay"] = "ok"
    };

    private static readonly Dictionary<string, string> HeavyShortForms = new(StringComparer.Ordinal)
    {
        ["are"] = "r",
        ["you're"] = "ur",
        ["youre"] = "ur",
        ["because"] = "cuz",
        ["people"] = "ppl",
        ["tonight"] = "tonite"
    };

    private static readonly HashSet<string> ThanksForms = new(StringComparer.Ordinal) { "thanks", "thx", "ty" };

    private static readonly HashSet<string> EmphasisWords = new(StringComparer.Ordinal)
    {
        "so", "no", "yes", "nice", "hey", "wow", "cool", "ok", "oh", "yeah", "man", "sweet"
    };

    private static readonly char[] LineEndChars = LineEndMarks.ToCharArray();

    private static readonly HashSet<string> LaughWords = new(StringComparer.Ordinal)
    {
        "lol", "heh", "hehe", "haha", "lmao", "rofl"
    };

    public static string Apply(string line, TypingProfile profile, int seed, bool guarded = false)
    {
        if (string.IsNullOrWhiteSpace(line) || profile == null)
        {
            return line;
        }

        var words = Split(line, guarded);

        if (words.Count == 0)
        {
            return line;
        }

        Shorten(words, profile);
        SetCase(words, profile.Case);

        if (!guarded)
        {
            Slip(words, profile, seed);
            Stretch(words, profile, seed);
            Shout(words, profile, seed);
        }

        EndLine(words[^1], profile.End);
        var typed = Join(words);
        return guarded ? typed : AddTail(typed, words[^1], profile, seed);
    }

    private enum WordKind
    {
        /// <summary>Any habit may change it.</summary>
        Plain,

        /// <summary>A name inside a free line: only its case may change.</summary>
        NameLike,

        /// <summary>A number, or a capitalised word in a guarded line: kept exactly.</summary>
        Fixed
    }

    private sealed class Word
    {
        public string Lead;
        public string Core;
        public string Trail;
        public WordKind Kind;
    }

    private static List<Word> Split(string line, bool guarded)
    {
        var parts = line.Split(Space, StringSplitOptions.RemoveEmptyEntries);
        var words = new List<Word>(parts.Length);

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var start = 0;
            var end = part.Length - 1;

            while (start <= end && !char.IsLetterOrDigit(part[start]))
            {
                start++;
            }

            while (end >= start && !char.IsLetterOrDigit(part[end]))
            {
                end--;
            }

            var word = start > end
                ? new Word { Lead = part, Core = string.Empty, Trail = string.Empty, Kind = WordKind.Fixed }
                : new Word { Lead = part[..start], Core = part[start..(end + 1)], Trail = part[(end + 1)..] };

            if (word.Core.Length > 0)
            {
                word.Kind = KindOf(word.Core, words.Count, guarded);
            }

            words.Add(word);
        }

        return words;
    }

    private static WordKind KindOf(string core, int index, bool guarded)
    {
        var hasUpper = false;

        for (var i = 0; i < core.Length; i++)
        {
            if (char.IsDigit(core[i]))
            {
                return WordKind.Fixed;
            }

            hasUpper |= char.IsUpper(core[i]);
        }

        if (!hasUpper)
        {
            return WordKind.Plain;
        }

        if (guarded)
        {
            return WordKind.Fixed;
        }

        return index == 0 ? WordKind.Plain : WordKind.NameLike;
    }

    private static void Shorten(List<Word> words, TypingProfile profile)
    {
        if (profile.AbbreviationLevel <= TypingProfile.NoAbbreviations)
        {
            return;
        }

        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];

            if (word.Kind != WordKind.Plain)
            {
                continue;
            }

            var lower = word.Core.ToLowerInvariant();

            if (lower == ThankWord && i + 1 < words.Count && IsThankYouTail(word, words[i + 1]))
            {
                word.Core = profile.ThanksWord;
                word.Trail = words[i + 1].Trail;
                words.RemoveAt(i + 1);
                continue;
            }

            if (ThanksForms.Contains(lower))
            {
                word.Core = profile.ThanksWord;
                continue;
            }

            if (CommonShortForms.TryGetValue(lower, out var common))
            {
                word.Core = common;
                continue;
            }

            if (profile.AbbreviationLevel >= TypingProfile.HeavyAbbreviations &&
                HeavyShortForms.TryGetValue(lower, out var heavy))
            {
                word.Core = heavy;
            }
        }
    }

    private static bool IsThankYouTail(Word thank, Word next) =>
        thank.Trail.Length == 0 && next.Lead.Length == 0 && next.Kind == WordKind.Plain &&
        next.Core.Equals(YouWord, StringComparison.OrdinalIgnoreCase);

    private static void SetCase(List<Word> words, TypingCase habit)
    {
        if (habit == TypingCase.Normal)
        {
            return;
        }

        for (var i = 0; i < words.Count; i++)
        {
            if (words[i].Kind != WordKind.Fixed)
            {
                words[i].Core = words[i].Core.ToLowerInvariant();
            }
        }
    }

    // One pair of inner letters swapped in one plain lower-case word, never a name.
    private static void Slip(List<Word> words, TypingProfile profile, int seed)
    {
        if (!Rolls(seed, TypoSalt, profile.TypoPercent))
        {
            return;
        }

        var candidates = new List<Word>();

        for (var i = 0; i < words.Count; i++)
        {
            if (words[i].Kind == WordKind.Plain && words[i].Core.Length >= MinTypoLength && IsLowerWord(words[i].Core))
            {
                candidates.Add(words[i]);
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var word = candidates[Pick(seed, TypoWordSalt, candidates.Count)];
        var spots = word.Core.Length - TypoEdgeLetters * 2 - (SwapWidth - 1);
        var at = TypoEdgeLetters + (spots <= 1 ? 0 : Pick(seed, TypoSpotSalt, spots));
        var letters = word.Core.ToCharArray();
        (letters[at], letters[at + 1]) = (letters[at + 1], letters[at]);
        word.Core = new string(letters);
    }

    private static void Stretch(List<Word> words, TypingProfile profile, int seed)
    {
        if (!Rolls(seed, EmphasisSalt, profile.EmphasisPercent))
        {
            return;
        }

        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];

            if (word.Kind == WordKind.Plain && EmphasisWords.Contains(word.Core.ToLowerInvariant()))
            {
                word.Core += new string(word.Core[^1], EmphasisRepeat);
                return;
            }
        }
    }

    private static void Shout(List<Word> words, TypingProfile profile, int seed)
    {
        if (profile.Case != TypingCase.RareCaps || !Rolls(seed, ShoutSalt, RareCapsPercent))
        {
            return;
        }

        Word longest = null;

        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];

            if (word.Kind == WordKind.Plain && word.Core.Length >= MinShoutLength &&
                (longest == null || word.Core.Length > longest.Core.Length))
            {
                longest = word;
            }
        }

        if (longest != null)
        {
            longest.Core = longest.Core.ToUpperInvariant();
        }
    }

    private static void EndLine(Word last, TypingEnd habit)
    {
        if (habit == TypingEnd.Keep)
        {
            return;
        }

        var marksStart = last.Trail.Length;

        while (marksStart > 0 && LineEndMarks.IndexOf(last.Trail[marksStart - 1]) >= 0)
        {
            marksStart--;
        }

        var marks = last.Trail[marksStart..];
        var asks = marks.IndexOf(Question) >= 0;
        var calls = marks.IndexOf(Bang) >= 0;
        var stops = marks.IndexOf(FullStop) >= 0;
        var ending = habit switch
        {
            TypingEnd.Drop => asks ? Question.ToString() : calls ? Bang.ToString() : string.Empty,
            TypingEnd.Double => asks ? DoubleQuestion : calls ? DoubleBang : string.Empty,
            _ => asks ? Question.ToString() : calls ? Bang.ToString() : stops ? Ellipsis : string.Empty
        };

        last.Trail = last.Trail[..marksStart] + ending;
    }

    private static string AddTail(string typed, Word last, TypingProfile profile, int seed)
    {
        if (string.IsNullOrEmpty(profile.Tail) || !Rolls(seed, TailSalt, profile.TailPercent) ||
            last.Trail.IndexOf(Question) >= 0 || HasLaugh(typed))
        {
            return typed;
        }

        return typed + Space + profile.Tail;
    }

    private static bool HasLaugh(string typed)
    {
        var parts = typed.Split(Space, StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < parts.Length; i++)
        {
            if (LaughWords.Contains(parts[i].Trim(LineEndChars).ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    private static string Join(List<Word> words)
    {
        var parts = new string[words.Count];

        for (var i = 0; i < words.Count; i++)
        {
            parts[i] = words[i].Lead + words[i].Core + words[i].Trail;
        }

        return string.Join(Space, parts);
    }

    private static bool IsLowerWord(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsLower(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Rolls(int seed, int salt, int percent) =>
        percent > 0 && Pick(seed, salt, PercentSides) < percent;

    private static int Pick(int seed, int salt, int sides) => (int)(ChoiceSeed.Unit(seed, salt) * sides);
}
