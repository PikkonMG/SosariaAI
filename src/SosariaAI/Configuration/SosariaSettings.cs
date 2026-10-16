using System;
using Server;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Deliberation;
using SosariaAI.Memory;
using SosariaAI.Navigation;
using SosariaAI.Social;
using SosariaAI.Logging;

namespace SosariaAI.Configuration;

/// <summary>
/// Operator settings. Character routines live in
/// Configuration/sosariaai/characters.json. Idle lines come from the persona file.
/// Unprompted musing uses musingIntervalMinutes in that file.
/// </summary>
public static class SosariaSettings
{
    private static readonly ILogger logger = SosariaLog.For(typeof(SosariaSettings));

    public static TimeSpan MusingInterval { get; private set; }

    public static bool LogActivity { get; private set; }

    public static CharactersConfiguration Characters { get; private set; }

    /// <summary>
    /// Gold a character keeps in the bank when it shops for gear. The
    /// career.ignorePriceLimits test flag zeroes it; a career.goldReserve of 0 keeps none, and
    /// a negative one falls back to the default.
    /// </summary>
    public static int GearGoldReserve =>
        Characters?.Career?.EffectiveGoldReserve() ?? CareerSettings.DefaultGoldReserve;

    public static void Configure()
    {
        ActivityFile.Open(ConfigFile.RootDirectory);
        Characters = CharactersFile.LoadOrCreate(CharactersFile.DefaultPath);
        LogActivity = Characters.LogActivity;
        MusingInterval = MusingRules.IntervalFromMinutes(Characters.MusingIntervalMinutes);
        Journal = new EventJournal(MemoryStore.Shared);
        GuildWars = new GuildWarRegistry();
    }

    /// <summary>
    /// ModernUO runs Initialize after the tile matrix, the regions and the world are
    /// loaded. Graph generation reads tiles and finds regions, so it cannot run in
    /// Configure. Saved corpses are cleaned once here, before any client can log in; a corpse
    /// made later is cleaned where it is made. A world save only asks the memory writer to
    /// write; the shutdown close waits for it.
    /// </summary>
    public static void Initialize()
    {
        CorpseSanitizer.CleanWorld();
        MoongateGuards.Apply(Characters.FeluccaGuardedMoongates);
        GuardFreeRooms.Apply();
        NavWorld.Load();
        PathSearch.Start();
        MemoryStore.Shared.Open(MemoryStore.DefaultPath());
        Journal.Restore(Core.Now);
        AdventureTracker.Shared.Start();
        ActivityPulse.Start();
        EventSink.Shutdown += PathSearch.Stop;
        EventSink.WorldSave += MemoryStore.Shared.WriteSoon;
        EventSink.Shutdown += AdventureTracker.Shared.Stop;
        EventSink.Shutdown += MemoryStore.Shared.Close;
        EventSink.Shutdown += ActivityFile.WriteThrough;
        EventSink.ServerCrashed += OnServerCrashed;
        WarnOffEraFixtures();
    }

    /// <summary>
    /// A fixture keeps its authored persona in any era. When that persona is tagged for
    /// other eras, the operator hears about it once at boot.
    /// </summary>
    private static void WarnOffEraFixtures()
    {
        var band = EraBands.Current();

        foreach (var (characterId, personaId) in PersonaEras.MisfitFixtures(Characters, Brain.Personas, band))
        {
            logger.Warning(
                "{Character} keeps persona {Persona}, which is tagged for other eras than {Era}",
                characterId,
                personaId,
                EraBands.Tag(band)
            );
        }
    }

    public static EventJournal Journal { get; private set; }

    public static GuildWarRegistry GuildWars { get; private set; } = new GuildWarRegistry();

    /// <summary>The activity file holds its last lines in a buffer; a crash writes them out.</summary>
    private static void OnServerCrashed(ServerCrashedEventArgs e) => ActivityFile.WriteThrough();
}
