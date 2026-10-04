using System;
using System.Collections.Generic;
using System.Text;
using SosariaAI.Behaviour;

namespace SosariaAI.Configuration;

/// <summary>
/// Words a model-written persona may not use. Each era band bans the things that do not
/// exist yet in it (the same split as <see cref="PersonaEras"/>: Age of Shadows content
/// from the ML band on, Stygian Abyss content from the modern band on). Every band bans
/// talk of the world outside Sosaria: machines, programs, and the game itself.
/// </summary>
public static class PersonaEraWords
{
    private const char Space = ' ';
    private const string PluralEnding = "s";
    private const int EdgeSpaces = 2;

    /// <summary>Talk from outside the world. A person in Sosaria never says these.</summary>
    public static readonly string[] OffWorld =
    [
        "ai",
        "artificial intelligence",
        "bot",
        "robot",
        "npc",
        "program",
        "programmed",
        "language model",
        "assistant",
        "chatgpt",
        "gpt",
        "openai",
        "game",
        "gaming",
        "video game",
        "simulation",
        "discord",
        "twitch",
        "youtube",
        "tiktok",
        "smartphone",
        "iphone",
        "wifi",
        "selfie",
        "emoji",
        "podcast",
        "instagram",
        "twitter",
        "facebook",
        "reddit",
        "streamer",
        "google"
    ];

    /// <summary>
    /// Age of Shadows, Samurai Empire and Mondain's Legacy content. Banned in the T2A band.
    /// First Time Setup builds champion spawns from Age of Shadows on, so their talk waits too.
    /// </summary>
    public static readonly string[] AosOnward =
    [
        "champion spawn",
        "champ spawn",
        "power scroll",
        "powerscroll",
        "insurance",
        "insured",
        "bonded",
        "bonding",
        "idoc",
        "malas",
        "luna",
        "umbra",
        "doom",
        "paladin",
        "chivalry",
        "necromancer",
        "necromancy",
        "necro",
        "artifact",
        "arties",
        "tokuno",
        "zento",
        "samurai",
        "ninja",
        "bushido",
        "ninjitsu",
        "elf",
        "elves",
        "elven",
        "heartwood",
        "spellweaving",
        "peerless"
    ];

    /// <summary>Stygian Abyss and later content. Banned in the T2A and ML bands.</summary>
    public static readonly string[] SaOnward =
    [
        "gargish",
        "ter mur",
        "termur",
        "imbuing",
        "imbue",
        "mysticism",
        "high seas",
        "eodon",
        "shadowguard",
        "void pool",
        "stygian"
    ];

    /// <summary>The out-of-era words for <paramref name="band"/>: content that does not exist yet.</summary>
    public static IReadOnlyList<string> NotYetIn(EraBand band) =>
        band switch
        {
            EraBand.T2A => [.. AosOnward, .. SaOnward],
            EraBand.ML => SaOnward,
            _ => []
        };

    /// <summary>The first word in <paramref name="text"/> that the band bans, or null when it is clean.</summary>
    public static string FindBanned(string text, EraBand band)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var padded = Padded(text);
        return FindIn(padded, OffWorld) ?? FindIn(padded, NotYetIn(band));
    }

    /// <summary>
    /// Lowercase letters and digits with every other character turned into one space, and a
    /// space at each end, so a phrase matches only on whole words.
    /// </summary>
    public static string Padded(string text)
    {
        var builder = new StringBuilder(text.Length + EdgeSpaces);
        builder.Append(Space);

        for (var i = 0; i < text.Length; i++)
        {
            var c = char.ToLowerInvariant(text[i]);

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder[^1] != Space)
            {
                builder.Append(Space);
            }
        }

        if (builder[^1] != Space)
        {
            builder.Append(Space);
        }

        return builder.ToString();
    }

    private static string FindIn(string padded, IReadOnlyList<string> words)
    {
        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];

            if (padded.Contains(Space + word + Space, StringComparison.Ordinal) ||
                padded.Contains(Space + word + PluralEnding + Space, StringComparison.Ordinal))
            {
                return word;
            }
        }

        return null;
    }
}
