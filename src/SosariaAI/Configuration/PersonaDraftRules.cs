using System;
using System.Collections.Generic;
using System.Text.Json;
using SosariaAI.Behaviour;
using SosariaAI.Deliberation;

namespace SosariaAI.Configuration;

/// <summary>
/// Hard checks on a model-written persona. A line with an emote, a stray symbol, the wrong
/// length or a copy of another line is dropped, and so is a line that promises a group, a
/// trip or a meeting (<see cref="PromiseLines"/>), because a written line is said at random
/// and nothing keeps the promise. The prompt asks for spare lines, so a drop takes the next
/// one; a list left short fails the draft. A
/// background that runs long is cut to its first sentences, because a paid answer is worth
/// keeping over one extra sentence. A word from outside the world or from a later era
/// anywhere that is kept fails the whole draft, because it shows the model lost the brief.
/// A failed draft leaves the composed persona in place.
/// </summary>
public static class PersonaDraftRules
{
    public const int LikeCount = 3;
    public const int DislikeCount = 3;
    public const int WantCount = 2;
    public const int IdleCount = 12;
    public const int GreetingCount = 6;
    public const int ReturnCount = 3;
    public const int CombatCount = 4;
    public const int LootCount = 3;

    public const int BackgroundMinCharacters = 20;
    public const int BackgroundMaxCharacters = 240;
    public const int BackgroundMaxSentences = 2;
    public const int VoiceMinCharacters = 10;
    public const int VoiceMaxCharacters = 200;
    public const int ItemMinCharacters = 2;
    public const int ItemMaxCharacters = 48;
    public const int WantMinCharacters = 4;
    public const int WantMaxCharacters = 80;
    public const int LineMinCharacters = 2;
    public const int LineMaxCharacters = 80;

    private const char FirstPrintable = ' ';
    private const char LastPrintable = '~';
    private const char PlaceholderOpen = '{';
    private const char PlaceholderClose = '}';
    private const string EmoteSlash = "/me";
    private const string SentenceMarks = ".!?";
    private const char WordBreak = ' ';
    private const string SentenceEnd = ".";

    private static readonly char[] WantTrim = ['.', '!', '?', ';', ',', ' '];

