using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class SpawnPlanTests
{
    [Fact]
    public void Build_CountLessThanRoster_UsesFirstEntriesWithoutNumbers()
    {
        var plan = SpawnPlan.Build(["a", "b", "c"], 2);

        Assert.Equal(2, plan.Count);
        Assert.Equal(new SpawnPlanEntry("a", "a"), plan[0]);
        Assert.Equal(new SpawnPlanEntry("b", "b"), plan[1]);
    }

    [Fact]
    public void Build_CountEqualsRoster_UsesEachTemplateOnce()
    {
        var plan = SpawnPlan.Build(["a", "b", "c"], 3);

        Assert.Equal(3, plan.Count);
        Assert.Equal(new SpawnPlanEntry("a", "a"), plan[0]);
        Assert.Equal(new SpawnPlanEntry("b", "b"), plan[1]);
        Assert.Equal(new SpawnPlanEntry("c", "c"), plan[2]);
    }

    [Fact]
    public void Build_CountGreaterThanRoster_NumbersLaterCopiesFromOne()
    {
        var plan = SpawnPlan.Build(["a", "b", "c"], 5);

        Assert.Equal(5, plan.Count);
        Assert.Equal(new SpawnPlanEntry("a", "a"), plan[0]);
        Assert.Equal(new SpawnPlanEntry("b", "b"), plan[1]);
        Assert.Equal(new SpawnPlanEntry("c", "c"), plan[2]);
        Assert.Equal(new SpawnPlanEntry("a#1", "a"), plan[3]);
        Assert.Equal(new SpawnPlanEntry("b#1", "b"), plan[4]);
    }

    [Fact]
    public void Build_EmptyRoster_ReturnsEmpty()
    {
        Assert.Empty(SpawnPlan.Build([], 4));
        Assert.Empty(SpawnPlan.Build(null, 4));
        Assert.Empty(SpawnPlan.Build(["a"], 0));
    }
}
