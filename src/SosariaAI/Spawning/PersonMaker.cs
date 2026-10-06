using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Spawning;

/// <summary>
/// Makes one person out of the roster's parts, so copies of a template are not the
/// same person with different names. Each person rolls its own trade, its own voice
/// and its own experience, seeded by its id so a reboot brings back the same person. Experience follows the person's skill tier (<see cref="PersonProfileRules"/>);
/// the class build it wears is made at bind. A copy's voice comes only from personas
/// that fit the era band. Fixtures (no # in the id) keep their authored definition.
/// </summary>
public static class PersonMaker
{
    /// <summary>Share of people per job, in the order worker, fighter, thief, tamer.</summary>
    public const int WorkerWeight = 38;
    public const int FighterWeight = 42;
    public const int ThiefWeight = 8;
    public const int TamerWeight = 12;

    private const int JobSalt = 7;
    private const int TemplateSalt = 13;
    private const int PersonaSalt = 31;

    public static CharacterDefinition Compose(
        string uniqueId,
        CharacterDefinition slotTemplate,
        IReadOnlyList<CharacterDefinition> roster,
        IReadOnlyCollection<Persona> personas,
        EraBand band
    )
    {
        if (!WorkSites.IsCopy(uniqueId) || roster == null || roster.Count == 0)
        {
            return slotTemplate;
        }

        var job = RollJob(uniqueId);
        var candidates = TemplatesFor(roster, job);

        if (candidates.Count == 0)
        {
            candidates = TemplatesFor(roster, PersonJobs.Of(slotTemplate?.Build));
        }

        if (candidates.Count == 0)
        {
            return slotTemplate;
        }

        var template = candidates[Roll(uniqueId, TemplateSalt) % candidates.Count];
        var voices = PersonasFor(personas, PersonJobs.Of(template.Build), band);
        var persona = voices.Count == 0
            ? template.Persona
            : voices[Roll(uniqueId, PersonaSalt) % voices.Count].Id;

        var veteran = PersonProfileRules.Roll(uniqueId, template).IsVeteran;

        var person = new CharacterDefinition
        {
            Id = template.Id,
            Name = template.Name,
            Persona = persona,
            ReturnAfterDeath = template.ReturnAfterDeath,
            Spawn = template.Spawn,
            Build = WithExperience(template.Build, veteran),
            Routine = template.Routine,
            Routines = template.Routines,
            Choices = template.Choices
        };

        if (PersonJobs.Of(template.Build) == PersonJobs.Worker)
        {
            CraftCareerRules.Apply(person, CraftCareerRules.RollCareer(uniqueId), uniqueId);

            // A maker rolls its tier on the crafters' curve, so its experience follows the career.
            person.Build = WithExperience(template.Build, PersonProfileRules.Roll(uniqueId, person).IsVeteran);
        }

        if (PersonJobs.Of(template.Build) == PersonJobs.Tamer)
        {
            TamerLife.Apply(person);
        }

        return person;
    }

    public static string RollJob(string uniqueId)
    {
        var total = WorkerWeight + FighterWeight + ThiefWeight + TamerWeight;
        var roll = Roll(uniqueId, JobSalt) % total;

        if (roll < WorkerWeight)
        {
            return PersonJobs.Worker;
        }

        roll -= WorkerWeight;

        if (roll < FighterWeight)
        {
            return PersonJobs.Fighter;
        }

        roll -= FighterWeight;
        return roll < ThiefWeight ? PersonJobs.Thief : PersonJobs.Tamer;
    }

    /// <summary>
    /// Templates that hold a job of their own. A party follower has none: its whole
    /// life is "follow", and that belongs to the fixture in the crew.
    /// </summary>
    public static List<CharacterDefinition> TemplatesFor(IReadOnlyList<CharacterDefinition> roster, string job)
    {
        var found = new List<CharacterDefinition>();

        for (var i = 0; i < roster.Count; i++)
        {
            var template = roster[i];

            if (template == null || IsFollower(template) || PersonJobs.Of(template.Build) != job)
            {
                continue;
            }

            found.Add(template);
        }

        return found;
    }

    public static bool IsFollower(CharacterDefinition template)
    {
        var routines = template?.ResolvedRoutines();

        if (routines == null || routines.Count == 0)
        {
            return false;
        }

        foreach (var steps in routines.Values)
        {
            if (steps == null)
            {
                continue;
            }

            for (var i = 0; i < steps.Count; i++)
            {
                var skill = steps[i]?.Skill;

                if (skill is SkillKinds.Lumberjack or SkillKinds.Mine or SkillKinds.Fish or SkillKinds.Hunt
                    or SkillKinds.Dungeon or SkillKinds.Patrol or SkillKinds.Steal or SkillKinds.Tame)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static List<Persona> PersonasFor(IReadOnlyCollection<Persona> personas, string job, EraBand band)
    {
        var found = new List<Persona>();

        if (personas == null)
        {
            return found;
        }

        foreach (var persona in personas)
        {
            if (persona != null && PersonJobs.Fits(persona, job) && PersonaEras.Fits(persona, band))
            {
                found.Add(persona);
            }
        }

        found.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        return found;
    }

    private static BuildDefinition WithExperience(BuildDefinition build, bool veteran) =>
        new()
        {
            Preset = build?.Preset,
            Style = build?.Style,
            Role = build?.Role,
            Veteran = veteran,
            Skills = build?.Skills,
            Stats = build?.Stats,
            Kit = build?.Kit,
            CanHeal = build?.CanHeal,
            HealInterval = build?.HealInterval
        };

    private static int Roll(string uniqueId, int salt) =>
        WorkSites.StableRoll(uniqueId, salt);
}
