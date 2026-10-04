using System.Collections.Generic;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class CharacterDefinitionTests
{
    [Fact]
    public void UsesSkill_WorkRoutineBehindDecide_IsFound()
    {
        // A new woodcutter starts on its decide routine. It still needs a hatchet for the
        // work routine it will pick next.
        var definition = new CharacterDefinition
        {
            Routines = new Dictionary<string, List<SkillStepDefinition>>
            {
                ["default"] = [new SkillStepDefinition { Skill = SkillKinds.Decide }],
                ["work"] =
                [
                    new SkillStepDefinition { Skill = SkillKinds.GoTo },
                    new SkillStepDefinition { Skill = SkillKinds.Lumberjack }
                ]
            }
        };

        Assert.True(definition.UsesSkill(SkillKinds.Lumberjack));
        Assert.False(definition.UsesSkill(SkillKinds.Fish));
    }

    [Fact]
    public void UsesSkill_ForTheShippedWoodcutter_IsTrue()
    {
        var connor = CharactersFile.CreateDefault().Facets[FacetNames.Felucca].Roster[0];

        Assert.True(connor.UsesSkill(SkillKinds.Lumberjack));
    }
}
