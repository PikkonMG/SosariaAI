using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class HouseWantRulesTests
{
    private const int SmallPrice = 43800;
    private const int Seeds = 4000;

    [Fact]
    public void WantsHouse_OneInFour()
    {
        var wanting = 0;

        for (var seed = 0; seed < Seeds; seed++)
        {
            wanting += HouseWantRules.WantsHouse(seed) ? 1 : 0;
        }

        Assert.Equal(Seeds / HouseWantRules.WantEvery, wanting);
    }

    [Fact]
    public void SizeFor_MostlySmallSomeTwoStoryAFewTowers()
    {
        int small = 0, twoStory = 0, tower = 0;

        for (var seed = 0; seed < Seeds * HouseWantRules.WantEvery; seed += HouseWantRules.WantEvery)
        {
            switch (HouseWantRules.SizeFor(seed))
            {
                case HouseSize.Tower: tower++; break;
                case HouseSize.TwoStory: twoStory++; break;
                default: small++; break;
            }
        }

        Assert.Equal(Seeds * HouseWantRules.TowerShare / HouseWantRules.Percent, tower);
        Assert.Equal(Seeds * HouseWantRules.TwoStoryShare / HouseWantRules.Percent, twoStory);
        Assert.True(small > twoStory && twoStory > tower);
    }

    [Theory]
    [InlineData(HouseSize.Small, SmallPrice)]
    [InlineData(HouseSize.TwoStory, HouseWantRules.TwoStoryPrice)]
    [InlineData(HouseSize.Tower, HouseWantRules.TowerPrice)]
    public void Price_TheArchitectsPrice(HouseSize size, int price) => Assert.Equal(price, HouseWantRules.Price(size, SmallPrice));

    [Fact]
    public void Goal_SavesForTheWantedHouseOrNone()
    {
        var wanting = 0;
        var notWanting = 1;
        var goal = HouseWantRules.Goal(wanting, SmallPrice);

        Assert.NotNull(goal);
        Assert.Equal(AmbitionKind.Gold, goal.Value.Kind);
        Assert.True(HouseWantRules.IsHouseTarget(goal.Value.Target));
        Assert.Equal(HouseWantRules.Price(HouseWantRules.SizeFor(wanting), SmallPrice), goal.Value.Goal);
        Assert.Null(HouseWantRules.Goal(notWanting, SmallPrice));
        Assert.Null(HouseWantRules.Goal(wanting, smallPrice: 0));
    }

    [Fact]
    public void IsHouseTarget_OnlyTheHouseGoals()
    {
        Assert.True(HouseWantRules.IsHouseTarget(HouseWantRules.SmallTarget));
        Assert.True(HouseWantRules.IsHouseTarget(HouseWantRules.TowerTarget));
        Assert.False(HouseWantRules.IsHouseTarget(AmbitionRules.NestEggTarget));
        Assert.False(HouseWantRules.IsHouseTarget(null));
    }
}
