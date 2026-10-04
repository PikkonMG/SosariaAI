using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Combat;

public static class DecideChoice
{
    public const string DefaultRoutineId = "default";

    /// <summary>
    /// The hand-typed choice value wins when it is set. Catalog difficulty is
    /// the fallback when the choice has no number of its own.
    /// </summary>
    public static int RequiredPowerOf(ChoiceDefinition choice, DestinationCatalog catalog)
    {
        if (choice == null)
        {
            return 0;
        }

        var authored = choice.RequiredPower.GetValueOrDefault();

        if (authored > 0)
        {
            return authored;
        }

        if (catalog != null && !string.IsNullOrWhiteSpace(choice.Routine))
        {
            var dest = catalog.GetByName(choice.Routine) ?? catalog.Resolve(choice.Routine, Point3D.Zero);

            if (dest?.Difficulty is int difficulty && difficulty > 0)
            {
                return difficulty;
            }
        }

        return 0;
    }

    public static int EffectivePower(int personal, int alliesPower, bool partyContent)
    {
        if (!partyContent || alliesPower <= 0)
        {
            return personal;
        }

        return personal + (int)(alliesPower * ThreatRating.PartyPowerShare);
    }

    public static string CanonicalChoose(string chosen, IReadOnlyList<ChoiceDefinition> choices)
    {
        if (choices == null || choices.Count == 0 || string.IsNullOrWhiteSpace(chosen))
        {
            return chosen;
        }

        for (var i = 0; i < choices.Count; i++)
        {
            if (string.Equals(choices[i].Routine, chosen, StringComparison.OrdinalIgnoreCase))
            {
                return choices[i].Routine;
            }
        }

        if (chosen.Equals(Deliberation.ImmediateActs.GoHunt, StringComparison.OrdinalIgnoreCase) ||
            chosen.Equals("hunt", StringComparison.OrdinalIgnoreCase))
        {
            return FirstContaining(choices, "graveyard") ?? FirstContaining(choices, "hunt");
        }

        if (chosen.Equals(Deliberation.ImmediateActs.GoTown, StringComparison.OrdinalIgnoreCase))
        {
            return FirstContaining(choices, "town");
        }

        return chosen;
    }

    private static string FirstContaining(IReadOnlyList<ChoiceDefinition> choices, string token)
    {
        for (var i = 0; i < choices.Count; i++)
        {
            var id = choices[i].Routine;

            if (!string.IsNullOrWhiteSpace(id) &&
                id.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>True when a character is strong enough for what this choice asks of it.</summary>
    public static bool MeetsPower(ChoiceDefinition choice, int power, DestinationCatalog catalog = null)
    {
        var required = RequiredPowerOf(choice, catalog);
        return required <= 0 || power >= required;
    }

    /// <summary>
    /// The choice a language model asked for, but only if the character can survive it.
    /// Returns null when the model picked content above the character's power, so the
    /// caller can fall back to the free scorer instead of sending it to die.
    /// </summary>
    public static string ResolveWithinPower(
        string chosen,
        IReadOnlyList<ChoiceDefinition> choices,
        int power,
        DestinationCatalog catalog = null
    )
    {
        if (choices == null || choices.Count == 0 || string.IsNullOrWhiteSpace(chosen))
        {
            return null;
        }

        chosen = CanonicalChoose(chosen, choices);

        for (var i = 0; i < choices.Count; i++)
        {
            if (!string.Equals(choices[i].Routine, chosen, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return MeetsPower(choices[i], power, catalog) ? choices[i].Routine : null;
        }

        return null;
    }

    public static string Resolve(string chosen, IReadOnlyList<ChoiceDefinition> choices)
    {
        if (choices == null || choices.Count == 0)
        {
            return DefaultRoutineId;
        }

        if (!string.IsNullOrWhiteSpace(chosen))
        {
            for (var i = 0; i < choices.Count; i++)
            {
                if (string.Equals(choices[i].Routine, chosen, StringComparison.OrdinalIgnoreCase))
                {
                    return choices[i].Routine;
                }
            }
        }

        return choices[0].Routine;
    }

    public static string Fallback(IReadOnlyList<ChoiceDefinition> choices, Func<int, int> next)
    {
        if (choices == null || choices.Count == 0)
        {
            return DefaultRoutineId;
        }

        var weighted = new (string Id, int Weight)[choices.Count];

        for (var i = 0; i < choices.Count; i++)
        {
            weighted[i] = (choices[i].Routine, choices[i].Weight);
        }

        return WeightedChoice.Pick(weighted, next) ?? choices[0].Routine;
    }
}
