using System;
using System.Collections.Generic;
using System.IO;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class PersonaComposerTests
{
    private const int CopySampleSize = 1000;
    private const int MinUniqueFullText = 995;
    private const int MinUniqueBackground = 900;
    private const double MaxLineShare = 0.05;
    private const string CopyId = "Felucca:connor#3";
    private const string FixtureId = "Felucca:connor";
    private const string EditedMarker = "operator-kept-the-bank-line";
    private const string MissingPoolJob = PersonJobs.Worker;
    private const int MinGreetingPerJob = 40;

    // The modern band holds every tagged and untagged part, so these tests see all content.
    private const EraBand AllContent = EraBand.Modern;

    private static readonly PersonaPartCatalog DefaultParts = DefaultPersonaParts.Catalog();

    private static readonly string[] BackgroundPools =
        [PersonaPartsFile.PoolOrigin, PersonaPartsFile.PoolHabit, PersonaPartsFile.PoolWant];

    private static readonly string[] VoicePools = [PersonaPartsFile.PoolVoice];

    [Fact]
    public void Compose_SameCopyId_IsStable()
    {
        var basePersona = PersonasFile.CreateDefaultConnor();
        var first = PersonaComposer.Compose(CopyId, basePersona, PersonJobs.Worker, DefaultParts, AllContent);
        var again = PersonaComposer.Compose(CopyId, basePersona, PersonJobs.Worker, DefaultParts, AllContent);

        Assert.Equal(first.Background, again.Background);
        Assert.Equal(first.Voice, again.Voice);
        Assert.Equal(first.Likes, again.Likes);
        Assert.Equal(first.Dislikes, again.Dislikes);
        Assert.Equal(first.IdleLines, again.IdleLines);
        Assert.Equal(first.Greetings, again.Greetings);
        Assert.Equal(first.Drives[PersonaDrives.GreedKey], again.Drives[PersonaDrives.GreedKey]);
        Assert.Equal(first.ActiveStartHour, again.ActiveStartHour);
        Assert.Equal(first.ActiveEndHour, again.ActiveEndHour);
    }

    [Fact]
    public void Compose_FixtureId_KeepsTheAuthoredPersona()
    {
        var basePersona = PersonasFile.CreateDefaultConnor();
        var composed = PersonaComposer.Compose(FixtureId, basePersona, PersonJobs.Worker, DefaultParts, AllContent);

        Assert.Same(basePersona, composed);
    }

    [Fact]
    public void Compose_Copy_KeepsBaseIdJobsAndDisposition()
    {
        var basePersona = PersonasFile.CreateDefaultBran();
        var composed = PersonaComposer.Compose("Felucca:bran#4", basePersona, PersonJobs.Fighter, DefaultParts, AllContent);

        Assert.Equal(basePersona.Id, composed.Id);
        Assert.Equal(basePersona.Jobs, composed.Jobs);
        Assert.Equal(basePersona.Disposition, composed.Disposition);
        Assert.Equal(basePersona.DisplayName, composed.DisplayName);
        Assert.NotSame(basePersona, composed);
    }

    [Fact]
    public void Compose_ThousandCopies_HaveDistinctPersonaText()
    {
        var people = ComposeCopies(CopySampleSize);
        var fullTexts = new HashSet<string>(StringComparer.Ordinal);
        var backgrounds = new HashSet<string>(StringComparer.Ordinal);
        var duplicateFull = 0;
        var duplicateBackground = 0;

        for (var i = 0; i < people.Count; i++)
        {
            var persona = people[i];
            var full = FullPersonaText(persona);

            if (!fullTexts.Add(full))
            {
                duplicateFull++;
            }

            if (!backgrounds.Add(persona.Background))
            {
                duplicateBackground++;
            }
        }

        Assert.True(
            CopySampleSize - duplicateFull >= MinUniqueFullText,
            $"unique full texts {CopySampleSize - duplicateFull}, need {MinUniqueFullText}"
        );
        Assert.True(
            CopySampleSize - duplicateBackground >= MinUniqueBackground,
            $"unique backgrounds {CopySampleSize - duplicateBackground}, need {MinUniqueBackground}"
        );
    }

    [Fact]
    public void Compose_ThousandCopies_ShareIdleAndGreetingPartsThinly()
    {
        var people = ComposeCopies(CopySampleSize);
        AssertLineShare(people, persona => persona.IdleLines, PersonaPartsFile.PoolIdle);
        AssertLineShare(people, persona => persona.Greetings, PersonaPartsFile.PoolGreeting);
    }

    [Fact]
    public void Compose_NeverLikesAndDislikesTheSameThing()
    {
        var people = ComposeCopies(CopySampleSize);

        for (var i = 0; i < people.Count; i++)
        {
            var likes = new HashSet<string>(people[i].Likes ?? [], StringComparer.OrdinalIgnoreCase);

            foreach (var dislike in people[i].Dislikes ?? [])
            {
                Assert.DoesNotContain(dislike, likes);
            }
        }

        AssertNoExcludedPairMeets(people);
    }

    [Fact]
    public void Compose_DrivesStayInsideBaseOffsetAndClamp()
    {
        var people = ComposeCopyPairs(CopySampleSize);

        for (var i = 0; i < people.Count; i++)
        {
            var (basePersona, composed) = people[i];
            var baseDrives = PersonaDrives.From(basePersona.Drives);
            var composedDrives = PersonaDrives.From(composed.Drives);

            AssertDriveAxis(baseDrives.Greed, composedDrives.Greed);
            AssertDriveAxis(baseDrives.Caution, composedDrives.Caution);
            AssertDriveAxis(baseDrives.Valor, composedDrives.Valor);
        }
    }

    [Fact]
    public void Compose_TraitsLeanTheDrives()
    {
        var basePersona = PersonasFile.CreateDefaultConnor();
        var plain = PersonaDrives.From(PersonaComposer.Compose(CopyId, basePersona, PersonJobs.Worker, DefaultParts, AllContent).Drives);
        var brave = PersonaDrives.From(
            PersonaComposer.Compose(CopyId, basePersona, PersonJobs.Worker, DefaultParts, AllContent, PersonTrait.Brave | PersonTrait.Greedy).Drives
        );
        var cautious = PersonaDrives.From(
            PersonaComposer.Compose(CopyId, basePersona, PersonJobs.Worker, DefaultParts, AllContent, PersonTrait.Cautious | PersonTrait.Generous).Drives
        );

        Assert.True(brave.Valor >= plain.Valor && brave.Valor > cautious.Valor);
        Assert.True(brave.Greed > cautious.Greed);
        Assert.True(cautious.Caution > brave.Caution);
    }

    [Fact]
    public void Compose_ActiveHoursShiftInsideOffsetAndWrap()
    {
        var people = ComposeCopyPairs(CopySampleSize);
        var sawShift = false;

        for (var i = 0; i < people.Count; i++)
        {
            var (basePersona, composed) = people[i];

            if (basePersona.ActiveStartHour is null && basePersona.ActiveEndHour is null)
            {
                Assert.Null(composed.ActiveStartHour);
                Assert.Null(composed.ActiveEndHour);
                continue;
            }

            AssertHourShift(basePersona.ActiveStartHour, composed.ActiveStartHour);
            AssertHourShift(basePersona.ActiveEndHour, composed.ActiveEndHour);

            if (composed.ActiveStartHour != basePersona.ActiveStartHour ||
                composed.ActiveEndHour != basePersona.ActiveEndHour)
            {
                sawShift = true;
            }
        }

        var midnight = PersonaComposer.Compose(
            "Felucca:endless-newcomer#1",
            EndlessNewcomer(),
            PersonJobs.Worker,
            DefaultParts,
            AllContent
        );
        Assert.NotNull(midnight.ActiveEndHour);
        var midnightOffset = OffsetHours(24, midnight.ActiveEndHour.Value);
        Assert.InRange(midnightOffset, -PersonaComposer.HourOffsetRange, PersonaComposer.HourOffsetRange);
        Assert.Equal(DayShapeRules.NormalizeHour(24 + midnightOffset), midnight.ActiveEndHour.Value);
        Assert.True(sawShift);

        var noHours = new Persona
        {
            Id = "quiet-hours",
            Jobs = [PersonJobs.Worker],
            Background = "A labourer.",
            Voice = "Plain."
        };
        var stillNone = PersonaComposer.Compose("Felucca:quiet-hours#1", noHours, PersonJobs.Worker, DefaultParts, AllContent);
        Assert.Null(stillNone.ActiveStartHour);
        Assert.Null(stillNone.ActiveEndHour);
    }

    [Fact]
    public void DefaultWantParts_SeedANonDefaultAmbitionForTheJob()
    {
        var misfits = new List<string>();

        foreach (var job in PersonJobs.All)
        {
            var wants = DefaultParts.For(PersonaPartsFile.PoolWant, job, AllContent);
            Assert.NotEmpty(wants);

            for (var i = 0; i < wants.Count; i++)
            {
                var ambition = AmbitionRules.SeedFromBackground(wants[i].Text);

                if (ambition.Kind == AmbitionKind.None ||
                    string.Equals(ambition.Target, AmbitionRules.NestEggTarget, StringComparison.Ordinal) ||
                    !WantFitsJob(job, ambition))
                {
                    misfits.Add($"{wants[i].Id} ({job}, {ambition.Kind} {ambition.Target}): {wants[i].Text}");
                }
            }
        }

        Assert.True(misfits.Count == 0, $"{misfits.Count} want parts:{Environment.NewLine}{string.Join(Environment.NewLine, misfits)}");
    }

    [Fact]
    public void DefaultGreetingParts_AreLongEnough()
    {
        foreach (var job in PersonJobs.All)
        {
            var greetings = DefaultParts.For(PersonaPartsFile.PoolGreeting, job, AllContent);
            Assert.True(greetings.Count >= MinGreetingPerJob);

            for (var i = 0; i < greetings.Count; i++)
            {
                Assert.False(GreetingLines.IsTooShort(greetings[i].Text), greetings[i].Id);
            }
        }
    }

    [Fact]
    public void LoadOrCreate_WritesDefaultsOnlyWhenMissingAndKeepsEdits()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sosariaai-persona-parts-{Guid.NewGuid():N}");

        try
        {
            var first = PersonaPartsFile.LoadOrCreate(directory);
            var originPath = Path.Combine(directory, $"{PersonaPartsFile.PoolOrigin}.json");
            Assert.True(File.Exists(originPath));
            Assert.NotEmpty(first.For(PersonaPartsFile.PoolOrigin, PersonJobs.Worker, AllContent));

            File.WriteAllText(originPath, EditedOriginJson());
            var again = PersonaPartsFile.LoadOrCreate(directory);
            var kept = again.For(PersonaPartsFile.PoolOrigin, PersonJobs.Worker, AllContent);
            Assert.Contains(kept, part => part.Text.Contains(EditedMarker, StringComparison.Ordinal));
            Assert.True(File.Exists(Path.Combine(directory, $"{PersonaPartsFile.PoolHabit}.json")));
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
    public void Compose_MissingPoolForJob_FallsBackToBaseText()
    {
        var basePersona = PersonasFile.CreateDefaultConnor();
        var empty = new PersonaPartCatalog(new Dictionary<string, List<PersonaPart>>(StringComparer.OrdinalIgnoreCase)
        {
            [PersonaPartsFile.PoolOrigin] =
            [
                new PersonaPart
                {
                    Id = "origin-fighter-only",
                    Text = "Born in Jhelom for the pits.",
                    Jobs = [PersonJobs.Fighter]
                }
            ]
        });

        var composed = PersonaComposer.Compose(
            "Felucca:connor#9",
            basePersona,
            MissingPoolJob,
            empty,
            AllContent
        );

        Assert.Equal(basePersona.Background, composed.Background);
        Assert.Equal(basePersona.Voice, composed.Voice);
        Assert.Equal(basePersona.Likes, composed.Likes);
        Assert.Equal(basePersona.Dislikes, composed.Dislikes);
        Assert.Equal(basePersona.IdleLines, composed.IdleLines);
        Assert.Equal(basePersona.Greetings, composed.Greetings);
        Assert.Equal(basePersona.ReturnLines, composed.ReturnLines);
        Assert.Equal(basePersona.CombatLines, composed.CombatLines);
        Assert.Equal(basePersona.LootLines, composed.LootLines);
        Assert.NotSame(basePersona, composed);
    }

    private static List<Persona> ComposeCopies(int count)
    {
        var pairs = ComposeCopyPairs(count);
        var people = new List<Persona>(pairs.Count);

        for (var i = 0; i < pairs.Count; i++)
        {
            people.Add(pairs[i].Composed);
        }

        return people;
    }

    private static List<(Persona Base, Persona Composed)> ComposeCopyPairs(int count)
    {
        var personas = AllPersonas();
        var catalog = new PersonaCatalog(Index(personas));
        var roster = SampleRoster();
        var slot = roster[0];
        var pairs = new List<(Persona, Persona)>(count);

        for (var i = 1; i <= count; i++)
        {
            var uniqueId = "Felucca:connor#" + i;
            var definition = PersonMaker.Compose(uniqueId, slot, roster, personas, AllContent);
            var basePersona = catalog.Resolve(definition.Persona);
            var composed = PersonaComposer.Compose(
                uniqueId,
                basePersona,
                PersonJobs.Of(definition.Build),
                DefaultParts,
                AllContent
            );
            pairs.Add((basePersona, composed));
        }

        return pairs;
    }

    private static Dictionary<string, Persona> Index(IReadOnlyList<Persona> personas)
    {
        var byId = new Dictionary<string, Persona>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < personas.Count; i++)
        {
            byId[personas[i].Id] = personas[i];
        }

        return byId;
    }

    private static List<Persona> AllPersonas()
    {
        var list = new List<Persona>
        {
            PersonasFile.CreateDefaultConnor(),
            PersonasFile.CreateDefaultMira(),
            PersonasFile.CreateDefaultTobin(),
            PersonasFile.CreateDefaultHal(),
            PersonasFile.CreateDefaultWren(),
            PersonasFile.CreateDefaultBran(),
            PersonasFile.CreateDefaultSela(),
            PersonasFile.CreateDefaultTam(),
            PersonasFile.CreateDefaultDunn(),
            PersonasFile.CreateDefaultOrla(),
            PersonasFile.CreateDefaultKerr(),
            PersonasFile.CreateDefaultNyle(),
            PersonasFile.CreateDefaultOsric()
        };
        list.AddRange(PersonasFile.ExtraPersonas());
        list.AddRange(PersonasFile.MorePersonas());
        return list;
    }

    private static List<CharacterDefinition> SampleRoster() =>
    [
        Template("connor", null, "worker", PersonJobs.Worker),
        Template("mira", null, "worker", PersonJobs.Worker),
        Template("bran", "swordsman", null, PersonJobs.Fighter),
        Template("nyle", "thief", null, PersonJobs.Thief),
        Template("osric", "tamer", null, PersonJobs.Tamer)
    ];

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

    private static string FullPersonaText(Persona persona) =>
        string.Join(
            "\n",
            persona.Background,
            persona.Voice,
            string.Join("|", persona.Likes ?? []),
            string.Join("|", persona.Dislikes ?? [])
        );

    private static void AssertLineShare(
        IReadOnlyList<Persona> people,
        Func<Persona, List<string>> linesOf,
        string pool
    )
    {
        var partTexts = new HashSet<string>(StringComparer.Ordinal);

        foreach (var job in PersonJobs.All)
        {
            var parts = DefaultParts.For(pool, job, AllContent);

            for (var i = 0; i < parts.Count; i++)
            {
                partTexts.Add(parts[i].Text);
            }
        }

        var uses = new Dictionary<string, int>(StringComparer.Ordinal);
        var maxShare = (int)(people.Count * MaxLineShare);

        for (var i = 0; i < people.Count; i++)
        {
            var lines = linesOf(people[i]);

            if (lines == null)
            {
                continue;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var n = 0; n < lines.Count; n++)
            {
                var line = lines[n];

                if (!partTexts.Contains(line) || !seen.Add(line))
                {
                    continue;
                }

                uses[line] = uses.GetValueOrDefault(line) + 1;
            }
        }

        foreach (var pair in uses)
        {
            Assert.True(
                pair.Value <= maxShare,
                $"{pool} line used by {pair.Value} of {people.Count}: {pair.Key}"
            );
        }
    }

    private static void AssertNoExcludedPairMeets(IReadOnlyList<Persona> people)
    {
        var byId = new Dictionary<string, PersonaPart>(StringComparer.OrdinalIgnoreCase);
        var byText = new Dictionary<string, PersonaPart>(StringComparer.Ordinal);

        foreach (var pool in PersonaPartsFile.Pools)
        {
            foreach (var part in DefaultPartsIn(pool))
            {
                byId.TryAdd(part.Id, part);
                byText.TryAdd(part.Text, part);
            }
        }

        for (var i = 0; i < people.Count; i++)
        {
            var used = UsedPartIds(people[i], byText);

            foreach (var id in used)
            {
                if (!byId.TryGetValue(id, out var part) || part.Exclude == null)
                {
                    continue;
                }

                for (var n = 0; n < part.Exclude.Count; n++)
                {
                    Assert.False(used.Contains(part.Exclude[n]), $"{id} met {part.Exclude[n]}");
                }
            }
        }
    }

    /// <summary>
    /// The part ids a composed person carries. A list entry is a whole part text; the background
    /// and voice join several parts, so they are searched only for the parts of their own pools.
    /// </summary>
    private static HashSet<string> UsedPartIds(Persona persona, Dictionary<string, PersonaPart> byText)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        RememberWithin(used, persona.Background, BackgroundPools);
        RememberWithin(used, persona.Voice, VoicePools);

        AddList(used, byText, persona.Likes);
        AddList(used, byText, persona.Dislikes);
        AddList(used, byText, persona.IdleLines);
        AddList(used, byText, persona.Greetings);
        AddList(used, byText, persona.ReturnLines);
        AddList(used, byText, persona.CombatLines);
        AddList(used, byText, persona.LootLines);
        return used;
    }

    private static void AddList(
        HashSet<string> used,
        Dictionary<string, PersonaPart> byText,
        List<string> values
    )
    {
        if (values == null)
        {
            return;
        }

        for (var i = 0; i < values.Count; i++)
        {
            if (byText.TryGetValue(values[i], out var part))
            {
                used.Add(part.Id);
            }
        }
    }

    private static void RememberWithin(HashSet<string> used, string text, string[] pools)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (var pool in pools)
        {
            foreach (var part in DefaultPartsIn(pool))
            {
                if (text.Contains(part.Text, StringComparison.Ordinal))
                {
                    used.Add(part.Id);
                }
            }
        }
    }

    private static IEnumerable<PersonaPart> DefaultPartsIn(string pool)
    {
        foreach (var job in PersonJobs.All)
        {
            foreach (var part in DefaultParts.For(pool, job, AllContent))
            {
                yield return part;
            }
        }
    }

    private static void AssertDriveAxis(double baseline, double composed)
    {
        Assert.InRange(composed, PersonaDrives.MinValue, PersonaDrives.MaxValue);
        var low = Math.Max(PersonaDrives.MinValue, baseline - PersonaComposer.DriveOffsetRange);
        var high = Math.Min(PersonaDrives.MaxValue, baseline + PersonaComposer.DriveOffsetRange);
        Assert.InRange(composed, low, high);
    }

    private static void AssertHourShift(int? baseline, int? composed)
    {
        if (baseline is null)
        {
            Assert.Null(composed);
            return;
        }

        Assert.NotNull(composed);
        var offset = OffsetHours(baseline.Value, composed.Value);
        Assert.InRange(offset, -PersonaComposer.HourOffsetRange, PersonaComposer.HourOffsetRange);
        Assert.Equal(DayShapeRules.NormalizeHour(baseline.Value + offset), composed.Value);
    }

    private static int OffsetHours(int baseline, int composed)
    {
        for (var offset = -PersonaComposer.HourOffsetRange; offset <= PersonaComposer.HourOffsetRange; offset++)
        {
            if (DayShapeRules.NormalizeHour(baseline + offset) == composed)
            {
                return offset;
            }
        }

        return int.MaxValue;
    }

    // A worker never seeds a hunt; every other trade may want to save, train, travel or hunt.
    private static bool WantFitsJob(string job, Ambition ambition) =>
        job == PersonJobs.Worker
            ? ambition.Kind is AmbitionKind.Gold or AmbitionKind.Skill or AmbitionKind.Place
            : ambition.Kind is AmbitionKind.Gold or AmbitionKind.Skill or AmbitionKind.Place or AmbitionKind.Kills;

    private static Persona EndlessNewcomer()
    {
        foreach (var persona in PersonasFile.ExtraPersonas())
        {
            if (string.Equals(persona.Id, "endless-newcomer", StringComparison.OrdinalIgnoreCase))
            {
                return persona;
            }
        }

        throw new InvalidOperationException("endless-newcomer is missing.");
    }

    private static string EditedOriginJson() =>
        """
        {
          "pool": "origin",
          "parts": [
            {
              "id": "origin-operator-edit",
              "text": "Born on the operator-kept-the-bank-line.",
              "jobs": [ "worker", "fighter", "thief", "tamer" ]
            }
          ]
        }
        """;
}
