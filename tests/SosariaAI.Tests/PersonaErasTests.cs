using System;
using System.Collections.Generic;
using System.IO;
using Server.Json;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class PersonaErasTests
{
    private const string ErasKey = "\"eras\"";
    private const string TaggedPartId = "habit-f-paladin-tithe";
    private const string TaggedPersonaId = "paladin-of-trinsic";
    private const string UntaggedPersonaId = "bran";
    private const string LaterPersonaId = "later";
    private const string FixtureId = "bran";
    private const string CopyId = "Felucca:bran#2";
    private const int CopiesPerJob = 200;

    // Even a T2A shard needs enough parts to give each copy its own mix.
    private const int MinPartsPerJobInAnyBand = 10;

    private static readonly EraBand[] AllBands = [EraBand.T2A, EraBand.ML, EraBand.Modern];

    [Fact]
    public void Part_WithoutEras_OmitsTheKeyAndTaggedPartRoundTrips()
    {
        var plain = new PersonaPart { Id = "p", Text = "text", Jobs = [PersonJobs.Worker] };
        var emptied = new PersonaPart { Id = "e", Text = "text", Jobs = [PersonJobs.Worker], Eras = [] };
        var tagged = new PersonaPart { Id = "t", Text = "text", Jobs = [PersonJobs.Worker], Eras = PersonaEras.AosOnward() };

        Assert.DoesNotContain(ErasKey, JsonConfig.Serialize(plain), StringComparison.Ordinal);
        Assert.DoesNotContain(ErasKey, JsonConfig.Serialize(emptied), StringComparison.Ordinal);

        var json = JsonConfig.Serialize(tagged);
        Assert.Contains(ErasKey, json, StringComparison.Ordinal);
        Assert.Equal(PersonaEras.AosOnward(), FromJson<PersonaPart>(json).Eras);
    }

    [Fact]
    public void Persona_WithoutEras_OmitsTheKeyAndTaggedPersonaRoundTrips()
    {
        Assert.DoesNotContain(ErasKey, JsonConfig.Serialize(PersonasFile.CreateDefaultConnor()), StringComparison.Ordinal);

        var tagged = new Persona { Id = LaterPersonaId, Eras = PersonaEras.SaOnward() };
        var json = JsonConfig.Serialize(tagged);
        Assert.Contains(ErasKey, json, StringComparison.Ordinal);
        Assert.Equal(PersonaEras.SaOnward(), FromJson<Persona>(json).Eras);
    }

    [Fact]
    public void CatalogFor_KeepsUntaggedEverywhereAndTaggedOnlyInItsBands()
    {
        var catalog = new PersonaPartCatalog(new Dictionary<string, List<PersonaPart>>(StringComparer.OrdinalIgnoreCase)
        {
            [PersonaPartsFile.PoolIdle] =
            [
                new PersonaPart { Id = "any", Text = "any era", Jobs = [PersonJobs.Worker] },
                new PersonaPart { Id = "aos", Text = "aos on", Jobs = [PersonJobs.Worker], Eras = PersonaEras.AosOnward() },
                new PersonaPart { Id = "sa", Text = "sa on", Jobs = [PersonJobs.Worker], Eras = PersonaEras.SaOnward() }
            ]
        });

        Assert.Equal(["any"], Ids(catalog.For(PersonaPartsFile.PoolIdle, PersonJobs.Worker, EraBand.T2A)));
        Assert.Equal(["any", "aos"], Ids(catalog.For(PersonaPartsFile.PoolIdle, PersonJobs.Worker, EraBand.ML)));
        Assert.Equal(["any", "aos", "sa"], Ids(catalog.For(PersonaPartsFile.PoolIdle, PersonJobs.Worker, EraBand.Modern)));
    }

    [Fact]
    public void PersonasFor_DropsVoicesTaggedForOtherEras()
    {
        List<Persona> personas =
        [
            new() { Id = UntaggedPersonaId, Jobs = [PersonJobs.Fighter] },
            new() { Id = TaggedPersonaId, Jobs = [PersonJobs.Fighter], Eras = PersonaEras.AosOnward() }
        ];

        Assert.Equal([UntaggedPersonaId], PersonaIds(PersonMaker.PersonasFor(personas, PersonJobs.Fighter, EraBand.T2A)));
        Assert.Equal(
            [UntaggedPersonaId, TaggedPersonaId],
            PersonaIds(PersonMaker.PersonasFor(personas, PersonJobs.Fighter, EraBand.ML))
        );
    }

    [Fact]
    public void PersonMakerCompose_CopyInT2ANeverTakesALaterVoice()
    {
        var roster = new List<CharacterDefinition> { FighterTemplate(FixtureId, TaggedPersonaId) };
        List<Persona> personas =
        [
            new() { Id = UntaggedPersonaId, Jobs = [PersonJobs.Fighter] },
            new() { Id = TaggedPersonaId, Jobs = [PersonJobs.Fighter], Eras = PersonaEras.AosOnward() }
        ];

        for (var i = 1; i <= CopiesPerJob; i++)
        {
            var copy = PersonMaker.Compose(FixtureId + SpawnPlan.DuplicateMark + i, roster[0], roster, personas, EraBand.T2A);
            Assert.Equal(UntaggedPersonaId, copy.Persona);
        }

        Assert.Same(roster[0], PersonMaker.Compose(FixtureId, roster[0], roster, personas, EraBand.T2A));
    }

    [Fact]
    public void PersonaComposerCompose_InT2AUsesNoPartTaggedForLaterEras()
    {
        var parts = DefaultPersonaParts.Catalog();

        foreach (var job in PersonJobs.All)
        {
            var later = LaterTexts(parts, job);
            Assert.NotEmpty(later);

            for (var i = 1; i <= CopiesPerJob; i++)
            {
                var composed = PersonaComposer.Compose($"{CopyId}{i}", Persona.CreateNeutral(), job, parts, EraBand.T2A);

                foreach (var text in later)
                {
                    Assert.DoesNotContain(text, composed.Background, StringComparison.Ordinal);
                    Assert.DoesNotContain(text, composed.Voice, StringComparison.Ordinal);
                    Assert.DoesNotContain(text, composed.Likes);
                    Assert.DoesNotContain(text, composed.Dislikes);
                    Assert.DoesNotContain(text, composed.IdleLines);
                    Assert.DoesNotContain(text, composed.Greetings);
                    Assert.DoesNotContain(text, composed.ReturnLines);
                    Assert.DoesNotContain(text, composed.CombatLines);
                    Assert.DoesNotContain(text, composed.LootLines);
                }
            }
        }
    }

    [Fact]
    public void MisfitFixtures_ReportsOffEraFixturePersonaOnce()
    {
        var characters = new CharactersConfiguration
        {
            Facets = new Dictionary<string, FacetContent>(StringComparer.OrdinalIgnoreCase)
            {
                ["Felucca"] = new() { Roster = [FighterTemplate(FixtureId, TaggedPersonaId), FighterTemplate("kerr", UntaggedPersonaId)] },
                ["Trammel"] = new() { Roster = [FighterTemplate(FixtureId, TaggedPersonaId)] }
            }
        };
        var personas = new PersonaCatalog(new Dictionary<string, Persona>(StringComparer.OrdinalIgnoreCase)
        {
            [UntaggedPersonaId] = new() { Id = UntaggedPersonaId },
            [TaggedPersonaId] = new() { Id = TaggedPersonaId, Eras = PersonaEras.AosOnward() }
        });

        Assert.Equal([(FixtureId, TaggedPersonaId)], PersonaEras.MisfitFixtures(characters, personas, EraBand.T2A));
        Assert.Empty(PersonaEras.MisfitFixtures(characters, personas, EraBand.ML));
    }

    [Fact]
    public void ShippedDefaults_KeepEnoughPartsAndVoicesInEveryBand()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sosariaai-persona-eras-{Guid.NewGuid():N}");

        try
        {
            var parts = PersonaPartsFile.LoadOrCreate(Path.Combine(directory, "parts"));
            var personas = PersonasFile.LoadOrCreate(Path.Combine(directory, "personas"));

            Assert.Equal(PersonaEras.AosOnward(), PartById(parts, PersonaPartsFile.PoolHabit, PersonJobs.Fighter, TaggedPartId).Eras);
            Assert.Equal(PersonaEras.AosOnward(), personas.Resolve(TaggedPersonaId).Eras);
            Assert.Null(personas.Resolve(UntaggedPersonaId).Eras);

            foreach (var band in AllBands)
            {
                foreach (var job in PersonJobs.All)
                {
                    foreach (var pool in PersonaPartsFile.Pools)
                    {
                        Assert.True(parts.For(pool, job, band).Count >= MinPartsPerJobInAnyBand, $"{pool} {job} {band}");
                    }

                    Assert.NotEmpty(PersonMaker.PersonasFor(personas.All, job, band));
                }
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static HashSet<string> LaterTexts(PersonaPartCatalog parts, string job)
    {
        var texts = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pool in PersonaPartsFile.Pools)
        {
            foreach (var part in parts.For(pool, job, EraBand.Modern))
            {
                if (!PersonaEras.Fits(part, EraBand.T2A))
                {
                    texts.Add(part.Text);
                }
            }
        }

        return texts;
    }

    private static PersonaPart PartById(PersonaPartCatalog parts, string pool, string job, string id)
    {
        foreach (var part in parts.For(pool, job, EraBand.Modern))
        {
            if (part.Id == id)
            {
                return part;
            }
        }

        throw new InvalidOperationException($"{id} is not in the {pool} pool");
    }

    private static CharacterDefinition FighterTemplate(string id, string persona) =>
        new()
        {
            Id = id,
            Persona = persona,
            Build = new BuildDefinition { Preset = "swordsman" },
            Routines = new Dictionary<string, List<SkillStepDefinition>>
            {
                ["work"] = [new SkillStepDefinition { Skill = SkillKinds.Hunt }]
            }
        };

    private static T FromJson<T>(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<T>(json, JsonConfig.DefaultOptions);

    private static List<string> Ids(IReadOnlyList<PersonaPart> parts)
    {
        var ids = new List<string>(parts.Count);

        for (var i = 0; i < parts.Count; i++)
        {
            ids.Add(parts[i].Id);
        }

        return ids;
    }

    private static List<string> PersonaIds(List<Persona> personas)
    {
        var ids = new List<string>(personas.Count);

        for (var i = 0; i < personas.Count; i++)
        {
            ids.Add(personas[i].Id);
        }

        return ids;
    }
}
