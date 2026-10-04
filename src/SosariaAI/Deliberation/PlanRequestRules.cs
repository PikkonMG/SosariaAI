using System;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;

namespace SosariaAI.Deliberation;

/// <summary>
/// When a planning call may go out. No idle poll. A timer may only wake a retry already scheduled.
/// </summary>
public static class PlanRequestRules
{
    public const int MinGapMilliseconds = 250;
    public const int FirstBackoffSeconds = 30;
    public const int BackoffFactor = 2;
    public const int MaxBackoffSeconds = 480;

    public static readonly TimeSpan DefaultCooldown =
        TimeSpan.FromMinutes(BrainConfiguration.DefaultDecideCooldownMinutes);

    /// <summary>
    /// Ordinary turns in a life go to the paid chat model's planner only when a player can see
    /// the character: within earshot or in talk. A chat call for every one of hundreds of
    /// people would burn the budget. Jev next-job picks do not pass through here: they are
    /// cheap, bounded by the hourly token cap, and go out wherever the character is.
    /// </summary>
    public static bool NeedsAudience(PlanTrigger trigger) =>
        trigger is PlanTrigger.NoPlan or PlanTrigger.Retry or PlanTrigger.StepFailed
            or PlanTrigger.GoalComplete or PlanTrigger.WorldBlocked;

    public static bool ShouldAsk(
        bool hasOwningPlan,
        bool inFlight,
        PlanTrigger trigger,
        DateTime now,
        DateTime lastAsked,
        TimeSpan cooldown,
        DateTime backoffUntil,
        bool talkingToPlayer,
        bool playerNearby = false
    )
    {
        if (trigger == PlanTrigger.None)
        {
            return false;
        }

        if (NeedsAudience(trigger) && !talkingToPlayer && !playerNearby)
        {
            return false;
        }

        if (inFlight)
        {
            return false;
        }

        if (talkingToPlayer && trigger is not PlanTrigger.Died and not PlanTrigger.Invitation
            and not PlanTrigger.PlayerRequest)
        {
            return false;
        }

        if (hasOwningPlan && trigger is PlanTrigger.NoPlan or PlanTrigger.Retry)
        {
            return false;
        }

        if (backoffUntil != default && now < backoffUntil &&
            trigger is not PlanTrigger.Died and not PlanTrigger.Invitation)
        {
            return false;
        }

        if (NeedsAudience(trigger))
        {
            return CooledDown(now, lastAsked, cooldown);
        }

        return true;
    }

    private static bool CooledDown(DateTime now, DateTime lastAsked, TimeSpan cooldown)
    {
        var wait = cooldown <= TimeSpan.Zero ? DefaultCooldown : cooldown;
        return lastAsked == default || now - lastAsked >= wait;
    }

    public static TimeSpan Backoff(int consecutiveFailures)
    {
        if (consecutiveFailures <= 0)
        {
            return TimeSpan.Zero;
        }

        var seconds = FirstBackoffSeconds;

        for (var i = 1; i < consecutiveFailures; i++)
        {
            if (seconds > MaxBackoffSeconds / BackoffFactor)
            {
                seconds = MaxBackoffSeconds;
                break;
            }

            seconds *= BackoffFactor;
        }

        if (seconds > MaxBackoffSeconds)
        {
            seconds = MaxBackoffSeconds;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    public static bool CrowdAllows(DateTime lastGlobalEnqueue, DateTime now)
    {
        if (lastGlobalEnqueue == default)
        {
            return true;
        }

        return now - lastGlobalEnqueue >= TimeSpan.FromMilliseconds(MinGapMilliseconds);
    }

    public static PlanTrigger Merge(PlanTrigger existing, PlanTrigger incoming)
    {
        if (incoming == PlanTrigger.None)
        {
            return existing;
        }

        if (existing == PlanTrigger.None)
        {
            return incoming;
        }

        return Rank(incoming) >= Rank(existing) ? incoming : existing;
    }

    public static string DelayReason(
        bool hasOwningPlan,
        bool inFlight,
        PlanTrigger trigger,
        DateTime now,
        DateTime lastAsked,
        TimeSpan cooldown,
        DateTime backoffUntil,
        bool talkingToPlayer,
        bool crowdBlocked,
        bool budgetBlocked,
        bool paused,
        bool playerNearby = false
    )
    {
        if (paused)
        {
            return "provider-paused";
        }

        if (NeedsAudience(trigger) && !talkingToPlayer && !playerNearby)
        {
            return "no-audience";
        }

        if (budgetBlocked)
        {
            return "budget";
        }

        if (inFlight)
        {
            return "in-flight";
        }

        if (talkingToPlayer && trigger is not PlanTrigger.Died and not PlanTrigger.Invitation
            and not PlanTrigger.PlayerRequest)
        {
            return "talking";
        }

        if (hasOwningPlan && trigger is PlanTrigger.NoPlan or PlanTrigger.Retry)
        {
            return "plan-owns-work";
        }

        if (backoffUntil != default && now < backoffUntil &&
            trigger is not PlanTrigger.Died and not PlanTrigger.Invitation)
        {
            return "backoff";
        }

        if (NeedsAudience(trigger) && !CooledDown(now, lastAsked, cooldown))
        {
            return "cooldown";
        }

        if (crowdBlocked)
        {
            return "crowd";
        }

        if (trigger == PlanTrigger.None)
        {
            return "no-trigger";
        }

        return "allowed";
    }

    private static int Rank(PlanTrigger trigger) =>
        trigger switch
        {
            PlanTrigger.Died => 80,
            PlanTrigger.Invitation => 70,
            PlanTrigger.PlayerRequest => 70,
            PlanTrigger.StepFailed => 60,
            PlanTrigger.GoalComplete => 50,
            PlanTrigger.WorldBlocked => 40,
            PlanTrigger.NoPlan => 20,
            PlanTrigger.Retry => 10,
            _ => 0
        };
}
