using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class PersonProfileRulesTests
{
    private const int People = 400;

    private static CharacterDefinition Template(string preset, string role, params string[] work) =>
        new()
        {
            Id = "t",
            Build = new BuildDefinition { Preset = preset, Role = role },
            Routines = new Dictionary<string, List<SkillStepDefinition>>
            {
                ["work"] = Array.ConvertAll(work, kind => new SkillStepDefinition { Skill = kind }).ToList()
            }
        };

    [Fact]
    public void Roll_IsStableForOneId()
    {
        var template = Template(BuildPresets.Swordsman, null, SkillKinds.Hunt);
        var first = PersonProfileRules.Roll("Felucca:bran#3", template);
        var again = PersonProfileRules.Roll("Felucca:bran#3", template);

        Assert.Equal(first.Class, again.Class);
        Assert.Equal(first.Tier, again.Tier);
        Assert.Equal(first.Traits, again.Traits);
        Assert.Equal(first.Wealth, again.Wealth);
        Assert.Equal(first.Female, again.Female);
        Assert.Equal(first.PhaseLengthMultiplier, again.PhaseLengthMultiplier);
    }

    [Fact]
    public void Class_FollowsTheTemplateWork()
    {
        var smithy = Template(null, PersonJobs.Worker, SkillKinds.Mine, SkillKinds.Smith);
        var fishing = Template(null, PersonJobs.Worker, SkillKinds.Fish);
        var thief = Template(BuildPresets.Thief, null, SkillKinds.Steal);
        var mage = Template(BuildPresets.Mage, null, SkillKinds.Mage);
        var archer = Template(BuildPresets.Archer, null, SkillKinds.Hunt);

        for (var i = 0; i < People; i++)
        {
            var id = $"Felucca:p#{i}";
            Assert.Equal(PersonClass.Miner, PersonProfileRules.Roll(id, smithy, Expansion.T2A).Class);
            Assert.Equal(PersonClass.Fisherman, PersonProfileRules.Roll(id, fishing, Expansion.T2A).Class);
            Assert.Equal(PersonClass.Thief, PersonProfileRules.Roll(id, thief, Expansion.T2A).Class);
            Assert.Contains(PersonProfileRules.Roll(id, mage, Expansion.T2A).Class, new[] { PersonClass.Mage, PersonClass.Healer, PersonClass.TreasureHunter });
            Assert.Contains(PersonProfileRules.Roll(id, archer, Expansion.T2A).Class, new[] { PersonClass.Archer, PersonClass.Ranger });
        }
    }

    [Theory]
    [InlineData(Expansion.None)]
    [InlineData(Expansion.T2A)]
    [InlineData(Expansion.UOR)]
    [InlineData(Expansion.LBR)]
    [InlineData(Expansion.AOS)]
    [InlineData(Expansion.SE)]
    [InlineData(Expansion.ML)]
    [InlineData(Expansion.SA)]
    [InlineData(Expansion.EJ)]
    public void Roll_NeverPicksAClassTheEraLacks(Expansion expansion)
    {
        var templates = new[]
        {
            Template(BuildPresets.Swordsman, null, SkillKinds.Hunt, SkillKinds.Music),
            Template(BuildPresets.Mage, null, SkillKinds.Mage),
            Template(BuildPresets.Archer, null, SkillKinds.Hunt),
            Template(null, PersonJobs.Worker, SkillKinds.Lumberjack, SkillKinds.Carpentry)
        };

        for (var i = 0; i < People; i++)
        {
            foreach (var template in templates)
            {
                var personClass = PersonProfileRules.Roll($"Felucca:p#{i}", template, expansion).Class;
                Assert.True(PersonClassRules.Allowed(personClass, expansion), $"{personClass} on {expansion}");
            }
        }
    }

    [Fact]
    public void SecondAge_RollsOnlyTheSecondAgeClasses_AsBefore()
    {
        var fighter = Template(BuildPresets.Swordsman, null, SkillKinds.Hunt, SkillKinds.Music);
        var mage = Template(BuildPresets.Mage, null, SkillKinds.Mage);
        var later = new[] { PersonClass.Paladin, PersonClass.Necromancer, PersonClass.Samurai, PersonClass.Ninja };

        for (var i = 0; i < People; i++)
        {
            var id = $"Felucca:p#{i}";

            Assert.DoesNotContain(PersonProfileRules.Roll(id, fighter, Expansion.LBR).Class, later);
            Assert.DoesNotContain(PersonProfileRules.Roll(id, mage, Expansion.LBR).Class, later);
            Assert.Equal(PersonProfileRules.Roll(id, fighter, Expansion.None).Class, PersonProfileRules.Roll(id, fighter, Expansion.LBR).Class);
        }
    }

    [Fact]
    public void LaterEras_RollTheirOwnClasses()
    {
        var fighter = Template(BuildPresets.Swordsman, null, SkillKinds.Hunt);
        var mage = Template(BuildPresets.Mage, null, SkillKinds.Mage);
        var aos = new HashSet<PersonClass>();
        var se = new HashSet<PersonClass>();

        for (var i = 0; i < People; i++)
        {
            var id = $"Felucca:p#{i}";
            aos.Add(PersonProfileRules.Roll(id, fighter, Expansion.AOS).Class);
            aos.Add(PersonProfileRules.Roll(id, mage, Expansion.AOS).Class);
            se.Add(PersonProfileRules.Roll(id, fighter, Expansion.SE).Class);
        }

        Assert.Contains(PersonClass.Paladin, aos);
        Assert.Contains(PersonClass.Necromancer, aos);
        Assert.DoesNotContain(PersonClass.Samurai, aos);
        Assert.DoesNotContain(PersonClass.Ninja, aos);
        Assert.Contains(PersonClass.Samurai, se);
        Assert.Contains(PersonClass.Ninja, se);
    }

    [Theory]
    [InlineData(PersonClass.Warrior, Expansion.None)]
    [InlineData(PersonClass.Tamer, Expansion.None)]
    [InlineData(PersonClass.Paladin, Expansion.AOS)]
    [InlineData(PersonClass.Necromancer, Expansion.AOS)]
    [InlineData(PersonClass.Samurai, Expansion.SE)]
    [InlineData(PersonClass.Ninja, Expansion.SE)]
    public void ClassFloor_IsTheFirstExpansionWithItsSkill(PersonClass personClass, Expansion floor)
    {
        Assert.Equal(floor, PersonClassRules.Floor(personClass));
        Assert.True(PersonClassRules.Allowed(personClass, floor));
        Assert.True(floor == Expansion.None || !PersonClassRules.Allowed(personClass, floor - 1));
    }

    [Fact]
    public void Bards_ComeOnlyFromTemplatesThatPlayMusic()
    {
        var silent = Template(BuildPresets.Swordsman, null, SkillKinds.Hunt);
        var musical = Template(BuildPresets.Swordsman, null, SkillKinds.Hunt, SkillKinds.Music);
        var bards = 0;

        for (var i = 0; i < People; i++)
        {
            Assert.NotEqual(PersonClass.Bard, PersonProfileRules.Roll($"Felucca:p#{i}", silent, Expansion.T2A).Class);
            bards += PersonProfileRules.Roll($"Felucca:p#{i}", musical, Expansion.T2A).Class == PersonClass.Bard ? 1 : 0;
        }

        Assert.True(bards > 0);
    }

    [Fact]
    public void Traits_NeverHoldBothOfAPair()
    {
        for (var i = 0; i < People; i++)
        {
            var traits = PersonProfileRules.RollTraits($"Felucca:p#{i}");

            Assert.False(Both(traits, PersonTrait.Brave, PersonTrait.Cautious));
            Assert.False(Both(traits, PersonTrait.Restless, PersonTrait.Homebody));
            Assert.False(Both(traits, PersonTrait.Social, PersonTrait.Loner));
            Assert.False(Both(traits, PersonTrait.Greedy, PersonTrait.Generous));
        }
    }

    [Fact]
    public void Restless_SwitchesSooner_ThanAHomebody()
    {
        var restless = PersonProfileRules.RollPhaseLength("Felucca:p#1", PersonTrait.Restless);
        var homebody = PersonProfileRules.RollPhaseLength("Felucca:p#1", PersonTrait.Homebody);
        var plain = PersonProfileRules.RollPhaseLength("Felucca:p#1", PersonTrait.None);

        Assert.True(restless < plain);
        Assert.True(homebody > plain);
    }

    [Fact]
    public void Tendencies_StayBetweenZeroAndOne_AndFollowTheClass()
    {
        for (var i = 0; i < People; i++)
        {
            var id = $"Felucca:p#{i}";
            var traits = PersonProfileRules.RollTraits(id);

            foreach (var personClass in Enum.GetValues<PersonClass>())
            {
                var t = PersonProfileRules.RollTendencies(id, personClass, traits);

                foreach (var value in new[] { t.Banking, t.Adventuring, t.Travel, t.Crafting, t.Idling })
                {
                    Assert.InRange(value, PersonaDrives.MinValue, PersonaDrives.MaxValue);
                }
            }
        }

        var smith = PersonProfileRules.RollTendencies("Felucca:p#1", PersonClass.Smith, PersonTrait.None);
        var warrior = PersonProfileRules.RollTendencies("Felucca:p#1", PersonClass.Warrior, PersonTrait.None);
        Assert.True(smith.Crafting > warrior.Crafting);
        Assert.True(warrior.Adventuring > smith.Adventuring);
    }

    [Fact]
    public void Wealth_GrowsWithTier()
    {
        var noviceRich = 0;
        var grandmasterRich = 0;

        for (var i = 0; i < People; i++)
        {
            var id = $"Felucca:p#{i}";
            noviceRich += PersonProfileRules.RollWealth(id, SkillTier.Novice, PersonClass.Warrior, PersonTrait.None) == PersonWealth.Rich ? 1 : 0;
            grandmasterRich += PersonProfileRules.RollWealth(id, SkillTier.Grandmaster, PersonClass.Warrior, PersonTrait.None) == PersonWealth.Rich ? 1 : 0;
        }

        Assert.Equal(0, noviceRich);
        Assert.True(grandmasterRich > People / 2);
    }

    [Fact]
    public void Nerve_RisesWithValorAndTier()
    {
        var timid = new PersonaDrives(0.5, 0.9, 0.1, isCustom: true);
        var bold = new PersonaDrives(0.5, 0.1, 0.9, isCustom: true);

        Assert.True(PersonProfileRules.Nerve(bold, SkillTier.Journeyman) > PersonProfileRules.Nerve(timid, SkillTier.Journeyman));
        Assert.True(PersonProfileRules.Nerve(bold, SkillTier.Grandmaster) > PersonProfileRules.Nerve(bold, SkillTier.Novice));
        Assert.InRange(PersonProfileRules.Nerve(null, SkillTier.Grandmaster), PersonaDrives.MinValue, PersonaDrives.MaxValue);
    }

    [Fact]
    public void Profile_Has_ReadsOneTrait()
    {
        var profile = new PersonProfile(
            PersonClass.Warrior, SkillTier.Expert, PersonTrait.Brave | PersonTrait.Loner, PersonWealth.Modest,
            ActivityTendencies.Even, PersonProfile.NeutralPhaseLength, female: false
        );

        Assert.True(profile.Has(PersonTrait.Brave));
        Assert.False(profile.Has(PersonTrait.Cautious));
        Assert.False(profile.Has(PersonTrait.None));
        Assert.Equal("Expert Warrior", profile.Describe());
    }

    private static bool Both(PersonTrait traits, PersonTrait first, PersonTrait second) =>
        (traits & first) != 0 && (traits & second) != 0;
}
