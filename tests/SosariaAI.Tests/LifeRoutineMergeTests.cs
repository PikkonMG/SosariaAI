using System;
using System.Collections.Generic;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public sealed class LifeRoutineMergeTests
{
    private const string PersonId = "connor";
    private const string OwnWeightDescription = "go to the stable far less often";
    private const int OwnWeight = 7;

    [Fact]
    public void Apply_NoBlockInTheFile_FoldsInTheBuiltInRoutines()
    {
        // An older file carries no block. It must still get every life routine.
        var file = FileWith(Person());

        LifeRoutineMerge.Apply(file);

        var person = FirstPerson(file);
        Assert.Equal(
            [SkillKinds.BuyMount, SkillKinds.Mount, SkillKinds.Decide],
            Skills(person, LifeRoutineDefaults.Stable)
        );
        Assert.Equal("buy a mount at the stable", ChoiceFor(person, LifeRoutineDefaults.Stable).Description);
    }

    [Fact]
    public void Apply_CharacterCarriesItsOwn_KeepsTheCharacterStepsAndWeight()
    {
        // A hand edit, and every roster written before the block existed, must win.
        var person = Person(
            routines: new Dictionary<string, List<SkillStepDefinition>>(StringComparer.OrdinalIgnoreCase)
            {
                [LifeRoutineDefaults.Stable] = [new SkillStepDefinition { Skill = SkillKinds.Mount }]
            },
            choices:
            [
                new ChoiceDefinition
                {
                    Routine = LifeRoutineDefaults.Stable,
                    Weight = OwnWeight,
                    Description = OwnWeightDescription
                }
            ]
        );

        LifeRoutineMerge.Apply(FileWith(person));

        Assert.Equal([SkillKinds.Mount], Skills(person, LifeRoutineDefaults.Stable));
        Assert.Equal(OwnWeight, ChoiceFor(person, LifeRoutineDefaults.Stable).Weight);
        Assert.Equal(OwnWeightDescription, ChoiceFor(person, LifeRoutineDefaults.Stable).Description);
    }

    [Fact]
    public void Apply_RoutineSwitchedOff_TakesItFromACharacterThatCarriesACopy()
    {
        // The switch is the point of the block. A copy left in a roster entry must not
        // bring mount buying back after the operator turned it off.
        var person = Person(
            routines: new Dictionary<string, List<SkillStepDefinition>>(StringComparer.OrdinalIgnoreCase)
            {
                [LifeRoutineDefaults.Stable] =
                [
                    new SkillStepDefinition { Skill = SkillKinds.BuyMount },
                    new SkillStepDefinition { Skill = SkillKinds.Decide }
                ]
            },
            choices: [new ChoiceDefinition { Routine = LifeRoutineDefaults.Stable }]
        );

        var block = LifeRoutineDefaults.Create();
        block[LifeRoutineDefaults.Stable].Enabled = false;

        LifeRoutineMerge.Apply(FileWith(person, block));

        Assert.False(person.Routines.ContainsKey(LifeRoutineDefaults.Stable));
        Assert.Null(ChoiceFor(person, LifeRoutineDefaults.Stable));
        Assert.NotNull(ChoiceFor(person, LifeRoutineDefaults.Tavern));
    }

    [Fact]
    public void Apply_TwoCharacters_EachGetsAListOfItsOwn()
    {
        // The boot patchers insert steps into these lists. One shared list would take
        // every insert once per character.
        var first = Person();
        var second = Person();
        var file = FileWith(first);
        file.Facets[FacetNames.Felucca].Roster.Add(second);

        LifeRoutineMerge.Apply(file);

        Assert.NotSame(
            first.Routines[LifeRoutineDefaults.Tavern],
            second.Routines[LifeRoutineDefaults.Tavern]
        );
    }

    [Fact]
    public void Apply_RunTwice_AddsNothingTheSecondTime()
    {
        // The load path can fold the block in more than once. Doing so must not stack
        // a second copy of every choice.
        var person = Person();
        var file = FileWith(person);

        LifeRoutineMerge.Apply(file);
        var afterFirst = person.Choices.Count;
        LifeRoutineMerge.Apply(file);

        Assert.Equal(afterFirst, person.Choices.Count);
    }

    private static CharacterDefinition Person(
        Dictionary<string, List<SkillStepDefinition>> routines = null,
        List<ChoiceDefinition> choices = null
    ) =>
        new()
        {
            Id = PersonId,
            Routines = routines,
            Choices = choices
        };

    private static CharactersConfiguration FileWith(
        CharacterDefinition person,
        Dictionary<string, LifeRoutineDefinition> block = null
    ) =>
        new()
        {
            LifeRoutines = block,
            Facets = new Dictionary<string, FacetContent>(StringComparer.OrdinalIgnoreCase)
            {
                [FacetNames.Felucca] = new FacetContent { Roster = [person] }
            }
        };

    private static CharacterDefinition FirstPerson(CharactersConfiguration file) =>
        file.Facets[FacetNames.Felucca].Roster[0];

    private static List<string> Skills(CharacterDefinition person, string routineId)
    {
        var found = new List<string>();

        foreach (var step in person.Routines[routineId])
        {
            found.Add(step.Skill);
        }

        return found;
    }

    private static ChoiceDefinition ChoiceFor(CharacterDefinition person, string routineId)
    {
        foreach (var choice in person.Choices)
        {
            if (string.Equals(choice.Routine, routineId, StringComparison.OrdinalIgnoreCase))
            {
                return choice;
            }
        }

        return null;
    }
}
