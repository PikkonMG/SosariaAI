using System;
using System.Collections.Generic;
using SosariaAI.Configuration;

namespace SosariaAI.Skills;

/// <summary>
/// The days of a tamer: taming trips to the grounds of strong beasts (<see cref="TameSkill"/>)
/// with a bandage for the pet after, and hunts with its pets at heel. A composed tamer gets
/// these routines and their choices next to whatever routines its template gives it, the way a
/// composed worker gets its trade (<see cref="CraftCareerRules.Apply"/>); the template is never
/// changed, so a tamer template the operator writes gets them too. Its dungeon runs come
/// from the catalog hall every fighter delves.
/// </summary>
public static class TamerLife
{
    public const string TameRoutineId = "tame";
    public const string HuntRoutineId = "hunt";
    public const int TameChoiceWeight = 3;
    public const int HuntChoiceWeight = 2;
    public const string TameChoiceDescription = "go out and tame a strong beast";
    public const string HuntChoiceDescription = "hunt with the pets";

    /// <summary>The trip, the tamer's travel inside it, then a bandage for any pet it hurt.</summary>
    public static List<SkillStepDefinition> TameTrip() =>
    [
        new SkillStepDefinition { Skill = SkillKinds.Tame },
        new SkillStepDefinition { Skill = SkillKinds.Vet },
        new SkillStepDefinition { Skill = SkillKinds.Decide }
    ];

    /// <summary>A hunt on the tamer's own ground, the pets on its foes by "all kill".</summary>
    public static List<SkillStepDefinition> PetHunt() => CharactersFile.GraveyardTrip(party: null);

    /// <summary>Gives a freshly composed tamer its taming trip and its hunt, and their choices.</summary>
    public static void Apply(CharacterDefinition composed)
    {
        if (composed == null)
        {
            return;
        }

        var routines = new Dictionary<string, List<SkillStepDefinition>>(composed.ResolvedRoutines(), StringComparer.OrdinalIgnoreCase)
        {
            [TameRoutineId] = TameTrip(),
            [HuntRoutineId] = PetHunt()
        };
        var choices = new List<ChoiceDefinition>();

        foreach (var choice in composed.Choices ?? [])
        {
            if (choice?.Routine != null && !IsOwnRoutine(choice.Routine))
            {
                choices.Add(choice);
            }
        }

        choices.Add(new ChoiceDefinition { Routine = TameRoutineId, Weight = TameChoiceWeight, Description = TameChoiceDescription });
        choices.Add(new ChoiceDefinition { Routine = HuntRoutineId, Weight = HuntChoiceWeight, Description = HuntChoiceDescription });
        composed.Routines = routines;
        composed.Choices = choices;
    }

    private static bool IsOwnRoutine(string routine) =>
        string.Equals(routine, TameRoutineId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(routine, HuntRoutineId, StringComparison.OrdinalIgnoreCase);
}
