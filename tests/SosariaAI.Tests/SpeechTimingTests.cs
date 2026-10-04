using System;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class SpeechTimingTests
{
    [Fact]
    public void NextDelay_VariesAroundTheBase()
    {
        var configured = TimeSpan.FromMinutes(3);
        var low = SpeechTiming.NextDelay(configured, 0);
        var high = SpeechTiming.NextDelay(configured, SpeechTiming.PercentRange);

        Assert.True(low < configured);
        Assert.True(high > configured);
        Assert.Equal(SpeechTiming.MinScalePercent / 100.0, low.TotalMinutes / configured.TotalMinutes, 5);
    }
}
