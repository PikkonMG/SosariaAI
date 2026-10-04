using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class SpiritRulesTests
{
    private const string ExpectedKind = "Spirit";
    private const double ExpectedPracticeMin = 0;
    private const double ExpectedPracticeMax = 100;

    [Fact]
    public void Kind_IsSpirit()
    {
        Assert.Equal(ExpectedKind, SpiritRules.Kind);
        Assert.Equal(ExpectedKind, new SpiritSkill().Name);
    }

    [Fact]
    public void PracticeWindow_MatchesModernUOSpiritSpeakCheck()
    {
        Assert.Equal(ExpectedPracticeMin, SpiritRules.PracticeMin);
        Assert.Equal(ExpectedPracticeMax, SpiritRules.PracticeMax);
    }
}
