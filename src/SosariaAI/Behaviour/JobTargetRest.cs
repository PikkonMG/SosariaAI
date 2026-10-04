using System;
using System.Collections.Generic;
using System.Globalization;
using Server;
using SosariaAI.Memory;
using SosariaAI.Mobiles;

namespace SosariaAI.Behaviour;

/// <summary>
/// Each person's streak of one job failing the same way at one target: a ghost, a mount, a
/// friend, a dungeon, a stable or the goal of a walk (<see cref="RepeatFailure.AfterTargetFailure"/>).
/// Three failures the same way rest the job for that target, and a rest grows as the streak
/// runs on. The job's start refuses a resting target, and the pickers pass it over. The
/// streaks live on the person (<see cref="SosariaCharacter.TargetStreaks"/>) and the save keeps
/// them, each rest as an absolute UTC time: a restart neither ends nor lengthens a rest.
/// World thread only.
/// </summary>
public static class JobTargetRest
{
    /// <summary>A job that refuses a resting target says so; that refusal is no new way of failing.</summary>
    public const string RestingWhy = "rests that target after failing the same way three times";

    /// <summary>Past this many streaks of one person the ones that ran out are dropped.</summary>
    public const int PruneAt = 64;

    /// <summary>Splits the job from the target in a streak's name, and the fields of a saved streak.</summary>
    private const char FieldSep = '\t';

    /// <summary>A saved streak: count, rest end in ticks, the spot's x, y and z, then the way it failed.</summary>
    private const int StreakFields = 6;

    private const int CountField = 0;
    private const int RestUntilField = 1;
    private const int SpotXField = 2;
    private const int SpotYField = 3;
    private const int SpotZField = 4;
    private const int ReasonField = 5;

    /// <summary>Notes a failure and returns the streak it makes. A refusal of a resting target changes nothing.</summary>
    public static TargetStreak NoteFailure(SosariaCharacter who, string job, JobTarget target, string reason, DateTime now)
    {
        var streaks = who.TargetStreaks;
        var name = NameOf(job, target.Key);
        var prior = StreakOf(streaks, name);

        if (reason == RestingWhy)
        {
            return prior;
        }

        Prune(streaks, now);
        var streak = RepeatFailure.AfterTargetFailure(prior, reason, now, target.Spot);
        streaks[name] = Record(streak);
        return streak;
    }

    /// <summary>The job worked at the target: its streak there ends.</summary>
    public static void NoteSuccess(SosariaCharacter who, string job, JobTarget target) =>
        who.TargetStreaks.Remove(NameOf(job, target.Key));

    /// <summary>True while this person's job rests for this target.</summary>
    public static bool Rests(SosariaCharacter who, string job, string targetKey, DateTime now) =>
        targetKey != null && RepeatFailure.TargetRests(StreakOf(who.TargetStreaks, NameOf(job, targetKey)), now);

    /// <summary>The places this person's job rests for now: a walk's goals, for the planner to pass over.</summary>
    public static List<Point3D> RestingSpots(SosariaCharacter who, string job, DateTime now)
    {
        var spots = new List<Point3D>();
        var jobPrefix = NameOf(job, string.Empty);

        foreach (var (name, record) in who.TargetStreaks)
        {
            if (name.StartsWith(jobPrefix, StringComparison.Ordinal) && TryRead(record, out var streak) &&
                streak.Spot != Point3D.Zero && RepeatFailure.TargetRests(streak, now))
            {
                spots.Add(streak.Spot);
            }
        }

        return spots;
    }

    /// <summary>The key a mobile target goes by: its serial, which a rename does not change.</summary>
    public static string KeyOf(Mobile mobile) => mobile == null ? null : KeyOf(mobile.Serial);

    public static string KeyOf(Serial serial) => serial.Value.ToString();

    // A job's name holds no tab, so the job and the tab open every name of its streaks.
    private static string NameOf(string job, string targetKey) => $"{job}{FieldSep}{targetKey}";

    private static TargetStreak StreakOf(Dictionary<string, string> streaks, string name) =>
        streaks.TryGetValue(name, out var record) && TryRead(record, out var streak) ? streak : default;

    private static string Record(TargetStreak streak) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{streak.Count}{FieldSep}{streak.RestUntil.Ticks}{FieldSep}{streak.Spot.X}{FieldSep}{streak.Spot.Y}{FieldSep}{streak.Spot.Z}{FieldSep}{streak.Reason}"
        );

    private static bool TryRead(string record, out TargetStreak streak)
    {
        streak = default;
        var fields = record?.Split(FieldSep, StreakFields);

        if (fields is not { Length: StreakFields } ||
            !int.TryParse(fields[CountField], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ||
            !long.TryParse(fields[RestUntilField], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks) ||
            ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks ||
            !int.TryParse(fields[SpotXField], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(fields[SpotYField], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
            !int.TryParse(fields[SpotZField], NumberStyles.Integer, CultureInfo.InvariantCulture, out var z))
        {
            return false;
        }

        streak = new TargetStreak(count, fields[ReasonField], new DateTime(ticks, DateTimeKind.Utc), new Point3D(x, y, z));
        return true;
    }

    private static void Prune(Dictionary<string, string> streaks, DateTime now)
    {
        if (streaks.Count < PruneAt)
        {
            return;
        }

        var spent = new List<string>();

        foreach (var (name, record) in streaks)
        {
            if (!TryRead(record, out var streak) || !RepeatFailure.TargetRests(streak, now))
            {
                spent.Add(name);
            }
        }

        for (var i = 0; i < spent.Count; i++)
        {
            streaks.Remove(spent[i]);
        }
    }
}
