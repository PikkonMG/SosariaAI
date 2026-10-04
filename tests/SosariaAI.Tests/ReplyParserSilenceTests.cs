using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class ReplyParserSilenceTests
{
    [Fact]
    public void IsDeliberateSilence_TrueWhenTheModelAnsweredWithAnEmptyLine()
    {
        Assert.True(ReplyParser.IsDeliberateSilence("""{"say":"","mood":"dry"}"""));
        Assert.True(ReplyParser.IsDeliberateSilence("""{"say":"   "}"""));
    }

    [Fact]
    public void IsDeliberateSilence_FalseWhenTheReplyIsBroken()
    {
        // The live shard saw this from a small local model: a missing comma.
        Assert.False(ReplyParser.IsDeliberateSilence("""{"say":"Morning Olin." "mood":"tired"}"""));
        Assert.False(ReplyParser.IsDeliberateSilence("not json at all"));
        Assert.False(ReplyParser.IsDeliberateSilence(""));
    }

    [Fact]
    public void IsDeliberateSilence_FalseWhenThereIsSomethingToSay()
    {
        Assert.False(ReplyParser.IsDeliberateSilence("""{"say":"Bank is west.","mood":"calm"}"""));
    }
}
