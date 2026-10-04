using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;

namespace SosariaAI.Behaviour;

public readonly record struct ActionId(string Value)
{
    public static ActionId From(string routineId, SkillStepDefinition step, int index)
    {
        var routine = string.IsNullOrWhiteSpace(routineId) ? "default" : routineId;
        var skill = string.IsNullOrWhiteSpace(step?.Skill) ? "none" : step.Skill;

        if (!string.IsNullOrWhiteSpace(step?.Destination))
        {
            return new ActionId($"{routine}:{skill}:{step.Destination}");
        }

        if (!string.IsNullOrWhiteSpace(step?.Area?.Name))
        {
            return new ActionId($"{routine}:{skill}:{step.Area.Name}");
        }

        if (step?.Target != Point3D.Zero)
        {
            return new ActionId($"{routine}:{skill}:{step.Target.X},{step.Target.Y},{step.Target.Z}");
        }

        if (step?.BankSpot != Point3D.Zero)
        {
            return new ActionId($"{routine}:{skill}:bank");
        }

        return new ActionId($"{routine}:{skill}:{index}");
    }

    public static string SkillKindOf(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var parts = id.Split(':');
        return parts.Length >= 2 ? parts[1] : null;
    }
}

public sealed class ActionCandidate
{
    public ActionId Id { get; init; }

    public string SkillKind { get; init; }

    public string RoutineId { get; init; }

    public SkillStepDefinition Step { get; init; }

    public int RequiredPower { get; init; }
}

public readonly record struct ScoredAction(
    ActionId Id,
    string SkillKind,
    string RoutineId,
    double Score,
    string Why);

public sealed class ScoreResult
{
    public Goal Goal { get; init; }

    public IReadOnlyList<ScoredAction> Ranked { get; init; } = [];

    public ScoredAction Winner { get; init; }

    public string WinnerReason { get; init; }

    public GoalPlan Plan { get; init; }

    public IReadOnlyList<ScoredAction> Top3
    {
        get
        {
            if (Ranked == null || Ranked.Count == 0)
            {
                return [];
            }

            var take = Ranked.Count < ActionScorer.TopCount ? Ranked.Count : ActionScorer.TopCount;
            var top = new ScoredAction[take];

            for (var i = 0; i < take; i++)
            {
                top[i] = Ranked[i];
            }

            return top;
        }
    }
}
