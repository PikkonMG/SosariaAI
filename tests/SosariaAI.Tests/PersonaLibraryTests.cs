using System;
using System.Collections.Generic;
using System.IO;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>The persona part library built into the DLL: its size, spread, text rules and era fit.</summary>
public class PersonaLibraryTests
{
    private const int PartsPerPool = 2000;
    private const int MinPartsPerJob = 500;
    private const int MinPartsPerBand = 1800;
    private const int MaxChatLength = 60;
    private const char FirstPrintable = ' ';
    private const char LastPrintable = '~';

    private static readonly EraBand[] AllBands = [EraBand.T2A, EraBand.ML, EraBand.Modern];

    private static readonly string[] ChatPools =
    [
        PersonaPartsFile.PoolIdle,
        PersonaPartsFile.PoolGreeting,
        PersonaPartsFile.PoolReturn,
        PersonaPartsFile.PoolCombat,
        PersonaPartsFile.PoolLoot
    ];

    private static readonly IReadOnlyList<PersonaPartPool> Library = DefaultPersonaParts.Pools();

    [Fact]
    public void EveryPool_IsBuiltIntoTheDll()
    {
        var names = new HashSet<string>(typeof(PersonaPartsFile).Assembly.GetManifestResourceNames(), StringComparer.Ordinal);

        for (var i = 0; i < PersonaPartsFile.Pools.Length; i++)
        {
            var pool = PersonaPartsFile.Pools[i];
            Assert.Contains(PersonaPartsFile.ResourceName(pool), names);
            Assert.Equal(pool, Library[i].Pool);
        }
    }

    [Fact]
    public void EveryPool_HasTwoThousandPartsAndEveryJobOnFiveHundred()
    {
        foreach (var pool in Library)
        {
            Assert.Equal(PartsPerPool, pool.Parts.Count);

            foreach (var job in PersonJobs.All)
            {
                var count = 0;

                foreach (var part in pool.Parts)
                {
                    if (PersonaPartCatalog.Fits(part, job))
                    {
                        count++;
                    }
                }

                Assert.True(count >= MinPartsPerJob, $"{pool.Pool} lists {job} on {count} parts, need {MinPartsPerJob}");
            }
        }
    }

    [Fact]
    public void PartIds_AreUniqueAcrossTheLibrary()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var repeats = new List<string>();

        foreach (var pool in Library)
        {
            foreach (var part in pool.Parts)
            {
                if (!ids.Add(part.Id))
                {
                    repeats.Add(part.Id);
                }
            }
        }

        Assert.True(repeats.Count == 0, string.Join(", ", repeats));
    }

    [Fact]
    public void PartTexts_AreUniqueInsideAPool_CaseAndPunctuationIgnored()
    {
        var repeats = new List<string>();

        foreach (var pool in Library)
        {
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var part in pool.Parts)
            {
                if (!seen.TryAdd(PersonaEraWords.Padded(part.Text), part.Id))
                {
                    repeats.Add($"{part.Id} repeats {seen[PersonaEraWords.Padded(part.Text)]}");
                }
            }
        }

        Assert.True(repeats.Count == 0, string.Join(", ", repeats));
    }

    [Fact]
    public void ChatParts_AreShortPrintableAsciiChatLines()
    {
        var bad = new List<string>();

        foreach (var pool in Library)
        {
            if (Array.IndexOf(ChatPools, pool.Pool) < 0)
            {
                continue;
            }

            foreach (var part in pool.Parts)
            {
                if (part.Text.Length >= MaxChatLength || !IsPrintableAscii(part.Text) ||
                    !PersonaDraftRules.IsChatLine(part.Text, allowName: pool.Pool == PersonaPartsFile.PoolGreeting))
                {
                    bad.Add($"{part.Id}: {part.Text}");
                }
            }
        }

        Assert.True(bad.Count == 0, string.Join(Environment.NewLine, bad));
    }

    [Fact]
    public void EveryBand_KeepsEighteenHundredPartsPerPool()
    {
        foreach (var pool in Library)
        {
            foreach (var band in AllBands)
            {
                var count = 0;

                foreach (var part in pool.Parts)
                {
                    if (PersonaEras.Fits(part, band))
                    {
                        count++;
                    }
                }

                Assert.True(count >= MinPartsPerBand, $"{pool.Pool} has {count} parts in {band}, need {MinPartsPerBand}");
            }
        }
    }

    [Fact]
    public void PartsUsableInABand_UseNoWordThatBandBans()
    {
        var bad = new List<string>();

        foreach (var pool in Library)
        {
            foreach (var part in pool.Parts)
            {
                foreach (var band in AllBands)
                {
                    var word = PersonaEras.Fits(part, band) ? PersonaEraWords.FindBanned(part.Text, band) : null;

                    if (word != null)
                    {
                        bad.Add($"{part.Id} ({band}, \"{word}\"): {part.Text}");
                    }
                }
            }
        }

        Assert.True(bad.Count == 0, string.Join(Environment.NewLine, bad));
    }

    [Fact]
    public void ExcludedIds_NameRealParts()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pool in Library)
        {
            foreach (var part in pool.Parts)
            {
                ids.Add(part.Id);
            }
        }

        var missing = new List<string>();

        foreach (var pool in Library)
        {
            foreach (var part in pool.Parts)
            {
                foreach (var excluded in part.Exclude ?? [])
                {
                    if (!ids.Contains(excluded))
                    {
                        missing.Add($"{part.Id} excludes {excluded}");
                    }
                }
            }
        }

        Assert.True(missing.Count == 0, string.Join(", ", missing));
    }

    [Fact]
    public void LoadOrCreate_WritesAMissingPoolFromTheDll()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sosariaai-persona-library-{Guid.NewGuid():N}");

        try
        {
            PersonaPartsFile.LoadOrCreate(directory);
            var idlePath = Path.Combine(directory, $"{PersonaPartsFile.PoolIdle}.json");
            File.Delete(idlePath);

            var parts = PersonaPartsFile.LoadOrCreate(directory);

            Assert.True(File.Exists(idlePath));
            Assert.Equal(EmbeddedText(PersonaPartsFile.PoolIdle), File.ReadAllText(idlePath));

            foreach (var pool in PersonaPartsFile.Pools)
            {
                var loaded = new HashSet<string>(StringComparer.Ordinal);

                foreach (var job in PersonJobs.All)
                {
                    foreach (var part in parts.For(pool, job, EraBand.Modern))
                    {
                        loaded.Add(part.Id);
                    }
                }

                Assert.Equal(PartsPerPool, loaded.Count);
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

    private static bool IsPrintableAscii(string text)
    {
        foreach (var c in text)
        {
            if (c is < FirstPrintable or > LastPrintable)
            {
                return false;
            }
        }

        return true;
    }

    private static string EmbeddedText(string pool)
    {
        using var stream = typeof(PersonaPartsFile).Assembly.GetManifestResourceStream(PersonaPartsFile.ResourceName(pool));
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}