    /// <summary>Characters that mark an emote, markup, a command or a quote. No 1999 chat line held them.</summary>
    private static readonly char[] BannedMarks = ['*', '<', '>', '[', ']', '"', '~', '_', '|', '\\', '^', '`'];

    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The model's reply read as a draft, or null when it holds no JSON object of that shape.</summary>
    public static PersonaDraft Parse(string raw)
    {
        var json = ReplyParser.ExtractJson(raw);

        if (json == null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PersonaDraft>(json, ReadOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// A clean copy of <paramref name="draft"/> for <paramref name="band"/>, with each list cut
    /// to its count, or null with the reason it failed.
    /// </summary>
    public static PersonaDraft Vet(PersonaDraft draft, EraBand band, out string reason)
    {
        if (draft == null)
        {
            reason = "no JSON object";
            return null;
        }

        var background = FitBackground(draft.Background);
        var banned = FindBannedWord(draft, background, band);

        if (banned != null)
        {
            reason = $"uses \"{banned}\"";
            return null;
        }

        if (!IsText(background, BackgroundMinCharacters, BackgroundMaxCharacters))
        {
            reason = "bad background";
            return null;
        }

        var voice = draft.Voice?.Trim();

        if (!IsText(voice, VoiceMinCharacters, VoiceMaxCharacters))
        {
            reason = "bad voice";
            return null;
        }

        var spoken = new HashSet<string>(StringComparer.Ordinal);
        var clean = new PersonaDraft
        {
            CharacterId = draft.CharacterId,
            Era = draft.Era,
            Background = background,
            Voice = voice,
            Likes = Items(draft.Likes, ItemMinCharacters, ItemMaxCharacters, LikeCount),
            Dislikes = Items(draft.Dislikes, ItemMinCharacters, ItemMaxCharacters, DislikeCount),
            Wants = Wants(draft.Wants),
            IdleLines = Lines(draft.IdleLines, IdleCount, spoken, allowName: false),
            GreetingLines = Lines(draft.GreetingLines, GreetingCount, spoken, allowName: true),
            ReturnLines = Lines(draft.ReturnLines, ReturnCount, spoken, allowName: false),
            CombatLines = Lines(draft.CombatLines, CombatCount, spoken, allowName: false),
            LootLines = Lines(draft.LootLines, LootCount, spoken, allowName: false)
        };

        reason = ShortList(clean);
        return reason == null ? clean : null;
    }

    /// <summary>True for a speech line a 1999 player could have typed: plain ASCII, no emote, no markup.</summary>
    public static bool IsChatLine(string line, bool allowName)
    {
        if (!IsText(line, LineMinCharacters, LineMaxCharacters) ||
            line.Contains(EmoteSlash, StringComparison.OrdinalIgnoreCase) ||
            line.IndexOfAny(BannedMarks) >= 0)
        {
            return false;
        }

        var withoutName = allowName
            ? line.Replace(Persona.NamePlaceholder, string.Empty, StringComparison.OrdinalIgnoreCase)
            : line;
        return withoutName.IndexOf(PlaceholderOpen) < 0 && withoutName.IndexOf(PlaceholderClose) < 0;
    }

    /// <summary>
    /// The background cut to at most <see cref="BackgroundMaxSentences"/> sentences and
    /// <see cref="BackgroundMaxCharacters"/> characters. A cut inside a sentence ends on a
    /// whole word and a full stop. Null for no text.
    /// </summary>
    public static string FitBackground(string background)
    {
        var text = background?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var sentences = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var endsHere = SentenceMarks.IndexOf(text[i]) >= 0 &&
                           (i == text.Length - 1 || char.IsWhiteSpace(text[i + 1]));

            if (endsHere && ++sentences == BackgroundMaxSentences)
            {
                text = text[..(i + 1)];
                break;
            }
        }

        if (text.Length <= BackgroundMaxCharacters)
        {
            return text;
        }

        var cut = text[..BackgroundMaxCharacters];
        var lastEnd = cut.LastIndexOfAny(SentenceMarks.ToCharArray());

        if (lastEnd >= BackgroundMinCharacters)
        {
            return cut[..(lastEnd + 1)];
        }

        var lastWord = cut.LastIndexOf(WordBreak);
        var words = lastWord > 0 ? cut[..lastWord] : cut[..(BackgroundMaxCharacters - SentenceEnd.Length)];
        return words.TrimEnd(WantTrim) + SentenceEnd;
    }

    private static string FindBannedWord(PersonaDraft draft, string background, EraBand band)
    {
        var texts = new List<string> { background, draft.Voice };
        AddAll(texts, draft.Likes);
        AddAll(texts, draft.Dislikes);
        AddAll(texts, draft.Wants);
        AddAll(texts, draft.IdleLines);
        AddAll(texts, draft.GreetingLines);
        AddAll(texts, draft.ReturnLines);
        AddAll(texts, draft.CombatLines);
        AddAll(texts, draft.LootLines);

        for (var i = 0; i < texts.Count; i++)
        {
            var banned = PersonaEraWords.FindBanned(texts[i], band);

            if (banned != null)
            {
                return banned;
            }
        }

        return null;
    }

    private static void AddAll(List<string> into, List<string> from)
    {
        if (from != null)
        {
            into.AddRange(from);
        }
    }

    private static List<string> Items(List<string> source, int min, int max, int take)
    {
        var kept = new List<string>(take);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (source == null)
        {
            return kept;
        }

        for (var i = 0; i < source.Count && kept.Count < take; i++)
        {
            var item = source[i]?.Trim();

            if (IsText(item, min, max) && item.IndexOfAny(BannedMarks) < 0 && seen.Add(item))
            {
                kept.Add(item);
            }
        }

        return kept;
    }

    private static List<string> Wants(List<string> source)
    {
        var trimmed = new List<string>();

        if (source != null)
        {
            for (var i = 0; i < source.Count; i++)
            {
                var want = source[i]?.Trim().TrimEnd(WantTrim);

                if (!string.IsNullOrEmpty(want))
                {
                    trimmed.Add(want);
                }
            }
        }

        return Items(trimmed, WantMinCharacters, WantMaxCharacters, WantCount);
    }

    private static List<string> Lines(List<string> source, int take, HashSet<string> spoken, bool allowName)
    {
        var kept = new List<string>(take);

        if (source == null)
        {
            return kept;
        }

        for (var i = 0; i < source.Count && kept.Count < take; i++)
        {
            var line = source[i]?.Trim();

            if (IsChatLine(line, allowName) &&
                !PromiseLines.IsPromise(line) && !OrderLines.IsOrderTalk(line) && !OrderLines.ClaimsRepairs(line) &&
                spoken.Add(SpokenRepeat.Normalize(line)))
            {
                kept.Add(line);
            }
        }

        return kept;
    }

    private static string ShortList(PersonaDraft clean)
    {
        if (clean.Likes.Count < LikeCount)
        {
            return "too few likes";
        }

        if (clean.Dislikes.Count < DislikeCount)
        {
            return "too few dislikes";
        }

        if (clean.Wants.Count < WantCount)
        {
            return "too few wants";
        }

        if (clean.IdleLines.Count < IdleCount)
        {
            return "too few idle lines";
        }

        if (clean.GreetingLines.Count < GreetingCount)
        {
            return "too few greeting lines";
        }

        if (clean.ReturnLines.Count < ReturnCount)
        {
            return "too few return lines";
        }

        if (clean.CombatLines.Count < CombatCount)
        {
            return "too few combat lines";
        }

        return clean.LootLines.Count < LootCount ? "too few loot lines" : null;
    }

    private static bool IsText(string text, int min, int max)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < min || text.Length > max)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] < FirstPrintable || text[i] > LastPrintable)
            {
                return false;
            }
        }

        return true;
    }
}
