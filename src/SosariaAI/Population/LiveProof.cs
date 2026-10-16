using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Server;
using Server.Commands;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Logging;

namespace SosariaAI.Population;

/// <summary>
/// No-login sample of the crowd into <see cref="FileName"/>: every <see cref="SampleSeconds"/> for
/// <see cref="DurationMinutes"/> minutes, the steps done and failed, the stuck, the live, the
/// regions they stand in, the speech and the time one sample took. It starts
/// <see cref="StartDelaySeconds"/> after every boot, and staff start it again with
/// [<see cref="CommandName"/>; each run writes over the last one's file. It listens to each
/// routine once and lets go of all of them when the run ends. It does not start the shard.
/// </summary>
public static class LiveProof
{
    public const string CommandName = "SosariaLiveProof";
    public const string FileName = "live-proof.tsv";
    public const int StartDelaySeconds = 15;
    public const int DurationMinutes = 20;
    public const int SampleSeconds = 30;

    private static readonly ILogger console = SosariaLog.Console(typeof(LiveProof));
    private static TimerExecutionToken _token;
    private static DateTime _started;
    private static int _completed;
    private static int _failed;
    private static int _speech;
    private static readonly StringBuilder _log = new();
    private static readonly HashSet<Routine> _watched = [];

    public static void Initialize() => EventSink.ServerStarted += OnServerStarted;

    private static void OnServerStarted() =>
        Timer.StartTimer(TimeSpan.FromSeconds(StartDelaySeconds), Begin);

    /// <summary>[SosariaLiveProof. <see cref="CharacterCommands"/> registers it with the other staff commands.</summary>
    [Usage(CommandName)]
    [Description("Starts a timed population check into a live-proof file.")]
    public static void OnStart(CommandEventArgs e)
    {
        Begin();
        e.Mobile?.SendMessage(
            $"{CommandName} sampling every {SampleSeconds}s for {DurationMinutes} minutes."
        );
    }

    public static void Begin()
    {
        _token.Cancel();
        StopWatching();
        _started = Core.Now;
        _completed = 0;
        _failed = 0;
        _speech = 0;
        _log.Clear();
        _log.Append("time\tcompleted\tfailed\tstuck\tlive\tareas\tspeech\tsampleMs\n");
        Timer.StartTimer(
            TimeSpan.FromSeconds(SampleSeconds),
            TimeSpan.FromSeconds(SampleSeconds),
            Sample,
            out _token
        );
        console.Information("{Command} started", CommandName);
        Sample();
    }

    // A routine can be replaced; a new one is heard from the sample that first sees it.
    private static void Watch(Routine routine)
    {
        if (routine != null && _watched.Add(routine))
        {
            routine.SkillEnded += OnSkillEnded;
        }
    }

    private static void StopWatching()
    {
        foreach (var routine in _watched)
        {
            routine.SkillEnded -= OnSkillEnded;
        }

        _watched.Clear();
    }

    private static void OnSkillEnded(SosariaCharacter character, Skills.Skill skill, Skills.SkillStatus status)
    {
        if (status == Skills.SkillStatus.Done && RepeatFailure.Clears(skill.Name))
        {
            _completed++;
        }
        else if (status == Skills.SkillStatus.Failed && RepeatFailure.Counts(skill.Name))
        {
            _failed++;
        }

        if (character.RecentSpeech().Count > 0)
        {
            _speech++;
        }
    }

    private static void Sample()
    {
        var sampleStart = Core.TickCount;
        var live = 0;
        var stuck = 0;
        var areas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is not SosariaCharacter character || character.Deleted || !People.InWorld(character))
            {
                continue;
            }

            Watch(character.Routine);
            live++;
            var region = character.Region?.Name;

            if (!string.IsNullOrWhiteSpace(region))
            {
                areas.Add(region);
            }

            if (character.FailedRoutineCount >= RepeatFailure.Limit)
            {
                stuck++;
            }
        }

        var sampleMs = (int)(Core.TickCount - sampleStart);
        var minutes = (int)(Core.Now - _started).TotalMinutes;
        _log.Append(Core.Now.ToString("HH:mm:ss"));
        _log.Append('\t');
        _log.Append(_completed);
        _log.Append('\t');
        _log.Append(_failed);
        _log.Append('\t');
        _log.Append(stuck);
        _log.Append('\t');
        _log.Append(live);
        _log.Append('\t');
        _log.Append(areas.Count);
        _log.Append('\t');
        _log.Append(_speech);
        _log.Append('\t');
        _log.Append(sampleMs);
        _log.Append('\n');

        if (minutes >= DurationMinutes)
        {
            Finish();
        }
    }

    private static void Finish()
    {
        _token.Cancel();
        StopWatching();
        var path = ConfigFile.PathIn(FileName);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, _log.ToString(), Encoding.UTF8);
            console.Information("{Command} wrote {Path}", CommandName, path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            console.Warning(e, "{Command} cannot write {Path}", CommandName, path);
        }
    }
}
