using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class PlanBudgetRulesTests
{
    [Fact]
    public void MayPlan_UntilTheWindowBudgetIsSpent()
    {
        Assert.True(PlanBudgetRules.MayPlan(0, PlanBudget.BudgetMs));
        Assert.True(PlanBudgetRules.MayPlan(PlanBudget.BudgetMs - 1, PlanBudget.BudgetMs));
        Assert.False(PlanBudgetRules.MayPlan(PlanBudget.BudgetMs, PlanBudget.BudgetMs));
    }

    [Fact]
    public void WindowOver_RefillsAfterTheWindow()
    {
        Assert.False(PlanBudgetRules.WindowOver(PlanBudget.WindowMs - 1, PlanBudget.WindowMs));
        Assert.True(PlanBudgetRules.WindowOver(PlanBudget.WindowMs, PlanBudget.WindowMs));
    }

    [Fact]
    public void Budget_IsASmallShareOfTheGameThread() =>
        Assert.True(PlanBudget.BudgetMs < PlanBudget.WindowMs / 4);
}
