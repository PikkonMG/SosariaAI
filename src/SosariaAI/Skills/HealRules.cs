using System;
using Server;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Binding wounds between fights with what a player carries: the engine's bandage (its own
/// timer and Healing check), a heal potion, or a heal spell, on oneself or on someone
/// hurt nearby. Pre-AOS a bandage reaches one tile. No resurrection (GhostSkill).
/// </summary>
public static class HealRules
{
    public const int ReachTiles = 1;

    /// <summary>A healer looks this far for someone hurt when it is whole itself.</summary>
    public const int PatientRange = 8;

    public const int StartingBandageAmount = 20;

    /// <summary>One tending gives up after this long.</summary>
    public static readonly TimeSpan TendLimit = TimeSpan.FromSeconds(60);

    public static bool InReach(Point3D from, Point3D to) => NavMetric.Chebyshev(from, to) <= ReachTiles;

    /// <summary>Poisoned, or below the line a player tops up at between fights.</summary>
    public static bool NeedsHeal(int hits, int hitsMax, bool poisoned) =>
        SelfCareRules.NeedsCare(poisoned, hitsMax <= 0 ? Vitals.FullHits : (double)hits / hitsMax, inFight: false);

    /// <summary>
    /// A blue tends no outlaw, even where the Den rule keeps it from drawing on one. A red
    /// tends its own kind.
    /// </summary>
    public static bool MayTend(bool healerIsRed, bool patientIsOutlaw) => healerIsRed || !patientIsOutlaw;

    public static bool MayBegin(SosariaCharacter healer) => healer is { Deleted: false } && People.InWorld(healer);

    public static bool TimedOut(DateTime now, DateTime started) => TimeRules.Passed(started, now, TendLimit);

    /// <summary>A tending that ran out of remedies still did its job above the recovery line.</summary>
    public static SkillStatus Outcome(double hitsFraction) =>
        hitsFraction >= RecoveryRules.RecoverBelowHitsFraction ? SkillStatus.Done : SkillStatus.Failed;
}
