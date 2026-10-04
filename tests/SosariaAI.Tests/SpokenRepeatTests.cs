using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class SpokenRepeatTests
{
    [Fact]
    public void Matches_IgnoresPunctuationAndCase()
    {
        var recent = new[] { "Aye, the axe is keen today." };
        Assert.True(SpokenRepeat.Matches("aye the axe is keen today", recent));
        Assert.False(SpokenRepeat.Matches("Need to get this catch to the bank.", recent));
        Assert.False(SpokenRepeat.Matches("Aye, the axe is keen today.", []));
    }

    [Fact]
    public void IsNearRepeat_SameIdeaWithAFewWordsChanged()
    {
        const string first = "A book would help more than this wait.";
        const string second = "A book would help more than waiting here.";
        Assert.True(SpokenRepeat.IsNearRepeat(second, [first]));
        Assert.False(
            SpokenRepeat.IsNearRepeat("Need to get this catch to the bank.", [first])
        );
    }

    [Fact]
    public void RememberedLines_HoldsALongStretchOfSpeech()
    {
        Assert.True(SpokenRepeat.RememberedLines > 5);
    }
}
