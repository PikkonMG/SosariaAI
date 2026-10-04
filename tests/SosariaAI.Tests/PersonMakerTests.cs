using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class PersonMakerTests
{
    private static CharacterDefinition Template(string id, string preset, string role, string job)
    {
        var skill = job switch
        {
            PersonJobs.Worker => SkillKinds.Lumberjack,
            PersonJobs.Thief => SkillKinds.Steal,
            PersonJobs.Tamer => SkillKinds.Tame,
            _ => SkillKinds.Hunt
        };

        return new CharacterDefinition
        {
            Id = id,
            Persona = id,
            Build = new BuildDefinition { Preset = preset, Role = role },
            Routines = new Dictionary<string, List<SkillStepDefinition>>
            {
                ["work"] = [new SkillStepDefinition { Skill = skill }]
            }
        };
    }

    private static readonly List<CharacterDefinition> Roster =
    [
        Template("connor", null, "worker", PersonJobs.Worker),
        Template("mira", null, "worker", PersonJobs.Worker),
        Template("bran", "swordsman", null, PersonJobs.Fighter),
        Template("nyle", "thief", null, PersonJobs.Thief),
        Template("osric", "tamer", null, PersonJobs.Tamer),
        new CharacterDefinition
        {
            Id = "sela",
            Persona = "sela",
            Build = new BuildDefinition { Preset = "mage" },
            Routines = new Dictionary<string, List<SkillStepDefinition>>
            {
                ["default"] = [new SkillStepDefinition { Skill = SkillKinds.Follow }]
            }
        }
    ];

    private static readonly List<Persona> Personas =
    [
        new() { Id = "connor", Jobs = [PersonJobs.Worker] },
        new() { Id = "bod-crafter", Jobs = [PersonJobs.Worker] },
        new() { Id = "bran", Jobs = [PersonJobs.Fighter] },
        new() { Id = "dread-red", Jobs = [PersonJobs.Fighter] },
        new() { Id = "nyle", Jobs = [PersonJobs.Thief] },
        new() { Id = "osric", Jobs = [PersonJobs.Tamer] }
    ];

    [Fact]
    public void Compose_Fixture_KeepsItsAuthoredDefinition()
    {
        var slot = Roster[0];
        Assert.Same(slot, PersonMaker.Compose("Felucca:connor", slot, Roster, Personas, EraBand.Modern));
    }

    [Fact]
    public void Compose_Copy_IsStableAndOwnsItsVoiceAndTrade()
    {
        var first = PersonMaker.Compose("Felucca:connor#3", Roster[0], Roster, Personas, EraBand.Modern);
        var again = PersonMaker.Compose("Felucca:connor#3", Roster[0], Roster, Personas, EraBand.Modern);

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(first.Persona, again.Persona);
        Assert.Equal(first.Build.Veteran, again.Build.Veteran);
        Assert.NotEqual("sela", first.Id);
        Assert.Contains(Personas, p => p.Id == first.Persona && PersonJobs.Fits(p, PersonJobs.Of(first.Build)));
    }

    [Fact]
    public void Compose_ManyCopies_SpreadOverEveryJob()
    {
        var jobs = new HashSet<string>();
        var personas = new HashSet<string>();

        for (var i = 1; i <= 60; i++)
        {
            var person = PersonMaker.Compose("Felucca:connor#" + i, Roster[0], Roster, Personas, EraBand.Modern);
            jobs.Add(PersonJobs.Of(person.Build));
            personas.Add(person.Persona);
        }

        Assert.Equal(4, jobs.Count);
        Assert.True(personas.Count >= 4);
    }

    [Fact]
    public void Compose_Copy_VeteranFlagFollowsItsSkillTier()
    {
        var veterans = 0;

        for (var i = 1; i <= 80; i++)
        {
            var uniqueId = "Felucca:connor#" + i;
            var person = PersonMaker.Compose(uniqueId, Roster[0], Roster, Personas, EraBand.Modern);
            var profile = PersonProfileRules.Roll(uniqueId, person);

            Assert.Equal(profile.IsVeteran, person.Build.Veteran);
            veterans += person.Build.Veteran ? 1 : 0;
        }

        Assert.InRange(veterans, 1, 79);
    }

    [Fact]
    public void IsFollower_OnlyAFollowStepIsAFollower()
    {
        Assert.True(PersonMaker.IsFollower(Roster[5]));
        Assert.False(PersonMaker.IsFollower(Roster[2]));
    }

    [Fact]
    public void ExtraPersonas_AllCarryAJobAndAVoice()
    {
        var extras = new List<Persona>(PersonasFile.ExtraPersonas());
        extras.AddRange(PersonasFile.MorePersonas());
        Assert.True(extras.Count >= 30);

        foreach (var persona in extras)
        {
            Assert.NotEmpty(persona.Jobs);
            Assert.NotEmpty(persona.IdleLines);
            Assert.NotEmpty(persona.Greetings);
            Assert.NotNull(persona.Drives);
        }
    }
}
