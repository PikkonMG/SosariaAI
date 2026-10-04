using SosariaAI.Behaviour;
using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class OutlawRulesTests
{
    private const int Strong = 120;
    private const int Weak = 40;
    private const int DefaultPopulation = 200;
    private const int DefaultReds = 20;
    private const int OperatorReds = 15;
    private const int Near = 2;
    private const int Far = 12;
    private const int Lone = 1;
    private const int Pair = 2;
    private const int BigPack = 4;
    private const int NoCrowd = 0;

    [Fact]
    public void MayAttack_NeedsOddsAndAPack()
    {
        Assert.True(OutlawRules.MayAttack(DispositionKind.Outlaw, true, false, Pair, NoCrowd, false, Strong, Weak, 0.5));
        Assert.False(OutlawRules.MayAttack(DispositionKind.Lawful, true, false, Pair, NoCrowd, false, Strong, Weak, 0.5));
        Assert.False(OutlawRules.MayAttack(DispositionKind.Outlaw, false, false, Pair, NoCrowd, false, Strong, Weak, 0.5));
        Assert.False(OutlawRules.MayAttack(DispositionKind.Outlaw, true, true, Pair, NoCrowd, false, Strong, Weak, 0.5));
        Assert.False(OutlawRules.MayAttack(DispositionKind.Outlaw, true, false, Pair, NoCrowd, false, Weak, Strong, 0.5));
        Assert.False(OutlawRules.MayAttack(DispositionKind.Outlaw, true, false, Pair, NoCrowd, false, Strong, Weak, 0.1));
    }

    [Fact]
    public void MayAttack_LoneRedProwlsTheRoads_ButHoldsItsDungeonHall()
    {
        Assert.False(OutlawRules.MayAttack(DispositionKind.Outlaw, true, false, Lone, NoCrowd, false, Strong, Weak, 0.5));
        Assert.True(OutlawRules.MayAttack(DispositionKind.Outlaw, true, false, Lone, NoCrowd, true, Strong, Weak, 0.5));
    }

    [Fact]
    public void MayAttack_BidesBeforeAMob()
    {
        Assert.False(OutlawRules.MayAttack(DispositionKind.Outlaw, true, false, Pair, OutlawRules.CrowdRetreat, false, Strong, Weak, 0.5));
        Assert.True(OutlawRules.MayAttack(DispositionKind.Outlaw, true, false, Pair, OutlawRules.CrowdRetreat - 1, false, Strong, Weak, 0.5));
        Assert.False(OutlawRules.MayAttack(DispositionKind.Outlaw, true, false, BigPack, BigPack + 1, false, Strong, Weak, 0.5));
        Assert.True(OutlawRules.MayAttack(DispositionKind.Outlaw, true, false, BigPack, BigPack, false, Strong, Weak, 0.5));
    }

    [Fact]
    public void RunReason_FromStrongerSideMobOrWound()
    {
        Assert.True(Runs(Weak, Strong, 1.0, NoCrowd, Pair, 1.0));
        Assert.False(Runs(Strong, Weak, 1.0, NoCrowd, Pair, 1.0));
        Assert.True(Runs(Strong, Weak, OutlawRules.RunHitsFraction - 0.01, NoCrowd, Pair, 1.0));
        Assert.True(Runs(Strong, Weak, 1.0, OutlawRules.CrowdRetreat, Pair, 1.0));
    }

    [Fact]
    public void RunReason_NotFromASideJustAboutEven()
    {
        const int AboutEven = Strong + 1;
        var clearlyStronger = (int)(Strong * OutlawRules.RunPowerMargin) + 1;

        Assert.False(Runs(Strong, AboutEven, 1.0, NoCrowd, Pair, 1.0));
        Assert.True(Runs(Strong, clearlyStronger, 1.0, NoCrowd, Pair, 1.0));
    }

    [Fact]
    public void RunReason_ARedFinishesANearlyBeatenVictim_UnlessBadlyHurtItself()
    {
        const double NearlyBeaten = FocusRules.FinishHitsFraction - 0.01;

        Assert.False(Runs(Weak, Strong, 1.0, NoCrowd, Pair, NearlyBeaten));
        Assert.False(Runs(Strong, Weak, 1.0, OutlawRules.CrowdRetreat, Pair, NearlyBeaten));
        Assert.True(Runs(Strong, Weak, OutlawRules.RunHitsFraction - 0.01, NoCrowd, Pair, NearlyBeaten));
    }

    /// <summary>A red runs for any reason <see cref="OutlawRules.RunReason"/> gives.</summary>
    private static bool Runs(int selfPower, int incomingPower, double hitsFraction, int blueCrowd, int packSize, double victimHitsFraction) =>
        OutlawRules.RunReason(selfPower, incomingPower, hitsFraction, blueCrowd, packSize, victimHitsFraction) != RedRunReason.None;

    [Fact]
    public void RunReason_NamesTheTestThatFired()
    {
        Assert.Equal(RedRunReason.Hurt, OutlawRules.RunReason(Strong, Weak, OutlawRules.RunHitsFraction - 0.01, NoCrowd, Pair, 1.0));
        Assert.Equal(RedRunReason.Stronger, OutlawRules.RunReason(Weak, Strong, 1.0, NoCrowd, Pair, 1.0));
        Assert.Equal(RedRunReason.Mob, OutlawRules.RunReason(Strong, Weak, 1.0, OutlawRules.CrowdRetreat, Pair, 1.0));
        Assert.Equal(RedRunReason.None, OutlawRules.RunReason(Strong, Weak, 1.0, NoCrowd, Pair, 1.0));
        Assert.Equal("a mob", OutlawRules.RunWords(RedRunReason.Mob));
    }

    [Fact]
    public void RedCount_SilentFile_IsOneInTen() =>
        Assert.Equal(DefaultReds, OutlawRules.RedCount(null, null, DefaultPopulation, enabledWhenUnset: true));

    [Fact]
    public void RedCount_OperatorValues_AlwaysWin()
    {
        Assert.Equal(OperatorReds, OutlawRules.RedCount(true, OperatorReds, DefaultPopulation, enabledWhenUnset: true));
        Assert.Equal(OperatorReds, OutlawRules.RedCount(null, OperatorReds, DefaultPopulation, enabledWhenUnset: true));
        Assert.Equal(0, OutlawRules.RedCount(false, OperatorReds, DefaultPopulation, enabledWhenUnset: true));
        Assert.Equal(0, OutlawRules.RedCount(true, 0, DefaultPopulation, enabledWhenUnset: true));
    }

    [Fact]
    public void RedCount_FacetWithoutReds_StaysEmptyUnlessTurnedOn()
    {
        Assert.Equal(0, OutlawRules.RedCount(null, null, DefaultPopulation, enabledWhenUnset: false));
        Assert.Equal(DefaultReds, OutlawRules.RedCount(true, null, DefaultPopulation, enabledWhenUnset: false));
    }

    [Fact]
    public void RedCount_NeverExceedsPopulation() =>
        Assert.Equal(DefaultPopulation, OutlawRules.RedCount(true, DefaultPopulation * 2, DefaultPopulation, true));

    [Fact]
    public void VictimScore_PrefersNearHurtIsolatedAndWeaker()
    {
        var baseline = OutlawRules.VictimScore(Far, 1.0, OutlawRules.IsolationThreshold, tierGap: 0);

        Assert.True(OutlawRules.VictimScore(Near, 1.0, OutlawRules.IsolationThreshold, 0) > baseline);
        Assert.True(OutlawRules.VictimScore(Far, 0.3, OutlawRules.IsolationThreshold, 0) > baseline);
        Assert.True(OutlawRules.VictimScore(Far, 1.0, 0, 0) > baseline);
        Assert.True(OutlawRules.VictimScore(Far, 1.0, OutlawRules.IsolationThreshold, 2) > baseline);
        Assert.Equal(baseline, OutlawRules.VictimScore(Far, 1.0, OutlawRules.IsolationThreshold, -3));
        Assert.True(OutlawRules.VictimScore(Far, 1.0, OutlawRules.IsolationThreshold + 3, 0) < baseline);
    }

    [Fact]
    public void IsLiveHumanCombatant_Monster_DoesNotBlockPick()
    {
        const bool deleted = false;
        const bool alive = true;
        const bool isPlayer = false;
        const bool isSosaria = false;

        Assert.False(OutlawRules.IsLiveHumanCombatant(deleted, alive, isPlayer, isSosaria));
    }

    [Fact]
    public void IsLiveHumanCombatant_PlayerOrBot_HoldsFight()
    {
        const bool deleted = false;
        const bool alive = true;

        Assert.True(OutlawRules.IsLiveHumanCombatant(deleted, alive, isPlayer: true, isSosaria: false));
        Assert.True(OutlawRules.IsLiveHumanCombatant(deleted, alive, isPlayer: false, isSosaria: true));
    }

    [Fact]
    public void IsLiveHumanCombatant_DeadOrDeleted_DoesNotHoldFight()
    {
        const bool isPlayer = true;
        const bool isSosaria = false;

        Assert.False(OutlawRules.IsLiveHumanCombatant(deleted: true, alive: true, isPlayer, isSosaria));
        Assert.False(OutlawRules.IsLiveHumanCombatant(deleted: false, alive: false, isPlayer, isSosaria));
    }
}
