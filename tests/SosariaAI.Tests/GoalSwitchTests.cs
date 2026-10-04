using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class GoalSwitchTests
{
    [Fact]
    public void ThreeFailures_SwitchTradeToRecoverToLeisureToWork()
    {
        Assert.False(GoalSwitch.ShouldSwitch(2));
        Assert.True(GoalSwitch.ShouldSwitch(GoalSwitch.FailedActionsBeforeSwitch));
        Assert.Equal(GoalKind.Recover, GoalSwitch.Next(GoalKind.Trade));
        Assert.Equal(GoalKind.Leisure, GoalSwitch.Next(GoalKind.Recover));
        Assert.Equal(GoalKind.Work, GoalSwitch.Next(GoalKind.Leisure));
    }

    [Fact]
    public void AfterFailure_CountsTheSameGoal()
    {
        var count = GoalSwitch.AfterFailure(GoalKind.Trade, GoalKind.Trade, 0);
        count = GoalSwitch.AfterFailure(GoalKind.Trade, GoalKind.Trade, count);
        count = GoalSwitch.AfterFailure(GoalKind.Trade, GoalKind.Trade, count);
        Assert.Equal(3, count);
        Assert.Equal(1, GoalSwitch.AfterFailure(GoalKind.Trade, GoalKind.Work, count));
        Assert.Equal(0, GoalSwitch.AfterSuccess());
    }
}
