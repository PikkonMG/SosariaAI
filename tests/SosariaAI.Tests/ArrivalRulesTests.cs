using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class ArrivalRulesTests
{
    private const int RollSweep = 1000;

    [Fact]
    public void Style_SplitsLingerWaitAndWander()
    {
        var counts = new Dictionary<ArrivalStyle, int>();

        for (var roll = 0; roll < ArrivalRules.PercentScale; roll++)
        {
            var style = ArrivalRules.Style(roll);
            counts[style] = counts.GetValueOrDefault(style) + 1;
        }

        Assert.Equal(ArrivalRules.LingerPercent, counts[ArrivalStyle.Linger]);
        Assert.Equal(ArrivalRules.WaitPercent, counts[ArrivalStyle.Wait]);
        Assert.Equal(
            ArrivalRules.PercentScale - ArrivalRules.LingerPercent - ArrivalRules.WaitPercent,
            counts[ArrivalStyle.Wander]
        );
    }

    [Theory]
    [InlineData(ArrivalStyle.Linger, ArrivalRules.LingerMinSeconds, ArrivalRules.LingerMaxSeconds)]
    [InlineData(ArrivalStyle.Wait, ArrivalRules.WaitMinSeconds, ArrivalRules.WaitMaxSeconds)]
    public void Length_StaysInTheStyleRange(ArrivalStyle style, int min, int max)
    {
        for (var roll = -RollSweep; roll < RollSweep; roll++)
        {
            Assert.InRange(ArrivalRules.Length(style, roll), TimeSpan.FromSeconds(min), TimeSpan.FromSeconds(max));
        }
    }

    [Fact]
    public void Wander_WalksStraightOn()
    {
        // A traveller who does not stay leaves for the next thing the place offers at once.
        Assert.False(ArrivalRules.Stays(ArrivalStyle.Wander));
        Assert.True(ArrivalRules.Stays(ArrivalStyle.Linger));
        Assert.True(ArrivalRules.Stays(ArrivalStyle.Wait));
        Assert.Equal(TimeSpan.Zero, ArrivalRules.Length(ArrivalStyle.Wander, RollSweep));
    }
}
