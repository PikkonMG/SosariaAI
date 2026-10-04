using System.Collections.Generic;

namespace SosariaAI.Social;

/// <summary>
/// The lines a talk file starts with when the operator has none. Original lines for SosariaAI,
/// written as 1999 players typed: lowercase, short, era slang, no roleplay and no emotes.
/// </summary>
public static partial class TalkDefaults
{
    /// <summary>Every category with its default lines, in file order.</summary>
    public static readonly IReadOnlyList<TalkTopic> All = [.. Social(), .. Groups(), .. Fighting(), .. Trade(), .. Street()];
}
