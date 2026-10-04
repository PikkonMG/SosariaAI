using Server.Items;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class RemoveTrapRulesTests
{
    private const string ExpectedKind = "RemoveTrap";
    private const int ExpectedReachTiles = 2;
    private const double ExpectedTrapPowerWindow = 30;
    private const int EasyTrapPower = 25;
    private const int ExpectedClearedTrapPower = 0;
    private const int ExpectedClearedTrapLevel = 0;

    [Fact]
    public void Kind_IsRemoveTrap()
    {
        Assert.Equal(ExpectedKind, RemoveTrapRules.Kind);
        Assert.Equal(ExpectedKind, new RemoveTrapSkill().Name);
    }

    [Fact]
    public void Reach_IsTwoTiles() =>
        Assert.Equal(ExpectedReachTiles, RemoveTrapRules.ReachTiles);

    [Fact]
    public void SkillWindow_IsTheEngineCheck_TheTrapPowerToThirtyAbove()
    {
        // Engine RemoveTrap: CheckTargetSkill(RemoveTrap, chest, TrapPower, TrapPower + 30).
        var (min, max) = RemoveTrapRules.SkillWindow(EasyTrapPower);
        Assert.Equal(ExpectedTrapPowerWindow, RemoveTrapRules.TrapPowerWindow);
        Assert.Equal(EasyTrapPower, min);
        Assert.Equal(EasyTrapPower + ExpectedTrapPowerWindow, max);
    }

    [Fact]
    public void HasWork_NoneOutOfTheWorld() => Assert.False(RemoveTrapSkill.HasWork(null));

    [Fact]
    public void IsTrapped_UsesTrapType()
    {
        Assert.False(RemoveTrapRules.IsTrapped(TrapType.None));
        Assert.True(RemoveTrapRules.IsTrapped(TrapType.MagicTrap));
        Assert.True(RemoveTrapRules.IsTrapped(TrapType.ExplosionTrap));
        Assert.True(RemoveTrapRules.IsTrapped(TrapType.DartTrap));
        Assert.True(RemoveTrapRules.IsTrapped(TrapType.PoisonTrap));
    }

    [Fact]
    public void TrapCleared_MatchesModernUODisarm()
    {
        Assert.Equal(ExpectedClearedTrapPower, RemoveTrapRules.ClearedTrapPower);
        Assert.Equal(ExpectedClearedTrapLevel, RemoveTrapRules.ClearedTrapLevel);
    }
}
