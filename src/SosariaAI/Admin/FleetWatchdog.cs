using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Skill = SosariaAI.Skills.Skill;

namespace SosariaAI.Admin;

/// <summary>
/// Catches live characters the per-skill stall checks miss: no move off the spot and no
/// finished work for <see cref="StallRules.StallAfter"/> outside a legitimate hold. It runs
/// once a minute from the status pass, not per think. The rescue is the one a stuck player
/// had: the marooned move back to a road, or else the step ended as a failure, so the scorer
/// picks again and the failure bars that step on this spot for a while.
/// </summary>
public static class FleetWatchdog
{
    /// <summary>The end line of a step the watchdog ends: it failed for standing still.</summary>
    public const string StalledWhy = "no progress on this spot";

    private static readonly ILogger logger = SosariaLog.For(typeof(FleetWatchdog));
    private static readonly Dictionary<Serial, Watch> Watches = new();

    private sealed class Watch
    {
        public readonly StallTrack Track = new();
        public Routine Routine;
        public Skill Skill;
        public string Activity;
        public bool FinishedWork;
    }

    public static int RoadMoves { get; private set; }

    public static int Rescores { get; private set; }

    /// <summary>One look at every live character. Returns the ones standing stalled now.</summary>
    public static List<StuckLine> Pass(IReadOnlyList<SosariaCharacter> live, DateTime now)
    {
        var stuck = new List<StuckLine>();
        var seen = new HashSet<Serial>();

        for (var i = 0; i < live.Count; i++)
        {
            var character = live[i];

            // A ghost walks to a healer on its own rules; the ghost fallback covers it.
            if (character.IsGhost)
            {
                continue;
            }

            seen.Add(character.Serial);

            if (!Watches.TryGetValue(character.Serial, out var watch))
            {
                watch = new Watch();
                Watches[character.Serial] = watch;
            }

            var verdict = StallRules.Observe(watch.Track, Look(character, watch, now), now);

            if (verdict is StallVerdict.Rescue or StallVerdict.RescueAgain)
            {
                Rescue(character, watch.Track, logged: verdict == StallVerdict.Rescue, now);
            }

            if (StallRules.IsStalled(watch.Track, now))
            {
                stuck.Add(
                    new StuckLine(
                        character.Name,
                        character.CurrentActivity,
                        character.LocationDescription,
                        StallRules.StillMinutes(watch.Track, now),
                        watch.Track.Rescues
                    )
                );
            }
        }

        ForgetGone(seen);
        return stuck;
    }

    private static StallSample Look(SosariaCharacter character, Watch watch, DateTime now)
    {
        Listen(character, watch);
        var skill = character.Routine?.CurrentSkill;
        var activity = character.CurrentActivity;
        var worked = skill != null && ReferenceEquals(skill, watch.Skill) &&
                     !string.Equals(activity, watch.Activity, StringComparison.Ordinal);
        var advanced = worked || watch.FinishedWork;
        watch.Skill = skill;
        watch.Activity = activity;
        watch.FinishedWork = false;

        var held = StallRules.IsHeld(
            skill?.Name,
            skill is CraftStationSkill,
            character.Conversation.IsActive(now),
            FleetStatus.InCombat(character),
            RoutineDriver.IsHunting(character)
        );

        return new StallSample(character.Map?.Name, character.Location, advanced, held);
    }

    /// <summary>A routine can be replaced; listen to the one the character has now.</summary>
    private static void Listen(SosariaCharacter character, Watch watch)
    {
        var routine = character.Routine;

        if (ReferenceEquals(routine, watch.Routine))
        {
            return;
        }

        if (watch.Routine != null)
        {
            watch.Routine.SkillEnded -= OnSkillEnded;
        }

        watch.Routine = routine;

        if (routine != null)
        {
            routine.SkillEnded += OnSkillEnded;
        }
    }

    private static void OnSkillEnded(SosariaCharacter character, Skill skill, SkillStatus status)
    {
        if (character != null && status == SkillStatus.Done && RepeatFailure.Counts(skill?.Name) &&
            Watches.TryGetValue(character.Serial, out var watch))
        {
            watch.FinishedWork = true;
        }
    }

    private static void Rescue(SosariaCharacter character, StallTrack track, bool logged, DateTime now)
    {
        var at = character.Location;
        var activity = character.CurrentActivity;
        var movedToRoad = character.TryMaroonedRescue();

        if (movedToRoad)
        {
            RoadMoves++;
        }
        else
        {
            Rescores++;
            character.Routine?.FailActive(StalledWhy);
            character.Pulse.WakeNow();
        }

        if (logged && SosariaSettings.LogActivity)
        {
            logger.Information(
                "Watchdog: {Name} made no progress for {Minutes} minutes at {Location} while {Activity}; {Action}",
                character.Name,
                StallRules.StillMinutes(track, now),
                at,
                activity,
                movedToRoad ? "moved back to a road" : "dropped the plan to choose again"
            );
        }
    }

    private static void ForgetGone(HashSet<Serial> seen)
    {
        List<Serial> gone = null;

        foreach (var pair in Watches)
        {
            if (!seen.Contains(pair.Key))
            {
                (gone ??= []).Add(pair.Key);
            }
        }

        if (gone == null)
        {
            return;
        }

        for (var i = 0; i < gone.Count; i++)
        {
            if (Watches.Remove(gone[i], out var watch) && watch.Routine != null)
            {
                watch.Routine.SkillEnded -= OnSkillEnded;
            }
        }
    }
}
