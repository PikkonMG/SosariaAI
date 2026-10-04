using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Logging;
using Server.Engines.Spawners;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Navigation;
using SosariaAI.Spawning;

namespace SosariaAI.Admin;

/// <summary>
/// First time setup for a fresh save. The plugin does not fill an empty world by itself:
/// characters wait until a staff member presses First Time Setup in [SosariaPanel. The
/// button runs the engine's own world commands one after another, then brings the
/// population in. The world itself says whether it is set up: a save with spawners or
/// bankers in it is, so deleting the saves always asks for the setup again.
/// </summary>
public static class WorldSetup
{
    public static readonly TimeSpan StepGap = TimeSpan.FromSeconds(2);

    /// <summary>The client needs a moment in the world before a gump shows.</summary>
    public static readonly TimeSpan LoginPromptDelay = TimeSpan.FromSeconds(3);

    public const string LoginPrompt =
        "This world is not set up yet. Press First Time Setup in the SosariaAI panel to build it and bring the characters in.";

    private const int PromptHue = 0x26;

    public const string WaitingLine =
        "The world is empty. Log in as staff, type [SosariaPanel and press First Time Setup; characters spawn after it";

    private static readonly ILogger console = SosariaLog.Console(typeof(WorldSetup));

    private static bool _running;

    public static bool IsRunning => _running;

    public static void Configure() => EventSink.Connected += OnConnected;

    // An administrator who logs into an unset world is shown the panel at once.
    private static void OnConnected(Mobile mobile)
    {
        if (!Waiting || mobile == null || mobile.AccessLevel < SosariaPanelGump.SetupAccess)
        {
            return;
        }

        Timer.StartTimer(
            LoginPromptDelay,
            () =>
            {
                if (!Waiting || mobile.NetState == null)
                {
                    return;
                }

                mobile.SendMessage(PromptHue, LoginPrompt);
                SosariaPanelGump.DisplayTo(mobile);
            }
        );
    }

    /// <summary>Set at boot while the world waits for First Time Setup. Saved characters stay logged out.</summary>
    public static bool Waiting { get; private set; }

    /// <summary>True while the world still needs First Time Setup.</summary>
    public static bool AwaitsSetup()
    {
        Waiting = WorldSetupRules.AwaitsSetup(WorldHasSpawners(), WorldHasBankers());
        return Waiting;
    }

    /// <summary>Runs the setup for <paramref name="staff"/>, who pressed the button. Game thread only.</summary>
    public static void Run(Mobile staff)
    {
        if (_running || staff == null)
        {
            return;
        }

        _running = true;
        var steps = WorldSetupRules.Steps(
            EraBands.Current(),
            EnabledFacets(),
            folder => Directory.Exists(Path.Combine(Core.BaseDirectory, folder))
        );
        console.Information("{Staff} started First Time Setup ({Count} steps)", staff.Name, steps.Count);
        RunStep(staff, steps, 0);
    }

    private static void RunStep(Mobile staff, List<SetupStep> steps, int index)
    {
        if (index >= steps.Count)
        {
            Finish(staff);
            return;
        }

        var step = steps[index];
        staff.SendMessage($"Setup {index + 1}/{steps.Count}: [{step.Command} {step.Arguments}");

        // The engine's handler runs for the staff member who pressed the button. The panel
        // already required an administrator; the table entry itself asks for a developer.
        if (CommandSystem.Entries.TryGetValue(step.Command, out var entry))
        {
            var arguments = string.IsNullOrEmpty(step.Arguments) ? [] : new[] { step.Arguments };
            entry.Handler(new CommandEventArgs(staff, step.Command, step.Arguments, arguments));
        }
        else
        {
            console.Warning("First Time Setup skipped [{Command}: the engine has no such command", step.Command);
        }

        Timer.StartTimer(StepGap, () => RunStep(staff, steps, index + 1));
    }

    /// <summary>
    /// The world is set up. The saved nav graphs are checked now against the items the setup
    /// placed, and a facet with none is built (every facet when <c>nav.rebuildOnBoot</c> is
    /// true), never before it (<see cref="NavBootRules"/>); then the population comes in.
    /// </summary>
    private static void Finish(Mobile staff)
    {
        _running = false;
        Waiting = false;
        staff.SendMessage("First Time Setup is done. The navigation graphs are checked now, then the characters come in.");
        console.Information(NavBootRules.SetupCheckLine);
        NavWorld.CheckAfterSetup();
        console.Information("First Time Setup is done; spawning the population");
        CharacterSpawner.StartAfterSetup();
    }

    // A facet the era does not have gets no spawners, as it gets no people.
    private static IEnumerable<string> EnabledFacets()
    {
        var maps = SosariaSettings.Characters?.Maps;

        if (maps == null)
        {
            yield break;
        }

        var expansion = EraRules.Current();

        foreach (var (facet, toggle) in maps)
        {
            if (toggle is { Enabled: true } && EraRules.FacetAllowed(facet, expansion))
            {
                yield return facet;
            }
        }
    }

    private static bool WorldHasBankers()
    {
        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is Banker { Deleted: false })
            {
                return true;
            }
        }

        return false;
    }

    private static bool WorldHasSpawners()
    {
        foreach (var item in World.Items.Values)
        {
            if (item is ISpawner { Deleted: false })
            {
                return true;
            }
        }

        return false;
    }
}
