using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Skills;

namespace SosariaAI.Admin;

/// <summary>
/// One look at a live character. <see cref="Advanced"/> is true when a step other than
/// Decide, wander or rest finished well since the last look, or the same step turned out
/// more goods (ore, logs, items banked). <see cref="Held"/> is true while standing still
/// is the point of the step.
/// </summary>
public readonly record struct StallSample(string Map, Point3D At, bool Advanced, bool Held);

/// <summary>Where a character stood still and since when. One per live character.</summary>
public sealed class StallTrack
{
    public string Map { get; set; }

    public Point3D Anchor { get; set; }

    /// <summary>When the still time began, moved forward by every held span so a hold pauses the clock.</summary>
    public DateTime StillSince { get; set; }

    public DateTime LastSeenAt { get; set; }

    public DateTime LastRescueAt { get; set; }

    /// <summary>Rescues since the character last made progress.</summary>
    public int Rescues { get; set; }

    public bool Started => StillSince != default;
}

public enum StallVerdict
{
    /// <summary>Moved off the spot or finished work: the clock starts over.</summary>
    Progress,

    /// <summary>A legitimate hold: talking, fighting, hunting, banking, resting, crafting. The clock pauses.</summary>
    Held,

    /// <summary>Still, but not for long enough, or a rescue ran a short while ago.</summary>
    Waiting,

    /// <summary>The first rescue since the last progress. The only one that is logged.</summary>
    Rescue,

    /// <summary>A later rescue for the same stall, done without a new log line.</summary>
    RescueAgain
}

/// <summary>
/// The fleet watchdog's stall test. A walk leg gives up after <see cref="GoToSkill.GiveUp"/>
/// and repeated stalls on one tile run the marooned rescue after
/// <see cref="HomeLeash.MaroonedCheckCooldown"/>. The watchdog waits longer than both
/// together, so it only catches the characters those checks miss. Pure.
/// </summary>
public static class StallRules
{
    /// <summary>Extra minutes on top of the per-skill give-ups before the watchdog steps in.</summary>
    public const int SafetyMarginMinutes = 3;

    /// <summary>Drift inside this many tiles is standing still: a stuck walker sidesteps back and forth.</summary>
    public const int StillRadius = HomeLeash.MaroonedStallRadius;

    public static readonly TimeSpan StallAfter =
        GoToSkill.GiveUp + HomeLeash.MaroonedCheckCooldown + TimeSpan.FromMinutes(SafetyMarginMinutes);

    /// <summary>A rescue that did not help is tried again only after a full stall span.</summary>
    public static readonly TimeSpan RescueRetryAfter = StallAfter;

    /// <summary>
    /// A hold pauses the clock rather than resetting it. A walker that fails, banks in
    /// place and fails again spends part of its time in a bank step; only finished work
    /// or a real move starts the clock over.
    /// </summary>
    public static StallVerdict Observe(StallTrack track, StallSample sample, DateTime now)
    {
        var sinceLastLook = track.LastSeenAt == default ? TimeSpan.Zero : now - track.LastSeenAt;
        track.LastSeenAt = now;

        if (!track.Started || Moved(track, sample) || sample.Advanced)
        {
            track.Map = sample.Map;
            track.Anchor = sample.At;
            track.StillSince = now;
            track.LastRescueAt = default;
            track.Rescues = 0;
            return StallVerdict.Progress;
        }

        if (sample.Held)
        {
            track.StillSince += sinceLastLook;
            return StallVerdict.Held;
        }

        if (!IsStalled(track, now) ||
            !TimeRules.Rested(track.LastRescueAt, now, RescueRetryAfter))
        {
            return StallVerdict.Waiting;
        }

        track.LastRescueAt = now;
        track.Rescues++;
        return track.Rescues == 1 ? StallVerdict.Rescue : StallVerdict.RescueAgain;
    }

    public static bool IsStalled(StallTrack track, DateTime now) =>
        track.Started && now - track.StillSince >= StallAfter;

    public static int StillMinutes(StallTrack track, DateTime now) =>
        track.Started ? (int)Math.Max(0, (now - track.StillSince).TotalMinutes) : 0;

    /// <summary>
    /// Standing still is the point of the step: talking, fighting or hunting at a spawn,
    /// a crafting session at a station, or a step that waits in place by design.
    /// </summary>
    public static bool IsHeld(string skillKind, bool craftStation, bool talking, bool fighting, bool hunting) =>
        talking || fighting || hunting || craftStation || IsHoldSkill(skillKind);

    public static bool IsHoldSkill(string skillKind) =>
        PracticeRules.IsPractice(skillKind) ||
        skillKind is SkillKinds.Rest or SkillKinds.Camp or SkillKinds.Tavern or SkillKinds.Loiter
            or SkillKinds.Meditate or SkillKinds.BankCrowd or SkillKinds.BankDeposit or SkillKinds.BankShop
            or SkillKinds.PlayerVendor or SkillKinds.House or SkillKinds.Fish or SkillKinds.Boat
            or SkillKinds.Follow;

    private static bool Moved(StallTrack track, StallSample sample) =>
        !string.Equals(track.Map, sample.Map, StringComparison.OrdinalIgnoreCase) ||
        NavMetric.Chebyshev(track.Anchor, sample.At) > StillRadius;
}
