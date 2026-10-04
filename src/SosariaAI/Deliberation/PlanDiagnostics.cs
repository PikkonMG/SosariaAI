using System;
using SosariaAI.Behaviour;

namespace SosariaAI.Deliberation;

public enum PlanRequestState
{
    Idle,
    InFlight,
    Cooldown,
    Backoff,
    Paused
}

public sealed class PlanDiagnostics
{
    public int ChatCalls { get; set; }

    public int PlanningCalls { get; set; }

    public int AcceptedPlans { get; set; }

    public int RejectedPlans { get; set; }

    public int StaleReplies { get; set; }

    public int CompletedGoals { get; set; }

    public int FailedGoals { get; set; }

    public int FallbackUse { get; set; }

    public string Controller { get; set; } = PlanControl.ControllerFallback;

    public string Goal { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string PlanId { get; set; } = string.Empty;

    public int Revision { get; set; }

    public int StepIndex { get; set; }

    public string StepSkill { get; set; } = string.Empty;

    public PlanTrigger LastTrigger { get; set; }

    public PlanRequestState RequestState { get; set; }

    public string LastAccepted { get; set; } = string.Empty;

    public string LastRejected { get; set; } = string.Empty;

    public string FallbackReason { get; set; } = string.Empty;

    public string LastStepResult { get; set; } = string.Empty;

    public string NextCall { get; set; } = "no-trigger";

    public void NoteChat() => ChatCalls++;

    public void NotePlanning() => PlanningCalls++;

    public void NoteAccepted(ModelPlan plan)
    {
        AcceptedPlans++;
        Controller = PlanControl.ControllerModel;
        ApplyPlan(plan);
        LastAccepted = Describe(plan);
        FallbackReason = string.Empty;
    }

    public void NoteRejected(string why, bool stale)
    {
        RejectedPlans++;

        if (stale)
        {
            StaleReplies++;
        }

        LastRejected = why;
    }

    public void NoteFallback(string why)
    {
        FallbackUse++;
        Controller = PlanControl.ControllerFallback;
        FallbackReason = why;
    }

    public void NoteObserve(ObserveResult result)
    {
        if (result == null)
        {
            return;
        }

        LastStepResult = result.Kind + ":" + result.Detail;
        ApplyPlan(result.Plan);

        if (result.Kind == StepResultKind.GoalCompleted)
        {
            CompletedGoals++;
        }

        if (result.Plan?.State == PlanState.Failed)
        {
            FailedGoals++;
        }
    }

    public void ApplyPlan(ModelPlan plan)
    {
        if (plan == null)
        {
            PlanId = string.Empty;
            Revision = 0;
            StepIndex = 0;
            StepSkill = string.Empty;
            Goal = string.Empty;
            Reason = string.Empty;
            Controller = PlanControl.ControllerFallback;
            return;
        }

        PlanId = plan.Id;
        Revision = plan.Revision;
        StepIndex = plan.Index;
        StepSkill = plan.CurrentSkill ?? string.Empty;
        Goal = plan.Goal;
        Reason = plan.Reason;
        LastTrigger = plan.Trigger;
        Controller = plan.OwnsWork ? PlanControl.ControllerModel : PlanControl.ControllerFallback;
    }

    public string[] WatchLines()
    {
        return
        [
            "Control: " + Controller,
            "Goal: " + (string.IsNullOrWhiteSpace(Goal) ? "none" : Goal) +
            (string.IsNullOrWhiteSpace(Reason) ? string.Empty : " because " + Reason),
            "Plan: " + (string.IsNullOrWhiteSpace(PlanId) ? "none" : PlanId) +
            " rev " + Revision + " step " + StepIndex + " " + StepSkill,
            "Trigger: " + LastTrigger,
            "Request: " + RequestState,
            "Last accepted: " + EmptyAsNone(LastAccepted),
            "Last rejected: " + EmptyAsNone(LastRejected),
            "Fallback: " + EmptyAsNone(FallbackReason),
            "Last step: " + EmptyAsNone(LastStepResult),
            "Next plan call: " + NextCall,
            "Counts: chat " + ChatCalls +
            " plan " + PlanningCalls +
            " accepted " + AcceptedPlans +
            " rejected " + RejectedPlans +
            " stale " + StaleReplies +
            " completed " + CompletedGoals +
            " failed " + FailedGoals +
            " fallback " + FallbackUse
        ];
    }

    private static string Describe(ModelPlan plan)
    {
        if (plan == null)
        {
            return string.Empty;
        }

        return plan.Id + " " + plan.Goal;
    }

    private static string EmptyAsNone(string value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value;
}
