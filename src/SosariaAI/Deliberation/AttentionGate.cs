using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Spawning;

namespace SosariaAI.Deliberation;

/// <summary>
/// Decides whether a character listens to a line of speech. A line that names the
/// character, by its whole name or the short one people call it by, always gets through and opens an attention window for that speaker. While
/// the window is open, the same speaker gets through without the name. Everything else
/// is ignored, so no model call is made for talk that was not aimed at the character.
/// </summary>
public sealed class AttentionGate
{
    private const char Apostrophe = '\'';
    private static readonly char[] NameTrimCharacters = [' ', ',', '.', '!', '?', ':', ';', Apostrophe, '"'];

    private readonly Dictionary<(Serial Character, Serial Speaker), DateTime> _windows = new();

    public bool ShouldListen(Serial character, string characterName, Serial speaker, string text, DateTime now, TimeSpan window)
    {
        if (MentionsName(text, characterName))
        {
            Open(character, speaker, now, window);
            return true;
        }

        return _windows.TryGetValue((character, speaker), out var until) && now < until;
    }

    public void Open(Serial character, Serial speaker, DateTime now, TimeSpan window) =>
        _windows[(character, speaker)] = now + window;

    /// <summary>True when the line is nothing but the character's name, or its calling name, with any punctuation around it.</summary>
    public static bool IsOnlyName(string text, string characterName)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(characterName))
        {
            return false;
        }

        var said = text.Trim(NameTrimCharacters);

        return said.Equals(characterName.Trim(), StringComparison.OrdinalIgnoreCase) ||
               PlayerNameRules.CallingName(characterName) is { } called && said.Equals(called, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when the name, or the calling name people use for it ("Robard" for "Robard
    /// Fairbairn"), appears as a whole word anywhere in the line.
    /// </summary>
    public static bool MentionsName(string text, string characterName)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(characterName))
        {
            return false;
        }

        return MentionsWord(text, characterName.Trim()) ||
               PlayerNameRules.CallingName(characterName) is { } called && MentionsWord(text, called);
    }

    private static bool MentionsWord(string text, string name)
    {
        var index = text.IndexOf(name, StringComparison.OrdinalIgnoreCase);

        while (index >= 0)
        {
            // An apostrophe before the name means another name (O'Connor), so it does not count.
            var before = index == 0 || !char.IsLetterOrDigit(text[index - 1]) && text[index - 1] != Apostrophe;
            var after = index + name.Length >= text.Length || !char.IsLetterOrDigit(text[index + name.Length]);

            if (before && after)
            {
                return true;
            }

            index = text.IndexOf(name, index + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
