using System;
using System.Collections.Generic;
using SosariaAI.Spawning;

namespace SosariaAI.Deliberation;

/// <summary>
/// One plain sentence that tells a model who the character is in the world: skill level and
/// class, traits, purse, current job, guild tag, and a murderer's name. A model that does not
/// know a person is a mage lets a mage talk like a miner.
/// </summary>
public static class IdentityLine
{
    public static string For(
        string tierAndClass,
        PersonTrait traits,
        PersonWealth wealth,
        string job,
        string guildTag,
        bool murderer
    )
    {
        var parts = new List<string> { $"You are a {tierAndClass.ToLowerInvariant()}" };
        var traitWords = TraitWords(traits);

        if (traitWords.Count > 0)
        {
            parts.Add(string.Join(" and ", traitWords));
        }

        parts.Add(WealthWord(wealth));

        if (!string.IsNullOrWhiteSpace(job))
        {
            parts.Add($"your job today: {job.ToLowerInvariant()}");
        }

        if (!string.IsNullOrWhiteSpace(guildTag))
        {
            parts.Add($"you wear the guild tag [{guildTag}]");
        }

        if (murderer)
        {
            parts.Add("you are a known murderer");
        }

        return string.Join(", ", parts) + ".";
    }

    /// <summary>Tier and class only, for a paid decision state: "a veteran warrior", and a murderer's name.</summary>
    public static string Brief(string tierAndClass, bool murderer)
    {
        var who = $"a {tierAndClass.ToLowerInvariant()}";
        return murderer ? who + ", a known murderer" : who;
    }

    public static List<string> TraitWords(PersonTrait traits)
    {
        var words = new List<string>();

        foreach (var trait in Enum.GetValues<PersonTrait>())
        {
            if (trait != PersonTrait.None && traits.HasFlag(trait))
            {
                words.Add(trait.ToString().ToLowerInvariant());
            }
        }

        return words;
    }

    public static string WealthWord(PersonWealth wealth) =>
        wealth switch
        {
            PersonWealth.Poor => "short on coin",
            PersonWealth.Modest => "getting by",
            PersonWealth.Comfortable => "comfortable",
            _ => "rich"
        };
}
