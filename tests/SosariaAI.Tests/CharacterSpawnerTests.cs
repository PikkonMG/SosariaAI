using System;
using System.IO;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Population;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class CharacterSpawnerTests
{
    private const string SpawnerRelativePath = "src/SosariaAI/Spawning/CharacterSpawner.cs";
    private const string InitializeStart = "public static void Initialize()";
    private const string InitializeEnd = "private static void PumpSlice";
    private const string PumpSliceEnd = "private static void FinishBoot";
    private const string BindFacetCall = "BindFacet(";
    private const string PulseCall = "LifecycleClock.AfterWorldLoad()";

    // The lifecycle pulse starts a timer, and the timer wheel needs its rings.
    static CharacterSpawnerTests() => Timer.Init(0);

    [Fact]
    public void Initialize_PulsesLifecycleAfterBindSoEveryoneLogsIn()
    {
        var source = ReadSpawnerSource();
        var start = source.IndexOf(InitializeStart, StringComparison.Ordinal);
        var end = source.IndexOf(InitializeEnd, StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start, "CharacterSpawner.Initialize must exist.");

        var initialize = source[start..end];
        var bindIndex = initialize.LastIndexOf(BindFacetCall, StringComparison.Ordinal);

        Assert.True(bindIndex >= 0, "CharacterSpawner.Initialize must bind saved characters.");
        Assert.Contains("over the plan cap in characters.json and were removed", initialize);

        // Binds and spawns drain in slices, so the pulse lives at the end of the
        // pump, not inside Initialize.
        var pumpStart = source.IndexOf(InitializeEnd, StringComparison.Ordinal);
        var pumpEnd = source.IndexOf(PumpSliceEnd, StringComparison.Ordinal);

        Assert.True(pumpStart >= 0 && pumpEnd > pumpStart, "CharacterSpawner.PumpSlice must exist.");

        var pump = source[pumpStart..pumpEnd];
        var pulseIndex = source.IndexOf(PulseCall, StringComparison.Ordinal);

        Assert.True(pulseIndex >= pumpEnd, "The lifecycle pulse must run after the last bind slice.");
        Assert.Contains(PulseCall, source[pumpEnd..]);
        Assert.Contains("PumpSlice", pump);
    }

    [Fact]
    public void Initialize_RunsAfterTheSettingsInitialize()
    {
        // The engine sorts every Initialize by CallPriority with an unstable sort: a tie
        // let the spawner run before the moongate guards were off and the graph was read.
        var spawner = typeof(CharacterSpawner).GetMethod(nameof(CharacterSpawner.Initialize));
        var settings = typeof(SosariaSettings).GetMethod(nameof(SosariaSettings.Initialize));

        Assert.True(new CallPriorityComparer().Compare(spawner, settings) > 0);
    }

    [Fact]
    public void AfterWorldLoad_EmptyWorld_DoesNotThrow()
    {
        LifecycleClock.AfterWorldLoad();
    }

    private static string ReadSpawnerSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, SpawnerRelativePath);

            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(SpawnerRelativePath);
    }
}
