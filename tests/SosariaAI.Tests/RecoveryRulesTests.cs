using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class RecoveryRulesTests
{
    private const double EmptyHitsFraction = 0;
    private const double FullHitsFraction = 1;
    private const double HurtHitsFraction = RecoveryRules.RecoverBelowHitsFraction / 2;
    private const double RecoveringHitsFraction =
        (RecoveryRules.RecoverBelowHitsFraction + RecoveryRules.FitHitsFraction) / 2;

    [Fact]
    public void NeedsRecovery_InCombat_IsFalse()
    {
        Assert.False(RecoveryRules.NeedsRecovery(EmptyHitsFraction, true));
        Assert.False(RecoveryRules.NeedsRecovery(HurtHitsFraction, true));
        Assert.False(RecoveryRules.NeedsRecovery(RecoveryRules.RecoverBelowHitsFraction, true));
        Assert.False(RecoveryRules.NeedsRecovery(FullHitsFraction, true));
    }

    [Fact]
    public void NeedsRecovery_BelowThresholdOutOfCombat_IsTrue()
    {
        Assert.True(RecoveryRules.NeedsRecovery(EmptyHitsFraction, false));
        Assert.True(RecoveryRules.NeedsRecovery(HurtHitsFraction, false));
    }

    [Fact]
    public void NeedsRecovery_AtOrAboveThreshold_IsFalse()
    {
        Assert.False(RecoveryRules.NeedsRecovery(RecoveryRules.RecoverBelowHitsFraction, false));
        Assert.False(RecoveryRules.NeedsRecovery(RecoveringHitsFraction, false));
        Assert.False(RecoveryRules.NeedsRecovery(RecoveryRules.FitHitsFraction, false));
        Assert.False(RecoveryRules.NeedsRecovery(FullHitsFraction, false));
    }

    [Fact]
    public void IsFit_AtOrAboveThreshold_IsTrue()
    {
        Assert.True(RecoveryRules.IsFit(RecoveryRules.FitHitsFraction));
        Assert.True(RecoveryRules.IsFit(FullHitsFraction));
    }

    [Fact]
    public void IsFit_BelowThreshold_IsFalse()
    {
        Assert.False(RecoveryRules.IsFit(EmptyHitsFraction));
        Assert.False(RecoveryRules.IsFit(HurtHitsFraction));
        Assert.False(RecoveryRules.IsFit(RecoveryRules.RecoverBelowHitsFraction));
        Assert.False(RecoveryRules.IsFit(RecoveringHitsFraction));
    }

    [Fact]
    public void Recovery_UsesHysteresis_SoAcharacterDoesNotBounce()
    {
        // Drops out below the recovery line, and is only fit again well above it.
        Assert.True(RecoveryRules.NeedsRecovery(0.59, inCombat: false));
        Assert.False(RecoveryRules.IsFit(0.61));
        Assert.False(RecoveryRules.IsFit(0.94));
        Assert.True(RecoveryRules.IsFit(0.95));
    }
}
