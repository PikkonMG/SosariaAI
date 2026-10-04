using Server;
using SosariaAI.Behaviour;
using SosariaAI.Economy;
using SosariaAI.Navigation;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class BankTellerRulesTests
{
    private const int RichBalance = 100000;

    /// <summary>The Magincia banker's spawner; the town's bank marker stands twelve tiles south of it.</summary>
    private static readonly Point3D MaginciaBanker = new(3734, 2149, 20);

    [Fact]
    public void Keywords_MatchTheBanker()
    {
        Assert.Equal(0x0000, BankTellerRules.WithdrawKeyword);
        Assert.Equal(0x0001, BankTellerRules.BalanceKeyword);
        Assert.Equal(0x0002, BankTellerRules.BankKeyword);
    }

    [Fact]
    public void DepositAmount_BanksOnlyTheSurplus()
    {
        Assert.Equal(0, BankTellerRules.DepositAmount(BankTellerRules.WalkingMoney));
        Assert.Equal(0, BankTellerRules.DepositAmount(BankTellerRules.WalkingMoney + BankTellerRules.MinTransaction - 1));
        Assert.Equal(
            BankTellerRules.MinTransaction,
            BankTellerRules.DepositAmount(BankTellerRules.WalkingMoney + BankTellerRules.MinTransaction)
        );
    }

    [Fact]
    public void WithdrawAmount_RefillsWalkingMoneyInRoundNumbers()
    {
        var amount = BankTellerRules.WithdrawAmount(
            packGold: 150,
            RichBalance,
            BankTellerRules.ClassicWithdrawCeiling
        );

        Assert.Equal(900, amount);
        Assert.Equal(0, amount % BankTellerRules.RoundTo);
    }

    [Fact]
    public void WithdrawAmount_NeverNamesMoreThanTheAccountHolds()
    {
        Assert.Equal(300, BankTellerRules.WithdrawAmount(0, 350, BankTellerRules.ClassicWithdrawCeiling));
        Assert.Equal(0, BankTellerRules.WithdrawAmount(0, BankTellerRules.MinTransaction - 1, BankTellerRules.ClassicWithdrawCeiling));
    }

    [Fact]
    public void WithdrawAmount_ZeroWhenThePackIsFull() =>
        Assert.Equal(0, BankTellerRules.WithdrawAmount(BankTellerRules.WalkingMoney, RichBalance, BankTellerRules.ClassicWithdrawCeiling));

    [Fact]
    public void WithdrawAmount_KeepsToTheBankerCeiling() =>
        Assert.Equal(
            BankTellerRules.MinTransaction,
            BankTellerRules.WithdrawAmount(0, RichBalance, BankTellerRules.MinTransaction)
        );

    [Fact]
    public void WithdrawCeiling_FollowsTheEra()
    {
        Assert.Equal(BankTellerRules.ClassicWithdrawCeiling, BankTellerRules.WithdrawCeiling(mondainsLegacy: false));
        Assert.Equal(BankTellerRules.ModernWithdrawCeiling, BankTellerRules.WithdrawCeiling(mondainsLegacy: true));
    }

    [Fact]
    public void WithdrawLine_IsTheSpokenCommand() =>
        Assert.Equal("withdraw 2000", BankTellerRules.WithdrawLine(2000));

    [Fact]
    public void PurseNeedsBanker_OnlyWhenHeavyOrLight()
    {
        Assert.False(BankTellerRules.PurseNeedsBanker(BankTellerRules.WalkingMoney, RichBalance, boxTakesGold: true));
        Assert.True(BankTellerRules.PurseNeedsBanker(BankTellerRules.WalkingMoney * BankTellerRules.HeavyPurseMultiple + 1, 0, boxTakesGold: true));
        Assert.True(BankTellerRules.PurseNeedsBanker(0, RichBalance, boxTakesGold: true));
        Assert.False(BankTellerRules.PurseNeedsBanker(0, 0, boxTakesGold: true));
    }

    [Fact]
    public void PurseNeedsBanker_AHeavyPurseIsNoTripToABoxThatRefusesGold()
    {
        const int heavyPurse = BankTellerRules.WalkingMoney * BankTellerRules.HeavyPurseMultiple + 1;

        Assert.False(BankTellerRules.PurseNeedsBanker(heavyPurse, RichBalance, boxTakesGold: false));

        // A light purse still draws from the account: a withdrawal needs no room in the box.
        Assert.True(BankTellerRules.PurseNeedsBanker(0, RichBalance, boxTakesGold: false));
    }

    [Fact]
    public void AsksBalance_FollowsThePercent()
    {
        Assert.True(BankTellerRules.AsksBalance(0));
        Assert.True(BankTellerRules.AsksBalance(BankTellerRules.BalanceAskPercent - 1));
        Assert.False(BankTellerRules.AsksBalance(BankTellerRules.BalanceAskPercent));
    }

    [Fact]
    public void BankerWalkRange_ReachesTheMaginciaBankerFromAnywhereOnItsBankFloor()
    {
        // A trip ends up to the bank's quiet range from its spot, the spot is scattered off
        // the town's bank marker, and Magincia's marker stands twelve tiles from the banker:
        // walkers there found no banker within sixteen tiles and failed at the box.
        var farthest = NavMetric.Chebyshev(WorkSites.MaginciaTown, MaginciaBanker) +
                       WorkSites.TownScatterRadius + MeetingRules.BankQuietRange;

        Assert.True(farthest <= BankTellerRules.BankerWalkRange);
    }

    [Fact]
    public void AtCounter_OpensOnlyForAnHonestPersonTheBankerHears()
    {
        Assert.Equal(CounterTurn.Open, BankTellerRules.AtCounter(criminal: false, bankerHears: true, bankerOnFloor: true, steps: 0));
        Assert.Equal(CounterTurn.Criminal, BankTellerRules.AtCounter(criminal: true, bankerHears: true, bankerOnFloor: true, steps: 0));
    }

    [Fact]
    public void AtCounter_OutOfHearingStepsCloserThenGivesUp()
    {
        Assert.Equal(CounterTurn.StepCloser, BankTellerRules.AtCounter(false, bankerHears: false, bankerOnFloor: true, steps: 0));
        Assert.Equal(
            CounterTurn.StepCloser,
            BankTellerRules.AtCounter(false, bankerHears: false, bankerOnFloor: true, steps: BankTellerRules.MaxCounterSteps - 1)
        );
        Assert.Equal(
            CounterTurn.OutOfHearing,
            BankTellerRules.AtCounter(false, bankerHears: false, bankerOnFloor: true, steps: BankTellerRules.MaxCounterSteps)
        );
        Assert.Equal(CounterTurn.NoBanker, BankTellerRules.AtCounter(false, bankerHears: false, bankerOnFloor: false, steps: 0));
    }
}
