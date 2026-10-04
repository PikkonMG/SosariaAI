using System;
using System.Collections.Generic;

namespace SosariaAI.Configuration;

/// <summary>
/// The life routines every character shares, kept in one place. A change here, such as
/// how often anyone wants a mount, reaches every roster entry, and a new routine also
/// reaches a file written before the routine existed. A routine that holds a place of
/// its own, such as loiter, stays with its character.
/// </summary>
public static class LifeRoutineDefaults
{
    public const string Tavern = "tavern";
    public const string Visit = "visit";
    public const string Sightsee = "sightsee";
    public const string House = "house";
    public const string Stable = "stable";
    public const string BankCrowd = "bankcrowd";

    public const int TavernWeight = 2;

    public static Dictionary<string, LifeRoutineDefinition> Create() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            [Tavern] = new LifeRoutineDefinition
            {
                Weight = TavernWeight,
                Description = "have a drink at the inn",
                Steps =
                [
                    new SkillStepDefinition { Skill = SkillKinds.Tavern },
                    new SkillStepDefinition { Skill = SkillKinds.Cook },
                    new SkillStepDefinition { Skill = SkillKinds.Taste },
                    new SkillStepDefinition { Skill = SkillKinds.Decide }
                ]
            },
            [BankCrowd] = new LifeRoutineDefinition
            {
                Description = "stand with the bank crowd",
                Steps =
                [
                    new SkillStepDefinition { Skill = SkillKinds.BankCrowd },
                    new SkillStepDefinition { Skill = SkillKinds.Decide }
                ]
            },
            [Visit] = new LifeRoutineDefinition
            {
                Description = "go see someone",
                Steps =
                [
                    new SkillStepDefinition { Skill = SkillKinds.Visit },
                    new SkillStepDefinition { Skill = SkillKinds.Decide }
                ]
            },
            [Sightsee] = new LifeRoutineDefinition
            {
                Description = "go look at a place",
                Steps =
                [
                    new SkillStepDefinition { Skill = SkillKinds.Sightsee },
                    new SkillStepDefinition { Skill = SkillKinds.Decide }
                ]
            },
            [House] = new LifeRoutineDefinition
            {
                Description = "buy a small house",
                Steps =
                [
                    new SkillStepDefinition { Skill = SkillKinds.House },
                    new SkillStepDefinition { Skill = SkillKinds.PlayerVendor },
                    new SkillStepDefinition { Skill = SkillKinds.Decide }
                ]
            },
            [Stable] = new LifeRoutineDefinition
            {
                Description = "buy a mount at the stable",
                Steps =
                [
                    new SkillStepDefinition { Skill = SkillKinds.BuyMount },
                    new SkillStepDefinition { Skill = SkillKinds.Mount },
                    new SkillStepDefinition { Skill = SkillKinds.Decide }
                ]
            }
        };
}
