using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class WeightedChoiceTests
{
    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.24, 0)]
    [InlineData(0.26, 2)]
    [InlineData(0.99, 2)]
    [InlineData(1.0, 2)]
    public void Index_LandsByShareOfTheTotal(double unit, int index) =>
        Assert.Equal(index, WeightedChoice.Index([1.0, 0.0, 3.0], unit));

    [Fact]
    public void Index_NothingWeighs_IsMinusOne()
    {
        Assert.Equal(-1, WeightedChoice.Index([0.0, -1.0], 0.5));
        Assert.Equal(-1, WeightedChoice.Index([], 0.5));
        Assert.Equal(-1, WeightedChoice.Index(null, 0.5));
    }
}
