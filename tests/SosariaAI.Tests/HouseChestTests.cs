using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class HouseChestTests
{
    [Theory]
    [InlineData(0, 100, 100)]
    [InlineData(40, 100, 60)]
    [InlineData(120, 100, 0)]
    [InlineData(-5, 10, 10)]
    public void TopUp_ToTheMark(int have, int target, int add) => Assert.Equal(add, HouseChest.TopUp(have, target));

    [Theory]
    [InlineData(20, 50, 20)]
    [InlineData(20, 5, 5)]
    [InlineData(-3, 5, 0)]
    public void Take_AsFarAsTheChestHolds(int want, int inChest, int taken) =>
        Assert.Equal(taken, HouseChest.Take(want, inChest));
}
