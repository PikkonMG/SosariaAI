using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;

namespace SosariaAI.Deliberation;

public sealed record PlanAcceptContext(
    string CharacterId,
    int CurrentRevision,
    bool Alive,
    bool OffWorld,
    DateTime Now,
    IReadOnlyList<string> AvailableSkills,
    IReadOnlyList<string> Destinations,
    IReadOnlyList<string> People,
    IReadOnlyList<string> Items,
    Expansion Expansion
)
{
    /// <summary>A party agreement changed after the plan was asked for; set by <see cref="PlanningPath"/>.</summary>
    public bool AgreementChanged { get; init; }
}

public sealed class PlanWork
{
    public bool FromModel { get; init; }

    public string SkillKind { get; init; }

    public string TargetRef { get; init; }

    public string Why { get; init; }

    public ActionCandidate Candidate { get; init; }

    public string FallbackReason { get; init; }
}

public sealed class AcceptResult
{
    public bool Accepted { get; init; }

    public bool Stale { get; init; }

    public ModelPlan Plan { get; init; }

    public string Rejection { get; init; }
}

public sealed class ObserveResult
{
    public ModelPlan Plan { get; init; }

    public bool RequestRevision { get; init; }

    public StepResultKind Kind { get; init; }

    public string Detail { get; init; }
}

/// <summary>
/// One controller for model plans. Scoring stays the fallback when no valid plan owns work.
/// </summary>
public static class PlanControl
{
    public const string ControllerModel = "model-plan";
    public const string ControllerFallback = "fallback";
    public const string FallbackNoPlan = "no-model-plan";
    public const string FallbackExpired = "plan-expired";
    public const string FallbackUrgent = "urgent";
    public const string FallbackUnavailable = "step-unavailable";
    public const string FallbackBlocked = "plan-blocked";

    public static bool OwnsOrdinaryWork(ModelPlan plan, DateTime now) =>
        plan is { OwnsWork: true } && !IsExpired(plan, now);

    public static bool IsExpired(ModelPlan plan, DateTime now) =>
        plan != null && plan.Expires != default && now >= plan.Expires;

