using System;
using SosariaAI.Behaviour;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class PlanRequestRulesTests
{
    private static readonly DateTime Noon = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ShouldAsk_NoPlanWithoutAPlayer_IsFalse()
    {
        Assert.False(
            PlanRequestRules.ShouldAsk(
                hasOwningPlan: false,
                inFlight: false,
                PlanTrigger.NoPlan,
                Noon,
                lastAsked: default,
                PlanRequestRules.DefaultCooldown,
                backoffUntil: default,
                talkingToPlayer: false,
                playerNearby: false
            )
        );
        Assert.Equal(
            "no-audience",
            PlanRequestRules.DelayReason(
                hasOwningPlan: false,
                inFlight: false,
                PlanTrigger.NoPlan,
                Noon,
                lastAsked: default,
                PlanRequestRules.DefaultCooldown,
                backoffUntil: default,
                talkingToPlayer: false,
                crowdBlocked: false,
                budgetBlocked: false,
                paused: false,
                playerNearby: false
            )
        );
    }

    [Fact]
    public void ShouldAsk_NoPlanWithNearbyPlayer_IsTrue()
    {
        Assert.True(
            PlanRequestRules.ShouldAsk(
                hasOwningPlan: false,
                inFlight: false,
                PlanTrigger.NoPlan,
                Noon,
                lastAsked: default,
                PlanRequestRules.DefaultCooldown,
                backoffUntil: default,
                talkingToPlayer: false,
                playerNearby: true
            )
        );
    }

    [Fact]
    public void ShouldAsk_GoalCompleteWithNearbyPlayer_IsTrue()
    {
        Assert.True(
            PlanRequestRules.ShouldAsk(
                hasOwningPlan: false,
                inFlight: false,
                PlanTrigger.GoalComplete,
                Noon,
                lastAsked: default,
                PlanRequestRules.DefaultCooldown,
                backoffUntil: default,
                talkingToPlayer: false,
                playerNearby: true
            )
        );
    }

    [Fact]
    public void ShouldAsk_StepFailedWithNearbyPlayerInsideCooldown_IsFalse()
    {
        Assert.False(
            PlanRequestRules.ShouldAsk(
                hasOwningPlan: false,
                inFlight: false,
                PlanTrigger.StepFailed,
                Noon,
                lastAsked: Noon - PlanRequestRules.DefaultCooldown / 2,
                PlanRequestRules.DefaultCooldown,
                backoffUntil: default,
                talkingToPlayer: false,
                playerNearby: true
            )
        );
    }

    [Fact]
    public void DelayReason_GoalCompleteWithNoPlayerNear_IsNoAudience()
    {
        Assert.Equal(
            "no-audience",
            PlanRequestRules.DelayReason(
                hasOwningPlan: false,
                inFlight: false,
                PlanTrigger.GoalComplete,
                Noon,
                lastAsked: default,
                PlanRequestRules.DefaultCooldown,
                backoffUntil: default,
                talkingToPlayer: false,
                crowdBlocked: false,
                budgetBlocked: false,
                paused: false,
                playerNearby: false
            )
        );
    }

    [Fact]
    public void ShouldAsk_NoPlanWhileTalkingToPlayer_IsFalse()
    {
        Assert.False(
            PlanRequestRules.ShouldAsk(
                hasOwningPlan: false,
                inFlight: false,
                PlanTrigger.NoPlan,
                Noon,
                lastAsked: default,
                PlanRequestRules.DefaultCooldown,
                backoffUntil: default,
                talkingToPlayer: true,
                playerNearby: true
            )
        );
    }

    [Fact]
    public void ShouldAsk_RealPlayerRequest_IsTrue()
    {
        Assert.True(
            PlanRequestRules.ShouldAsk(
                hasOwningPlan: false,
                inFlight: false,
                PlanTrigger.PlayerRequest,
                Noon,
                lastAsked: default,
                PlanRequestRules.DefaultCooldown,
                backoffUntil: default,
                talkingToPlayer: true,
                playerNearby: true
            )
        );
    }

    [Fact]
    public void ShouldAsk_DiedWithoutAPlayer_IsTrue()
    {
        Assert.True(
            PlanRequestRules.ShouldAsk(
                hasOwningPlan: false,
                inFlight: false,
                PlanTrigger.Died,
                Noon,
                lastAsked: default,
                PlanRequestRules.DefaultCooldown,
                backoffUntil: default,
                talkingToPlayer: false,
                playerNearby: false
            )
        );
    }

    [Fact]
    public void NeedsAudience_LowValueTriggersOnly()
    {
        Assert.True(PlanRequestRules.NeedsAudience(PlanTrigger.NoPlan));
        Assert.True(PlanRequestRules.NeedsAudience(PlanTrigger.StepFailed));
        Assert.True(PlanRequestRules.NeedsAudience(PlanTrigger.Retry));
        Assert.False(PlanRequestRules.NeedsAudience(PlanTrigger.Died));
        Assert.False(PlanRequestRules.NeedsAudience(PlanTrigger.Invitation));
        Assert.False(PlanRequestRules.NeedsAudience(PlanTrigger.PlayerRequest));
    }
}
