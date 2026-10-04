using System.Collections.Generic;
using System.Linq;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class TamerLifeTests
{
    private const string FieldRoutine = "tame";
    private const string TownRoutine = "town";

    private static CharacterDefinition OldTamer() =>
        new()
        {
            Id = "osric",
            Build = new BuildDefinition { Preset = "tamer" },
            Routines = new Dictionary<string, List<SkillStepDefinition>>
            {
                // The live template: a walk to the Britain field, lore, herding, then taming there.
                [FieldRoutine] =
                [
                    new SkillStepDefinition { Skill = SkillKinds.GoTo },
                    new SkillStepDefinition { Skill = SkillKinds.Lore },
                    new SkillStepDefinition { Skill = SkillKinds.Herd },
                    new SkillStepDefinition { Skill = SkillKinds.Tame }
                ],
                [TownRoutine] = [new SkillStepDefinition { Skill = SkillKinds.IdleWander }]
            },
            Choices =
            [
                new ChoiceDefinition { Routine = FieldRoutine, Weight = 3 },
                new ChoiceDefinition { Routine = TownRoutine, Weight = 1 }
            ]
        };

    [Fact]
    public void Apply_ReplacesTheFieldWalkWithTheTamingTrip()
    {
        var tamer = OldTamer();
        TamerLife.Apply(tamer);

        Assert.Equal(SkillKinds.Tame, tamer.Routines[TamerLife.TameRoutineId][0].Skill);
        Assert.DoesNotContain(tamer.Routines[TamerLife.TameRoutineId], step => step.Skill is SkillKinds.GoTo or SkillKinds.Herd);
        Assert.Contains(tamer.Routines[TamerLife.TameRoutineId], step => step.Skill == SkillKinds.Vet);
    }

    [Fact]
    public void Apply_GivesAHuntWithThePets_AndKeepsTheRest()
    {
        var tamer = OldTamer();
        TamerLife.Apply(tamer);

        Assert.Equal(SkillKinds.Hunt, tamer.Routines[TamerLife.HuntRoutineId][0].Skill);
        Assert.Null(tamer.Routines[TamerLife.HuntRoutineId][0].Party);
        Assert.True(tamer.Routines.ContainsKey(TownRoutine));
        Assert.Equal(1, tamer.Choices.Count(choice => choice.Routine == TamerLife.TameRoutineId));
        Assert.Contains(tamer.Choices, choice => choice.Routine == TamerLife.HuntRoutineId && choice.Weight == TamerLife.HuntChoiceWeight);
        Assert.Contains(tamer.Choices, choice => choice.Routine == TownRoutine);
    }

    [Fact]
    public void Apply_NeverChangesTheTemplatesOwnLists()
    {
        var template = OldTamer();
        var routines = template.Routines;
        var fieldWalk = routines[FieldRoutine];
        var composed = new CharacterDefinition { Id = template.Id, Routines = routines, Choices = template.Choices };

        TamerLife.Apply(composed);

        Assert.Same(fieldWalk, routines[FieldRoutine]);
        Assert.Equal(SkillKinds.GoTo, routines[FieldRoutine][0].Skill);
        Assert.Equal(2, template.Choices.Count);
    }

    [Fact]
    public void ComposedTamer_GetsTheTamerLife()
    {
        var roster = new List<CharacterDefinition> { OldTamer() };
        var personas = new List<Persona> { new() { Id = "osric", Jobs = [PersonJobs.Tamer] } };

        for (var i = 0; i < 200; i++)
        {
            var id = $"Felucca:osric#{i}";

            if (PersonMaker.RollJob(id) != PersonJobs.Tamer)
            {
                continue;
            }

            var composed = PersonMaker.Compose(id, roster[0], roster, personas, EraBand.T2A);
            Assert.Equal(SkillKinds.Tame, composed.Routines[TamerLife.TameRoutineId][0].Skill);
            Assert.True(composed.Routines.ContainsKey(TamerLife.HuntRoutineId));
            return;
        }

        Assert.Fail("no tamer among two hundred copies");
    }
}
