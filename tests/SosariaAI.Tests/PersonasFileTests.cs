using System;
using System.IO;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class PersonasFileTests
{
    [Fact]
    public void CreateDefaultConnor_MatchesShippedExample()
    {
        var persona = PersonasFile.CreateDefaultConnor();

        Assert.Equal(PersonasFile.ConnorId, persona.Id);
        Assert.Null(persona.DisplayName);
        Assert.Contains("Born in Britain", persona.Background);
        Assert.Contains("Short sentences", persona.Voice);
        Assert.Contains("a good axe", persona.Likes);
        Assert.Contains("thieves", persona.Dislikes);
        Assert.Contains("Anyone seen a good deal on ingots today?", persona.IdleLines);
        Assert.Contains("Morning, {name}.", persona.Greetings);
        Assert.Contains("That was a bad day.", persona.ReturnLines);
        Assert.Equal("neutral", persona.Disposition);
    }

    [Fact]
    public void CreateDefaultWorkers_HaveCombatAndLootLines()
    {
        AssertWorkerLines(PersonasFile.CreateDefaultConnor());
        AssertWorkerLines(PersonasFile.CreateDefaultMira());
        AssertWorkerLines(PersonasFile.CreateDefaultTobin());
        AssertWorkerLines(PersonasFile.CreateDefaultHal());
        AssertWorkerLines(PersonasFile.CreateDefaultWren());
    }

    [Fact]
    public void CreateDefaultMira_HasGreetingsAndReturnLines()
    {
        var persona = PersonasFile.CreateDefaultMira();
        Assert.Equal(PersonasFile.MiraId, persona.Id);
        Assert.NotEmpty(persona.Greetings);
        Assert.NotEmpty(persona.ReturnLines);
        Assert.Contains("{name}", persona.Greetings[0]);
        Assert.NotEmpty(persona.CombatLines);
        Assert.NotEmpty(persona.LootLines);
    }

    [Fact]
    public void LoadOrCreate_WritesConnorWhenDirectoryIsEmpty()
    {
        WithFreshCatalog((directory, catalog) =>
        {
            var persona = catalog.Resolve(PersonasFile.ConnorId);

            Assert.Equal(PersonasFile.ConnorId, persona.Id);
            Assert.Equal(PersonasFile.BranId, catalog.Resolve(PersonasFile.BranId).Id);
            Assert.True(File.Exists(Path.Combine(directory, $"{PersonasFile.ConnorId}.json")));
            Assert.True(File.Exists(Path.Combine(directory, $"{PersonasFile.MiraId}.json")));
            Assert.True(File.Exists(Path.Combine(directory, $"{PersonasFile.TobinId}.json")));
            Assert.True(File.Exists(Path.Combine(directory, $"{PersonasFile.HalId}.json")));
            Assert.True(File.Exists(Path.Combine(directory, $"{PersonasFile.WrenId}.json")));
            Assert.True(File.Exists(Path.Combine(directory, $"{PersonasFile.BranId}.json")));
            Assert.True(File.Exists(Path.Combine(directory, $"{PersonasFile.SelaId}.json")));
            Assert.True(File.Exists(Path.Combine(directory, $"{PersonasFile.TamId}.json")));
            Assert.True(File.Exists(Path.Combine(directory, $"{PersonasFile.DunnId}.json")));
            Assert.True(File.Exists(Path.Combine(directory, $"{PersonasFile.OrlaId}.json")));
            Assert.True(File.Exists(Path.Combine(directory, $"{PersonasFile.KerrId}.json")));
            Assert.True(File.Exists(Path.Combine(directory, $"{PersonasFile.NyleId}.json")));
            Assert.True(File.Exists(Path.Combine(directory, $"{PersonasFile.OsricId}.json")));
        });
    }

    // A preset line is said at random to whoever stands near, and nothing behind it ever
    // goes along or leaves, so no preset may hold an invite or a trip claim.
    [Fact]
    public void PresetPersonas_PromiseNoGroupTripOrMeeting()
    {
        WithFreshCatalog((_, catalog) =>
        {
            Assert.NotEmpty(catalog.All);

            foreach (var persona in catalog.All)
            {
                Assert.All(persona.IdleLines, line => AssertPromisesNothing(persona, line));
                Assert.All(persona.Greetings, line => AssertPromisesNothing(persona, line));
                Assert.All(persona.ReturnLines, line => AssertPromisesNothing(persona, line));
                Assert.All(persona.CombatLines, line => AssertPromisesNothing(persona, line));
                Assert.All(persona.LootLines, line => AssertPromisesNothing(persona, line));
            }
        });

        Assert.DoesNotContain("On my way, {name}.", PersonasFile.CreateDefaultHal().Greetings);
        Assert.DoesNotContain("With me, {name}.", PersonasFile.CreateDefaultBran().Greetings);
    }

    private static void AssertPromisesNothing(Persona persona, string line) =>
        Assert.False(PromiseLines.IsPromise(line), $"{persona.Id}: {line}");

    // Writes every preset to an empty directory, loads the catalog back, then removes it.
    private static void WithFreshCatalog(Action<string, PersonaCatalog> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sosariaai-personas-{Guid.NewGuid():N}");

        try
        {
            check(directory, PersonasFile.LoadOrCreate(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Resolve_MissingId_ReturnsNeutral()
    {
        var catalog = new PersonaCatalog(new());
        var persona = catalog.Resolve("missing");
        Assert.Equal(Persona.NeutralId, persona.Id);
        Assert.NotEmpty(persona.IdleLines);
        Assert.Empty(persona.CombatLines);
        Assert.Empty(persona.LootLines);
        Assert.Null(persona.PickCombatLine());
        Assert.Null(persona.PickLootLine());
    }

    [Fact]
    public void CreateDefaultBran_HasCrewVoiceAndCombatLines()
    {
        var persona = PersonasFile.CreateDefaultBran();
        Assert.Equal("lawful", persona.Disposition);

        Assert.Equal(PersonasFile.BranId, persona.Id);
        Assert.Contains("graveyard", persona.Background);
        Assert.Contains("Short sentences", persona.Voice);
        Assert.Contains("a fair fight", persona.Likes);
        Assert.NotEmpty(persona.IdleLines);
        Assert.NotEmpty(persona.Greetings);
        Assert.NotEmpty(persona.ReturnLines);
        Assert.NotEmpty(persona.CombatLines);
        Assert.NotEmpty(persona.LootLines);
        Assert.Contains("{name}", persona.Greetings[0]);
    }

    [Fact]
    public void CreateDefaultCombatPersonas_HaveIdsAndLines()
    {
        AssertCombatPersona(PersonasFile.CreateDefaultSela(), PersonasFile.SelaId);
        AssertCombatPersona(PersonasFile.CreateDefaultTam(), PersonasFile.TamId);
        AssertCombatPersona(PersonasFile.CreateDefaultDunn(), PersonasFile.DunnId);
        AssertCombatPersona(PersonasFile.CreateDefaultOrla(), PersonasFile.OrlaId);
        AssertCombatPersona(PersonasFile.CreateDefaultKerr(), PersonasFile.KerrId);
        Assert.Contains("trolls", PersonasFile.CreateDefaultKerr().Background);
        Assert.Contains("Thief", PersonasFile.CreateDefaultNyle().Background);
    }

    private static void AssertCombatPersona(Persona persona, string id)
    {
        Assert.Equal(id, persona.Id);
        Assert.False(string.IsNullOrWhiteSpace(persona.Background));
        Assert.Contains("Short sentences", persona.Voice);
        Assert.NotEmpty(persona.Likes);
        Assert.NotEmpty(persona.Dislikes);
        Assert.NotEmpty(persona.IdleLines);
        Assert.NotEmpty(persona.Greetings);
        Assert.NotEmpty(persona.ReturnLines);
        Assert.NotEmpty(persona.CombatLines);
        Assert.NotEmpty(persona.LootLines);
        Assert.Contains("{name}", persona.Greetings[0]);
    }

    private static void AssertWorkerLines(Persona persona)
    {
        Assert.NotEmpty(persona.CombatLines);
        Assert.NotEmpty(persona.LootLines);
    }
}
