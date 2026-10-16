using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Server;
using Server.Commands;
using Server.Logging;
using Server.Regions;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Admin;

/// <summary>
/// The minute pass over the population. It rides the <see cref="ActivityPulse"/> timer:
/// one census, one watchdog look per live character, and <see cref="StatusPage.FileName"/>
/// written again in the plugin's config folder.
/// </summary>
public static class FleetStatus
{
    public const string NoJob = "none";
    public const string WroteStatus = "Wrote the status page to {0}.";

    /// <summary>How long a count of the population serves the staff panel before it is taken again.</summary>
    public static readonly TimeSpan CountsLife = TimeSpan.FromSeconds(30);

    private static readonly ILogger logger = SosariaLog.For(typeof(FleetStatus));
    private static List<StuckLine> _stuck = [];
    private static bool _writeFailed;
    private static FleetCounts _counts;
    private static DateTime _countedAt;

    /// <summary>Characters the watchdog saw standing stalled on its last pass.</summary>
    public static int StuckCount => _stuck.Count;

    public static string StatusPath => ConfigFile.PathIn(StatusPage.FileName);

    public static void Initialize() => ActivityPulse.MinutePassed += OnMinute;

    /// <summary>[SosariaStatus: writes the page now with fresh counts and the last watchdog list.</summary>
    [Usage(CharacterCommands.StatusCommand)]
    [Description("Writes the SosariaAI status page now.")]
    public static void OnCommand(CommandEventArgs e)
    {
        e.Mobile?.SendMessage(string.Format(WroteStatus, WriteNow()));
    }

    /// <summary>
    /// The characters standing in the world now: everyone, save one the boot has not logged in
    /// yet (<see cref="Population.LifecycleClock"/>).
    /// </summary>
    public static List<SosariaCharacter> InWorld()
    {
        var found = new List<SosariaCharacter>();

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is SosariaCharacter { Deleted: false } character && People.InWorld(character))
            {
                found.Add(character);
            }
        }

        return found;
    }

    public static bool InCombat(SosariaCharacter character) =>
        character.Motor.Action is CharacterAction.Combat or CharacterAction.Flee ||
        character.Combatant is { Deleted: false, Alive: true };

    public static CharacterSample Sample(SosariaCharacter character)
    {
        var diag = character.Planning.Diag;
        var party = GameParty.InParty(character) ? GameParty.Of(character).Leader : null;
        return new CharacterSample(
            character.Map.Name,
            PkRules.IsRed(character.Kills),
            character.IsGhost,
            InCombat(character),
            character.Region?.IsPartOf<DungeonRegion>() == true,
            SupplyCheck.IsLow(character),
            party?.Serial.Value ?? CharacterSample.NoParty,
            character.Routine?.CurrentSkill?.Name ?? ActionDescriptions.Idle,
            JobRules.TryParse(character.ActiveGoalTarget, out var job) ? JobRules.NameOf(job) : NoJob,
            character.LocationDescription,
            diag.ChatCalls,
            diag.PlanningCalls
        );
    }

    /// <summary>
    /// The last count of the population, taken again only once it is older than
    /// <see cref="CountsLife"/>: the staff panel opens and redraws on every click.
    /// </summary>
    public static FleetCounts Counts()
    {
        if (_counts == null || TimeRules.Rested(_countedAt, Core.Now, CountsLife))
        {
            Count(InWorld());
        }

        return _counts;
    }

    private static FleetCounts Count(List<SosariaCharacter> live)
    {
        var samples = new List<CharacterSample>(live.Count);

        for (var i = 0; i < live.Count; i++)
        {
            samples.Add(Sample(live[i]));
        }

        _counts = FleetCounts.Of(samples);
        _countedAt = Core.Now;
        return _counts;
    }

    private static void OnMinute()
    {
        var live = InWorld();
        _stuck = FleetWatchdog.Pass(live, Core.Now);
        WriteReport(live);
    }

    /// <summary>Writes the page now with fresh counts and the last watchdog list. Returns its path.</summary>
    public static string WriteNow() => WriteReport(InWorld());

    private static string WriteReport(List<SosariaCharacter> live)
    {
        var now = Core.Now;
        var counts = Count(live);
        var repeatFailing = 0;

        for (var i = 0; i < live.Count; i++)
        {
            if (RepeatFailure.IsBlocked(live[i].FailedRoutineCount))
            {
                repeatFailing++;
            }
        }

        var report = new FleetReport(
            now,
            counts,
            _stuck,
            repeatFailing,
            JournalTally.Within(SosariaSettings.Journal?.Snapshot(), now, JournalTally.Window, StatusPage.MurderLimit),
            Brain.IsEnabled,
            FleetWatchdog.RoadMoves,
            FleetWatchdog.Rescores
        );

        var path = StatusPath;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, StatusPage.Render(report), Encoding.UTF8);
            _writeFailed = false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Once per failure streak: a locked file must not print a warning every minute.
            if (!_writeFailed)
            {
                logger.Warning(e, "Cannot write the status page {Path}", path);
            }

            _writeFailed = true;
        }

        return path;
    }
}
