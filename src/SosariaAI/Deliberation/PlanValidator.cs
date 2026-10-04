using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;

namespace SosariaAI.Deliberation;

public sealed record PlanProposal(
    string CharacterId,
    int ExpectedRevision,
    string Goal,
    string Reason,
    string Success,
    IReadOnlyList<ModelPlanStep> Steps,
    long RequestId
);

public static class PlanValidator
{
    public const string RejectEmpty = "empty-plan";
    public const string RejectTooManySteps = "too-many-steps";
    public const string RejectBadSkill = "unsupported-action";
    public const string RejectDecide = "decide-is-not-a-step";
    public const string RejectTarget = "unknown-target";
    public const string RejectCoordinates = "invented-coordinates";
    public const string RejectEra = "era-forbidden";
    public const string RejectSequence = "sequence-cannot-run";
    public const string RejectIdentity = "wrong-character";
    public const string RejectStale = "stale-reply";
    public const string RejectDead = "character-dead";
    public const string RejectOffWorld = "character-off-world";
    public const string RejectAgreement = "player-agreement-changed";
    public const string RejectSchema = "bad-schema";

    public static string Reject(
        PlanProposal proposal,
        IReadOnlyList<string> availableSkills,
        IReadOnlyList<string> destinations,
        IReadOnlyList<string> people,
        IReadOnlyList<string> items,
        Expansion expansion
    )
    {
        if (proposal == null || proposal.Steps == null || proposal.Steps.Count == 0)
        {
            return RejectEmpty;
        }

        if (proposal.Steps.Count > ModelPlan.MaxSteps)
        {
            return RejectTooManySteps;
        }

        if (string.IsNullOrWhiteSpace(proposal.Goal) || string.IsNullOrWhiteSpace(proposal.Reason))
        {
            return RejectSchema;
        }

        var skills = ToSet(availableSkills);

        for (var i = 0; i < proposal.Steps.Count; i++)
        {
            var step = proposal.Steps[i];
            var skill = step.SkillKind;

            if (string.IsNullOrWhiteSpace(skill))
            {
                return RejectBadSkill;
            }

            if (skill.Equals(SkillKinds.Decide, StringComparison.OrdinalIgnoreCase))
            {
                return RejectDecide;
            }

            if (!skills.Contains(skill))
            {
                return RejectBadSkill;
            }

            if (!EraRules.SkillAllowed(skill, expansion))
            {
                return RejectEra;
            }

            if (PlanRefs.LooksLikeCoordinates(step.TargetRef))
            {
                return RejectCoordinates;
            }

            if (!PlanRefs.IsKnown(step.TargetRef, destinations, people, items))
            {
                return RejectTarget;
            }
        }

        if (!SequenceCanRun(proposal.Steps, skills))
        {
            return RejectSequence;
        }

        return null;
    }

    /// <summary>
    /// A later buy or craft may depend on gold or goods earned earlier. That is allowed.
    /// A step that can never run (unknown skill, Decide) is already rejected above.
    /// </summary>
    public static bool SequenceCanRun(IReadOnlyList<ModelPlanStep> steps, HashSet<string> skills)
    {
        if (steps == null || steps.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < steps.Count; i++)
        {
            if (!skills.Contains(steps[i].SkillKind))
            {
                return false;
            }
        }

        return true;
    }

    public static HashSet<string> ToSet(IReadOnlyList<string> values)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (values == null)
        {
            return set;
        }

        for (var i = 0; i < values.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                set.Add(values[i]);
            }
        }

        return set;
    }
}
