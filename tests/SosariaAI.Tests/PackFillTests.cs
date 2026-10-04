using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class PackFillTests
{
    [Fact]
    public void IsAtOrAboveFraction_TrueWhenCurrentMeetsFractionOfMax()
    {
        Assert.True(PackFill.IsAtOrAboveFraction(80, 100, 0.8));
    }

    [Fact]
    public void IsAtOrAboveFraction_FalseWhenCurrentIsBelowFraction()
    {
        Assert.False(PackFill.IsAtOrAboveFraction(79, 100, 0.8));
    }

    [Fact]
    public void IsAtOrAboveFraction_TrueWhenFractionIsZero()
    {
        Assert.True(PackFill.IsAtOrAboveFraction(0, 100, 0));
    }

    [Fact]
    public void IsAtOrAboveFraction_TrueWhenMaxWeightIsZero()
    {
        Assert.True(PackFill.IsAtOrAboveFraction(0, 0, 0.8));
    }

    [Theory]
    [InlineData(40, 50, 0.8, true)]
    [InlineData(39, 50, 0.8, false)]
    [InlineData(1, 400, 0.8, false)]
    [InlineData(320, 400, 0.8, true)]
    public void IsAtOrAboveFraction_MatchesExpected(int current, int max, double fraction, bool expected)
    {
        Assert.Equal(expected, PackFill.IsAtOrAboveFraction(current, max, fraction));
    }
}
