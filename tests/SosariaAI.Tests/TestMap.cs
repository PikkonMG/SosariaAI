using System.Collections.Generic;
using System.Reflection;
using Server;
using Server.Items;

namespace SosariaAI.Tests;

/// <summary>
/// Mobile constructors read Map.Internal for a region. Test classes used to each
/// install the map and restore the previous slot on dispose, so a parallel class
/// finishing could null the map under another mid-test. Create it once and keep
/// it for the whole run.
/// </summary>
internal static class TestMap
{
    private const int InternalMapIndex = 0x7F;
    private const int LandMapIndex = 0x10;
    private const int LandSectors = 8;
    private const string ItemPersistenceField = "_itemPersistence";
    private const string MobilePersistenceField = "_mobilePersistence";
    private const string LastSerialField = "_lastEntitySerial";
    private const string SettingsField = "_settings";

    /// <summary>Above every fixed item serial a test names (the highest start is 0x4F000000).</summary>
    private const uint EngineItemSerialStart = 0x60000000;

    /// <summary>Above every fixed mobile serial a test names (the highest is below 0x800000).</summary>
    private const uint EngineMobileSerialStart = 0x20000000;

    /// <summary>
    /// The internal map, and the engine's type lookup a kit check reads by name: a person's
    /// armed check asks it for each kit piece.
    /// </summary>
    internal static void EnsureInternal()
    {
        AssemblyHandler.Assemblies ??= [typeof(Item).Assembly, typeof(Katana).Assembly];
        Map.Maps[InternalMapIndex] ??= new Map(
            InternalMapIndex, InternalMapIndex, InternalMapIndex,
            Map.SectorSize, Map.SectorSize, 1, "Internal", MapRules.Internal
        );
    }

    /// <summary>
    /// Item.Delete throws unless the world runs. The engine can make no entity before
    /// then, so its serial cursors move past the fixed test serials here, once.
    /// </summary>
    internal static void EnsureRunningWorld()
    {
        if (World.WorldState == WorldState.Running)
        {
            return;
        }

        StartEngineSerialsAt(ItemPersistenceField, EngineItemSerialStart);
        StartEngineSerialsAt(MobilePersistenceField, EngineMobileSerialStart);
        typeof(World).GetProperty(nameof(World.WorldState))!.SetValue(null, WorldState.Running);
    }

    /// <summary>
    /// Tests give fixed serials to what they build, and one world holds them all. The engine
    /// gives its own entities the next free serial past its last one, and the kit tests
    /// dress tens of thousands of items, so from its first serial it ran into the fixed
    /// ones. Past these starts it never reaches them.
    /// </summary>
    private static void StartEngineSerialsAt(string persistenceField, uint lastSerial)
    {
        var persistence = typeof(World).GetField(persistenceField, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        persistence.GetType().BaseType!
            .GetField(LastSerialField, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(persistence, (Serial)lastSerial);
    }

    /// <summary>
    /// Deletes what a test stood on the maps. Every class shares one world, so what a test
    /// leaves there is found by the next class that reads the same tiles.
    /// </summary>
    internal static void Remove(IEnumerable<IEntity> placed)
    {
        foreach (var entity in placed)
        {
            entity.Delete();
        }
    }

    /// <summary>
    /// A small non-internal map so item Parent=null hits a real sector list.
    /// Map.Internal skips OnEnter, which hides the death-flatten crash.
    /// </summary>
    /// <summary>
    /// The engine's decay scheduler, which the server configures at boot. An item that leaves a
    /// container on a real map registers with it, as the goods and coin of a secure-trade window
    /// do when the swap runs; unconfigured, the registration throws. It reads the server
    /// settings, which a test run never loads, so empty settings stand in (every decay key falls
    /// back to its default). Its timer never starts here: nothing decays in a test.
    /// </summary>
    internal static void EnsureDecayScheduler()
    {
        if (DecayScheduler.Shared != null)
        {
            return;
        }

        var settings = typeof(ServerConfiguration).GetField(SettingsField, BindingFlags.NonPublic | BindingFlags.Static)!;
        settings.SetValue(null, settings.GetValue(null) ?? new ServerSettings());
        DecayScheduler.Configure();
    }

    internal static Map EnsureLand()
    {
        EnsureInternal();
        var size = Map.SectorSize * LandSectors;
        return Map.Maps[LandMapIndex] ??= new Map(
            0, LandMapIndex, 0, size, size, 0, "TestLand", MapRules.None
        );
    }
}