    public static bool IsValidAfterLoad(ModelPlan plan, DateTime now, string characterId)
    {
        if (plan == null || string.IsNullOrWhiteSpace(plan.Id) || plan.Steps == null || plan.Steps.Count == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(characterId) &&
            !string.IsNullOrWhiteSpace(plan.CharacterId) &&
            !plan.CharacterId.Equals(characterId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsExpired(plan, now) || plan.State is PlanState.Complete or PlanState.Failed)
        {
            return false;
        }

        if (plan.Index < 0 || plan.Index >= plan.Steps.Count)
        {
            return false;
        }

        return true;
    }

    public static PlanWork SelectWork(
        ModelPlan plan,
        IReadOnlyList<ActionCandidate> catalog,
        Situation situation,
        DateTime now
    )
    {
        if (situation is { MustFlee: true } or { IsGhost: true } or { InCombat: true })
        {
            return new PlanWork { FromModel = false, FallbackReason = FallbackUrgent };
        }

        if (!OwnsOrdinaryWork(plan, now))
        {
            var reason = plan == null ? FallbackNoPlan :
                IsExpired(plan, now) ? FallbackExpired :
                plan.State == PlanState.Blocked ? FallbackBlocked :
                FallbackNoPlan;
            return new PlanWork { FromModel = false, FallbackReason = reason };
        }

        var step = plan.Current.Value;
        var candidate = Bind(catalog, step);

        if (candidate == null)
        {
            return new PlanWork
            {
                FromModel = true,
                SkillKind = step.SkillKind,
                TargetRef = step.TargetRef,
                Why = step.Why,
                FallbackReason = FallbackUnavailable
            };
        }

        return new PlanWork
        {
            FromModel = true,
            SkillKind = step.SkillKind,
            TargetRef = step.TargetRef,
            Why = step.Why,
            Candidate = candidate
        };
    }

    public static bool ScorerMayReplace(ModelPlan plan, DateTime now, Situation situation)
    {
        if (situation is { MustFlee: true } or { IsGhost: true } or { InCombat: true })
        {
            return true;
        }

        return !OwnsOrdinaryWork(plan, now);
    }

    public static AcceptResult Accept(ModelPlan current, PlanProposal proposal, PlanAcceptContext context)
    {
        var stale = StaleReason(current, proposal, context);

        if (stale != null)
        {
            return new AcceptResult
            {
                Accepted = false,
                Stale = stale == PlanValidator.RejectStale || stale == PlanValidator.RejectAgreement,
                Plan = current,
                Rejection = stale
            };
        }

        var invalid = PlanValidator.Reject(
            proposal,
            context.AvailableSkills,
            context.Destinations,
            context.People,
            context.Items,
            context.Expansion
        );

        if (invalid != null)
        {
            return new AcceptResult
            {
                Accepted = false,
                Stale = false,
                Plan = current,
                Rejection = invalid
            };
        }

        var created = context.Now;
        var revision = (current?.Revision ?? 0) + 1;
        var plan = new ModelPlan
        {
            Id = ModelPlan.NewId(proposal.CharacterId, created),
            Revision = revision,
            CharacterId = proposal.CharacterId,
            Goal = ModelPlan.ClampText(proposal.Goal),
            Reason = ModelPlan.ClampText(proposal.Reason),
            Success = ModelPlan.ClampText(proposal.Success),
            Steps = ClampSteps(proposal.Steps),
            Index = 0,
            State = PlanState.Waiting,
            FailureReason = string.Empty,
            StepFailures = 0,
            Expires = created + ModelPlan.DefaultLife,
            Results = [],
            Trigger = current?.Trigger ?? PlanTrigger.NoPlan
        };

        return new AcceptResult { Accepted = true, Stale = false, Plan = plan };
    }

    public static ObserveResult Observe(ModelPlan plan, StepObservation observation, DateTime now)
    {
        if (plan == null || !plan.OwnsWork)
        {
            return new ObserveResult
            {
                Plan = plan,
                RequestRevision = plan == null,
                Kind = observation.Kind,
                Detail = observation.Detail
            };
        }

        var results = ModelPlan.AppendResult(
            plan.Results,
            new PlanResultRecord(observation.SkillKind, observation.Kind, observation.Detail)
        );

        if (observation.Kind == StepResultKind.Interrupted)
        {
            return new ObserveResult
            {
                Plan = plan.With(state: PlanState.Blocked, results: results, failureReason: observation.Detail),
                RequestRevision = observation.Detail == StepProof.DetailDead,
                Kind = observation.Kind,
                Detail = observation.Detail
            };
        }

        if (observation.Kind == StepResultKind.ActionUnavailable)
        {
            return new ObserveResult
            {
                Plan = plan.With(state: PlanState.Failed, results: results, failureReason: observation.Detail),
                RequestRevision = true,
                Kind = observation.Kind,
                Detail = observation.Detail
            };
        }

        if (observation.Kind is StepResultKind.AttemptFailed or StepResultKind.NoUsefulProgress)
        {
            var failures = plan.StepFailures + 1;

            if (failures >= RepeatFailure.Limit)
            {
                return new ObserveResult
                {
                    Plan = plan.With(
                        state: PlanState.Failed,
                        stepFailures: failures,
                        results: results,
                        failureReason: observation.Detail
                    ),
                    RequestRevision = true,
                    Kind = observation.Kind,
                    Detail = observation.Detail
                };
            }

            return new ObserveResult
            {
                Plan = plan.With(stepFailures: failures, results: results, state: PlanState.Waiting),
                RequestRevision = false,
                Kind = observation.Kind,
                Detail = observation.Detail
            };
        }

        var nextIndex = plan.Index + 1;
        var facts = observation.After;
        var goalMet = string.IsNullOrWhiteSpace(plan.Success) && nextIndex >= plan.Steps.Count ||
                      StepProof.MeetsSuccess(plan.Success, facts);

        if (goalMet)
        {
            return new ObserveResult
            {
                Plan = plan.With(index: nextIndex, state: PlanState.Complete, stepFailures: 0, results: results),
                RequestRevision = true,
                Kind = StepResultKind.GoalCompleted,
                Detail = string.IsNullOrWhiteSpace(plan.Success) ? observation.Detail : plan.Success
            };
        }

        if (nextIndex >= plan.Steps.Count)
        {
            return new ObserveResult
            {
                Plan = plan.With(
                    index: nextIndex,
                    state: PlanState.Failed,
                    stepFailures: 0,
                    results: results,
                    failureReason: "success-unmet"
                ),
                RequestRevision = true,
                Kind = StepResultKind.AttemptFailed,
                Detail = "success-unmet"
            };
        }

        return new ObserveResult
        {
            Plan = plan.With(index: nextIndex, state: PlanState.Waiting, stepFailures: 0, results: results),
            RequestRevision = false,
            Kind = StepResultKind.StepCompleted,
            Detail = observation.Detail
        };
    }

    public static ModelPlan Resume(ModelPlan plan)
    {
        if (plan is not { State: PlanState.Blocked } || plan.Steps == null || plan.Index >= plan.Steps.Count)
        {
            return plan;
        }

        return plan.With(state: PlanState.Waiting, failureReason: string.Empty);
    }

    public static ModelPlan MarkRunning(ModelPlan plan) =>
        plan is { OwnsWork: true } ? plan.With(state: PlanState.Running) : plan;

    public static ModelPlan AfterLoad(ModelPlan plan, DateTime now, string characterId)
    {
        if (!IsValidAfterLoad(plan, now, characterId))
        {
            return null;
        }

        if (plan.State == PlanState.Running)
        {
            return plan.With(state: PlanState.Waiting);
        }

        return plan;
    }

    public static ActionCandidate Bind(IReadOnlyList<ActionCandidate> catalog, ModelPlanStep step)
    {
        if (catalog == null || string.IsNullOrWhiteSpace(step.SkillKind))
        {
            return null;
        }

        ActionCandidate found = null;
        var dest = PlanRefs.DestinationOf(step.TargetRef) ??
                   (PlanRefs.PersonOf(step.TargetRef) == null && PlanRefs.ItemOf(step.TargetRef) == null
                       ? PlanRefs.Normalize(step.TargetRef)
                       : null);
        var person = PlanRefs.PersonOf(step.TargetRef);

        for (var i = 0; i < catalog.Count; i++)
        {
            var candidate = catalog[i];

            if (!step.SkillKind.Equals(candidate.SkillKind, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            found ??= candidate;

            if (!string.IsNullOrWhiteSpace(dest) &&
                dest.Equals(candidate.Step?.Destination, StringComparison.OrdinalIgnoreCase))
            {
                found = candidate;
                break;
            }
        }

        if (found == null)
        {
            return null;
        }

        var definition = found.Step ?? new SkillStepDefinition { Skill = step.SkillKind };

        if (!string.IsNullOrWhiteSpace(dest) || !string.IsNullOrWhiteSpace(person))
        {
            definition = new SkillStepDefinition
            {
                Skill = step.SkillKind,
                Destination = string.IsNullOrWhiteSpace(dest) ? definition.Destination : dest,
                LeaderId = string.IsNullOrWhiteSpace(person) ? definition.LeaderId : person,
                Party = string.IsNullOrWhiteSpace(person) ? definition.Party : person,
                Target = definition.Target,
                Range = definition.Range,
                Area = definition.Area,
                FillFraction = definition.FillFraction,
                BankSpot = definition.BankSpot,
                Center = definition.Center,
                Radius = definition.Radius,
                Duration = definition.Duration,
                Points = definition.Points,
                Minutes = definition.Minutes,
                StopBelowHitsFraction = definition.StopBelowHitsFraction,
                Dungeon = definition.Dungeon
            };
        }

        return new ActionCandidate
        {
            Id = found.Id,
            SkillKind = found.SkillKind,
            RoutineId = found.RoutineId,
            Step = definition,
            RequiredPower = found.RequiredPower
        };
    }

    public static string StaleReason(ModelPlan current, PlanProposal proposal, PlanAcceptContext context)
    {
        if (context == null || proposal == null)
        {
            return PlanValidator.RejectSchema;
        }

        if (!context.Alive)
        {
            return PlanValidator.RejectDead;
        }

        if (context.OffWorld)
        {
            return PlanValidator.RejectOffWorld;
        }

        if (!string.IsNullOrWhiteSpace(context.CharacterId) &&
            !string.IsNullOrWhiteSpace(proposal.CharacterId) &&
            !context.CharacterId.Equals(proposal.CharacterId, StringComparison.OrdinalIgnoreCase))
        {
            return PlanValidator.RejectIdentity;
        }

        if (context.AgreementChanged)
        {
            return PlanValidator.RejectAgreement;
        }

        if (proposal.ExpectedRevision != context.CurrentRevision)
        {
            return PlanValidator.RejectStale;
        }

        if (current is { Revision: var revision } && revision > proposal.ExpectedRevision)
        {
            return PlanValidator.RejectStale;
        }

        return null;
    }

    private static IReadOnlyList<ModelPlanStep> ClampSteps(IReadOnlyList<ModelPlanStep> steps)
    {
        var list = new List<ModelPlanStep>();

        if (steps == null)
        {
            return list;
        }

        var count = Math.Min(steps.Count, ModelPlan.MaxSteps);

        for (var i = 0; i < count; i++)
        {
            list.Add(new ModelPlanStep(
                steps[i].SkillKind,
                ModelPlan.ClampText(steps[i].Why),
                PlanRefs.Normalize(steps[i].TargetRef)
            ));
        }

        return list;
    }
}
