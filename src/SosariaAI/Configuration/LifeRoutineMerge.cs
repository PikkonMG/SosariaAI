using System;
using System.Collections.Generic;

namespace SosariaAI.Configuration;

/// <summary>
/// Folds the shared life routines into every character, once, as the file is read.
/// A character that carries its own routine or its own choice keeps it, so a hand
/// edit and an older file both still win. A routine switched off is taken away from
/// everyone, so the switch cannot be undone by a copy left in a roster entry.
/// </summary>
public static class LifeRoutineMerge
{
    public static void Apply(CharactersConfiguration file)
    {
        if (file == null)
        {
            return;
        }

        var shared = file.LifeRoutines is { Count: > 0 } ? file.LifeRoutines : LifeRoutineDefaults.Create();

        if (file.Facets == null)
        {
            return;
        }

        foreach (var facet in file.Facets.Values)
        {
            MergeRoster(facet?.Roster, shared);
        }
    }

    private static void MergeRoster(
        IReadOnlyList<CharacterDefinition> roster,
        IReadOnlyDictionary<string, LifeRoutineDefinition> shared
    )
    {
        if (roster == null)
        {
            return;
        }

        for (var i = 0; i < roster.Count; i++)
        {
            Merge(roster[i], shared);
        }
    }

    private static void Merge(
        CharacterDefinition character,
        IReadOnlyDictionary<string, LifeRoutineDefinition> shared
    )
    {
        if (character == null)
        {
            return;
        }

        character.Routines ??= new Dictionary<string, List<SkillStepDefinition>>(StringComparer.OrdinalIgnoreCase);
        character.Choices ??= [];

        var added = new List<ChoiceDefinition>();

        foreach (var pair in shared)
        {
            var id = pair.Key;
            var routine = pair.Value;

            if (string.IsNullOrWhiteSpace(id) || routine == null)
            {
                continue;
            }

            if (!routine.Enabled)
            {
                character.Routines.Remove(id);
                RemoveChoice(character.Choices, id);
                continue;
            }

            // Each character gets a list of its own, so a change to one character's
            // steps never reaches the others.
            if (!character.Routines.ContainsKey(id) && routine.Steps is { Count: > 0 })
            {
                character.Routines[id] = [.. routine.Steps];
            }

            if (!HasChoice(character.Choices, id))
            {
                added.Add(
                    new ChoiceDefinition
                    {
                        Routine = id,
                        Weight = routine.Weight,
                        Description = routine.Description,
                        RequiredPower = routine.RequiredPower
                    }
                );
            }
        }

        if (added.Count == 0)
        {
            return;
        }

        added.AddRange(character.Choices);
        character.Choices = added;
    }

    private static bool HasChoice(List<ChoiceDefinition> choices, string id)
    {
        for (var i = 0; i < choices.Count; i++)
        {
            if (string.Equals(choices[i]?.Routine, id, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveChoice(List<ChoiceDefinition> choices, string id)
    {
        for (var i = choices.Count - 1; i >= 0; i--)
        {
            if (string.Equals(choices[i]?.Routine, id, StringComparison.OrdinalIgnoreCase))
            {
                choices.RemoveAt(i);
            }
        }
    }
}
