using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class PartyTacticsRulesTests
{
    private const int Near = 5;
    private const int StrongPower = 120;
    private const int WeakPower = 80;
    private const uint LowSerial = 10;
    private const uint HighSerial = 20;
    private const int NoneClose = 0;
    private const int OneClose = 1;

    [Fact]
    public void CanTank_ALivingMeleeFighterNearAndNotRunning()
    {
        Assert.True(CanTank());
        Assert.False(CanTank(alive: false));
        Assert.False(CanTank(sameMap: false));
        Assert.False(CanTank(distance: PartyTacticsRules.TankRange + 1));
        Assert.False(CanTank(melee: false));
        Assert.False(CanTank(fighter: false));
        Assert.False(CanTank(fleeing: true));
        Assert.True(CanTank(distance: PartyTacticsRules.TankRange));
    }

    [Fact]
    public void StrongerTank_ThePowerThenTheLowerSerial()
    {
        Assert.True(PartyTacticsRules.StrongerTank(StrongPower, HighSerial, WeakPower, LowSerial));
        Assert.False(PartyTacticsRules.StrongerTank(WeakPower, LowSerial, StrongPower, HighSerial));
        Assert.True(PartyTacticsRules.StrongerTank(StrongPower, LowSerial, StrongPower, HighSerial));
        Assert.False(PartyTacticsRules.StrongerTank(StrongPower, HighSerial, StrongPower, LowSerial));
    }

    [Fact]
    public void TakesTankTarget_OnlyWithItsOwnFoeOffItAndNothingInMelee()
    {
        Assert.True(PartyTacticsRules.TakesTankTarget(tankFoeInSight: true, ownFoeOnSelf: false, NoneClose));
        Assert.False(PartyTacticsRules.TakesTankTarget(tankFoeInSight: false, ownFoeOnSelf: false, NoneClose));
        Assert.False(PartyTacticsRules.TakesTankTarget(tankFoeInSight: true, ownFoeOnSelf: true, NoneClose));
        Assert.False(PartyTacticsRules.TakesTankTarget(tankFoeInSight: true, ownFoeOnSelf: false, OneClose));
    }

    [Fact]
    public void WaitsForTank_AFreshFightWaitsForAReadyTank()
    {
        Assert.True(PartyTacticsRules.WaitsForTank(tankReady: true, FocusRules.PlainTier));
        Assert.False(PartyTacticsRules.WaitsForTank(tankReady: false, FocusRules.PlainTier));
        Assert.False(PartyTacticsRules.WaitsForTank(tankReady: true, FocusRules.FriendAttackerTier));
        Assert.False(PartyTacticsRules.WaitsForTank(tankReady: true, FocusRules.AttackerTier));
        Assert.False(PartyTacticsRules.WaitsForTank(tankReady: true, FocusRules.OutlawTier));
    }

    private static bool CanTank(
        bool alive = true,
        bool sameMap = true,
        int distance = Near,
        bool melee = true,
        bool fighter = true,
        bool fleeing = false
    ) =>
        PartyTacticsRules.CanTank(alive, sameMap, distance, melee, fighter, fleeing);
}
