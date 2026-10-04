using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;

namespace SosariaAI.Deliberation;

/// <summary>
/// One character's planning session. HTTP is outside. Scoring is the fallback.
/// </summary>
public sealed class PlanningPath
{
    public const string CallKind = BrainProviders.DecisionKind;

    public ModelPlan Plan { get; private set; }

    public PlanDiagnostics Diag { get; } = new();

    public bool InFlight { get; private set; }

    public DateTime LastAsked { get; private set; }

    public DateTime BackoffUntil { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    public int AgreementGeneration { get; private set; }

    public int PendingRevision { get; private set; }

    public PlanTrigger PendingTrigger { get; private set; }

    public long PendingRequestId { get; private set; }

    public int PendingAgreement { get; private set; }

    public PlanTrigger MergedTrigger { get; private set; }

    public void Restore(ModelPlan plan, DateTime now, string characterId)
    {
        Plan = PlanControl.AfterLoad(plan, now, characterId);
        InFlight = false;
        PendingRequestId = 0;
        PendingTrigger = PlanTrigger.None;
        MergedTrigger = PlanTrigger.None;
        Diag.ApplyPlan(Plan);
        Diag.RequestState = PlanRequestState.Idle;
    }

    public void NoteAgreement() => AgreementGeneration++;

    public bool ShouldAsk(
        PlanTrigger trigger,
        DateTime now,
        TimeSpan cooldown,
        bool talkingToPlayer,
        DateTime lastGlobalEnqueue,
        bool paused,
        bool budgetOk,
        bool playerNearby = false
    )
    {
        MergedTrigger = PlanRequestRules.Merge(MergedTrigger, trigger);
        var ask = PlanRequestRules.ShouldAsk(
            PlanControl.OwnsOrdinaryWork(Plan, now),
            InFlight,
            MergedTrigger,
            now,
            LastAsked,
            cooldown,
            BackoffUntil,
            talkingToPlayer,
            playerNearby
        );
        var crowd = !PlanRequestRules.CrowdAllows(lastGlobalEnqueue, now);
        Diag.LastTrigger = MergedTrigger;
        Diag.NextCall = PlanRequestRules.DelayReason(
            PlanControl.OwnsOrdinaryWork(Plan, now),
            InFlight,
            MergedTrigger,
            now,
            LastAsked,
            cooldown,
            BackoffUntil,
            talkingToPlayer,
            crowd,
            !budgetOk,
            paused,
            playerNearby
        );
        Diag.RequestState = InFlight
            ? PlanRequestState.InFlight
            : now < BackoffUntil
                ? PlanRequestState.Backoff
                : Diag.NextCall == "cooldown"
                    ? PlanRequestState.Cooldown
                    : paused
                        ? PlanRequestState.Paused
                        : PlanRequestState.Idle;
        return ask && !crowd && budgetOk && !paused;
    }

    public void MarkEnqueued(long requestId, DateTime now)
    {
        InFlight = true;
        LastAsked = now;
        PendingRequestId = requestId;
        PendingRevision = Plan?.Revision ?? 0;
        PendingAgreement = AgreementGeneration;
        PendingTrigger = MergedTrigger;
        MergedTrigger = PlanTrigger.None;
        Diag.NotePlanning();
        Diag.RequestState = PlanRequestState.InFlight;
        Diag.LastTrigger = PendingTrigger;
    }

    public AcceptResult Deliver(
        string raw,
        long requestId,
        string characterId,
        PlanAcceptContext context,
        bool timedOut
    )
    {
        InFlight = false;
        Diag.RequestState = PlanRequestState.Idle;

        if (timedOut || string.IsNullOrWhiteSpace(raw))
        {
            ConsecutiveFailures++;
            BackoffUntil = context.Now + PlanRequestRules.Backoff(ConsecutiveFailures);
            Diag.NoteRejected(timedOut ? "timeout" : PlanValidator.RejectSchema, stale: false);
            Diag.NoteFallback(timedOut ? "timeout" : PlanValidator.RejectSchema);
            return new AcceptResult
            {
                Accepted = false,
                Plan = Plan,
                Rejection = timedOut ? "timeout" : PlanValidator.RejectSchema
            };
        }

        if (requestId != 0 && PendingRequestId != 0 && requestId != PendingRequestId)
        {
            Diag.NoteRejected(PlanValidator.RejectStale, stale: true);
            return new AcceptResult { Accepted = false, Stale = true, Plan = Plan, Rejection = PlanValidator.RejectStale };
        }

        var proposal = ReplyParser.ParsePlan(raw, characterId, PendingRevision, requestId);

        if (proposal == null)
        {
            ConsecutiveFailures++;
            BackoffUntil = context.Now + PlanRequestRules.Backoff(ConsecutiveFailures);
            Diag.NoteRejected(PlanValidator.RejectSchema, stale: false);
            Diag.NoteFallback(PlanValidator.RejectSchema);
            return new AcceptResult { Accepted = false, Plan = Plan, Rejection = PlanValidator.RejectSchema };
        }

        var acceptContext = context with
        {
            CurrentRevision = PendingRevision,
            AgreementChanged = AgreementGeneration != PendingAgreement
        };
        var result = PlanControl.Accept(Plan, proposal, acceptContext);

        if (!result.Accepted)
        {
            if (result.Stale)
            {
                Diag.NoteRejected(result.Rejection, stale: true);
            }
            else
            {
                ConsecutiveFailures++;
                BackoffUntil = context.Now + PlanRequestRules.Backoff(ConsecutiveFailures);
                Diag.NoteRejected(result.Rejection, stale: false);
                Diag.NoteFallback(result.Rejection);
            }

            return result;
        }

        ConsecutiveFailures = 0;
        BackoffUntil = default;
        Plan = result.Plan.With(trigger: PendingTrigger == PlanTrigger.None ? PlanTrigger.NoPlan : PendingTrigger);
        PendingTrigger = PlanTrigger.None;
        PendingRequestId = 0;
        Diag.NoteAccepted(Plan);
        return new AcceptResult { Accepted = true, Plan = Plan };
    }

    public PlanWork Next(IReadOnlyList<ActionCandidate> catalog, Situation situation, DateTime now)
    {
        var work = PlanControl.SelectWork(Plan, catalog, situation, now);

        if (!work.FromModel || work.Candidate == null)
        {
            if (!string.IsNullOrWhiteSpace(work.FallbackReason))
            {
                Diag.NoteFallback(work.FallbackReason);
            }

            return work;
        }

        Plan = PlanControl.MarkRunning(Plan);
        Diag.ApplyPlan(Plan);
        Diag.Controller = PlanControl.ControllerModel;
        return work;
    }

    public ObserveResult FinishStep(StepObservation observation, DateTime now)
    {
        var result = PlanControl.Observe(Plan, observation, now);
        Plan = result.Plan;
        Diag.NoteObserve(result);

        if (result.RequestRevision)
        {
            MergedTrigger = PlanRequestRules.Merge(
                MergedTrigger,
                result.Kind == StepResultKind.GoalCompleted ? PlanTrigger.GoalComplete :
                observation.Detail == StepProof.DetailDead ? PlanTrigger.Died :
                PlanTrigger.StepFailed
            );
        }

        return result;
    }

    public void Interrupt(string reason, DateTime now)
    {
        if (Plan == null || !Plan.OwnsWork && Plan.State != PlanState.Running)
        {
            return;
        }

        var observation = new StepObservation(
            Plan.CurrentSkill ?? string.Empty,
            StepResultKind.Interrupted,
            reason,
            default,
            default
        );
        FinishStep(observation, now);
    }

    public void ResumeIfBlocked()
    {
        if (Plan is { State: PlanState.Blocked })
        {
            Plan = PlanControl.Resume(Plan);
            Diag.ApplyPlan(Plan);
        }
    }

    public bool ScorerMayReplace(Situation situation, DateTime now) =>
        PlanControl.ScorerMayReplace(Plan, now, situation);
}
