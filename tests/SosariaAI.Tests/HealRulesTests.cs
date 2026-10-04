using System;
using Server;
using SosariaAI.Combat;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>Binding wounds between fights: who needs it, how far a bandage reaches, when to give up.</summary>
public class HealRulesTests
{
    private const int FullHits = 100;
    private const int ScratchedHits = 90;
    private const int HurtHits = 50;
    private const int NoPool = 0;
    private static readonly DateTime Start = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Reach_IsOneTile_PreAos()
    {
        var at = new Point3D(1425, 1695, 0);
        Assert.True(HealRules.InReach(at, at));
        Assert.True(HealRules.InReach(at, new Point3D(at.X + HealRules.ReachTiles, at.Y + HealRules.ReachTiles, at.Z)));
        Assert.False(HealRules.InReach(at, new Point3D(at.X + HealRules.ReachTiles + 1, at.Y, at.Z)));
    }

    [Fact]
    public void NeedsHeal_BelowTheRestLineOrPoisoned()
    {
        Assert.True(HealRules.NeedsHeal(HurtHits, FullHits, poisoned: false));
        Assert.False(HealRules.NeedsHeal(ScratchedHits, FullHits, poisoned: false));
        Assert.False(HealRules.NeedsHeal(FullHits, FullHits, poisoned: false));
        Assert.True(HealRules.NeedsHeal(FullHits, FullHits, poisoned: true));
        Assert.False(HealRules.NeedsHeal(NoPool, NoPool, poisoned: false));
    }

    [Fact]
    public void MayTend_ABlueTendsNoOutlaw_ARedTendsItsOwn()
    {
        Assert.True(HealRules.MayTend(healerIsRed: false, patientIsOutlaw: false));
        Assert.False(HealRules.MayTend(healerIsRed: false, patientIsOutlaw: true));
        Assert.True(HealRules.MayTend(healerIsRed: true, patientIsOutlaw: true));
    }

    [Fact]
    public void MayBegin_Null_IsFalse() =>
        Assert.False(HealRules.MayBegin(null));

    [Fact]
    public void TimedOut_AfterTheTendLimit()
    {
        Assert.False(HealRules.TimedOut(Start, Start));
        Assert.True(HealRules.TimedOut(Start + HealRules.TendLimit, Start));
        Assert.False(HealRules.TimedOut(Start, default));
    }

    [Fact]
    public void Outcome_DoneAboveTheRecoveryLine()
    {
        Assert.Equal(SkillStatus.Done, HealRules.Outcome(RecoveryRules.RecoverBelowHitsFraction));
        Assert.Equal(SkillStatus.Failed, HealRules.Outcome(RecoveryRules.RecoverBelowHitsFraction / 2));
    }
}
