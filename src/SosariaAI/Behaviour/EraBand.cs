using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Behaviour;

/// <summary>
/// The three eras the plugin's content is written for. ModernUO's expansion decides the
/// band: the plugin keeps no era switch of its own.
/// </summary>
public enum EraBand
{
    /// <summary>1998–2002: None, The Second Age, Renaissance, Third Dawn, Blackthorn's Revenge.</summary>
    T2A,

    /// <summary>2003–2007: Age of Shadows, Samurai Empire, Mondain's Legacy.</summary>
    ML,

    /// <summary>2009 on: Stygian Abyss, High Seas, Time of Legends, Endless Journey.</summary>
    Modern
}

public static class EraBands
{
    /// <summary>The tag written in persona and part files: "t2a", "ml", "modern".</summary>
    public const string T2ATag = "t2a";
    public const string MLTag = "ml";
    public const string ModernTag = "modern";

    public static EraBand Current() => Of(EraRules.Current());

    public static EraBand Of(Expansion expansion) =>
        expansion switch
        {
            < Expansion.AOS => EraBand.T2A,
            < Expansion.SA => EraBand.ML,
            _ => EraBand.Modern
        };

    public static string Tag(EraBand band) =>
        band switch
        {
            EraBand.T2A => T2ATag,
            EraBand.ML => MLTag,
            _ => ModernTag
        };

    /// <summary>
    /// True when content tagged with <paramref name="eras"/> belongs in <paramref name="band"/>.
    /// Untagged content fits every era.
    /// </summary>
    public static bool Fits(IReadOnlyCollection<string> eras, EraBand band)
    {
        if (eras == null || eras.Count == 0)
        {
            return true;
        }

        var tag = Tag(band);

        foreach (var era in eras)
        {
            if (string.Equals(era, tag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
