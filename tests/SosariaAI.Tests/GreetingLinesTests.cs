using System;
using SosariaAI.Behaviour;
using Xunit;
using SosariaAI.Memory;

namespace SosariaAI.Tests;

public class GreetingLinesTests
{
    [Fact]
    public void Pick_ReplacesAShortMachineLine()
    {
        Assert.True(GreetingLines.IsTooShort("Yes, Joel."));
        var line = GreetingLines.Pick(0, "Joel", "Yes, Joel.", 0);
        Assert.Contains("Joel", line);
        Assert.False(GreetingLines.IsTooShort(line));
        Assert.Equal("Morning, Mira.", GreetingLines.Pick(0, "Mira", "Morning, Mira.", 0));
    }

    [Fact]
    public void Pick_ColdLine_IsLongEnoughToSoundLikeSpeech()
    {
        var line = GreetingLines.Pick(BondRules.ColdThreshold, "Hal", "Yes, Hal.", 0);
        Assert.Contains("Hal", line);
        Assert.False(GreetingLines.IsTooShort(line));
        Assert.False(line.Equals("Hal.", StringComparison.Ordinal));
    }
}
