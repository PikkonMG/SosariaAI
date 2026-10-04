namespace SosariaAI.Behaviour;

/// <summary>
/// Three failed actions under one goal switch the goal, not only the action.
/// </summary>
public static class GoalSwitch
{
    public const int FailedActionsBeforeSwitch = 3;

    public static bool ShouldSwitch(int consecutiveFailedActions) =>
        consecutiveFailedActions >= FailedActionsBeforeSwitch;

    public static GoalKind Next(GoalKind current) =>
        current switch
        {
            GoalKind.Trade => GoalKind.Recover,
            GoalKind.Recover => GoalKind.Leisure,
            GoalKind.Leisure => GoalKind.Work,
            GoalKind.Hunt => GoalKind.Recover,
            GoalKind.Dungeon => GoalKind.Recover,
            GoalKind.Flee => GoalKind.Recover,
            GoalKind.Pk => GoalKind.Recover,
            _ => GoalKind.Trade
        };

    public static int AfterFailure(GoalKind lastGoal, GoalKind failedGoal, int consecutive)
    {
        if (lastGoal == failedGoal)
        {
            return consecutive + 1;
        }

        return 1;
    }

    public static int AfterSuccess() => 0;
}
