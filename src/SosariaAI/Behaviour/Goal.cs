using System;
using System.Collections.Generic;
using SosariaAI.Combat;
using SosariaAI.Configuration;

namespace SosariaAI.Behaviour;

public enum GoalKind
{
    Recover,
    Work,
    Trade,
    Hunt,
    Dungeon,
    Leisure,
    Pk,
    Ghost,
    Flee
}

public readonly record struct Goal(GoalKind Kind, string Target);

/// <summary>
/// Picks the goal the loop scores for: an interrupt or a big moment when one applies,
/// otherwise the long job of the person's phase. Free. No model.
/// </summary>
public static class GoalRules
{
    public const string NoTarget = "";

    public static RoutineFamily FamilyOf(GoalKind kind) =>
        kind switch
        {
            GoalKind.Recover => RoutineFamily.Rest,
            GoalKind.Work => RoutineFamily.Work,
            GoalKind.Trade => RoutineFamily.Trade,
            GoalKind.Hunt => RoutineFamily.Hunt,
            GoalKind.Dungeon => RoutineFamily.Dungeon,
            GoalKind.Leisure => RoutineFamily.Leisure,
            GoalKind.Pk => RoutineFamily.Pk,
            _ => RoutineFamily.Other
        };

    /// <summary>A lawful fighter that heard a PK report goes to seek the conflict where it happened.</summary>
    public static bool AnswersPkReport(Situation situation) =>
        situation is { HasPkReport: true, Role: CharacterRole.Fighter, Disposition: DispositionKind.Lawful };

    public static string Describe(Goal goal)
    {
        var kind = goal.Kind.ToString();

        if (string.IsNullOrWhiteSpace(goal.Target))
        {
            return kind;
        }

        return $"{kind} {goal.Target}";
    }

    /// <summary>
    /// Interrupts and big moments first: a ghost, a threat, low hits, a blue's way out of the
    /// Den, player conflict, a house, goods to sell, a party forming. Otherwise the long job the
    /// phase dice roll.
    /// <paramref name="skills"/> is the person's skill list; without it every job counts.
    /// </summary>
    public static Goal Pick(Situation situation, int seed = 0, IReadOnlyCollection<string> skills = null)
    {
        if (situation == null)
        {
            return new Goal(GoalKind.Work, NoTarget);
        }

        if (situation.IsGhost)
        {
            return new Goal(GoalKind.Ghost, NoTarget);
        }

        if (situation.MustFlee)
        {
            return new Goal(GoalKind.Flee, NoTarget);
        }

        if (GoalSwitch.ShouldSwitch(situation.FailedGoalCount))
        {
            return new Goal(GoalSwitch.Next(situation.CurrentGoal.Kind), NoTarget);
        }

        var needs = situation.Needs ?? new NeedsSnapshot();

        if (needs.HitsFraction < RecoveryRules.RecoverBelowHitsFraction)
        {
            return new Goal(GoalKind.Recover, NoTarget);
        }

        // A blue with no fight in the Den goes home first; its plan is the walk home alone.
        if (situation.LeavesDen)
        {
            return new Goal(GoalKind.Leisure, SkillKinds.GoHome);
        }

        // A PK hunter spends most of its phases hunting reds (PkHunterRules.HuntShare).
        if (situation.Disposition == DispositionKind.Outlaw ||
            AnswersPkReport(situation) ||
            situation.IsPkHunter && PkHunterRules.HuntsThisPhase(seed))
        {
            return new Goal(GoalKind.Pk, NoTarget);
        }

        if (situation.CanBuyHouse || situation.HouseDue)
        {
            return new Goal(GoalKind.Work, SkillKinds.House);
        }

        if (situation.HasSellGoods)
        {
            return new Goal(GoalKind.Trade, NoTarget);
        }

        if (needs.PartyForming)
        {
            return new Goal(GoalKind.Hunt, NoTarget);
        }

        return JobRules.GoalFor(JobRules.Roll(situation, seed, skills));
    }
}
