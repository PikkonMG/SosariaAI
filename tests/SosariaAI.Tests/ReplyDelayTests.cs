using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class ReplyDelayTests
{
    [Fact]
    public void For_AddsPerCharacterToBase()
    {
        var delay = ReplyDelay.For("abcd");
        Assert.Equal(
            ReplyDelay.BaseMilliseconds + ReplyDelay.MillisecondsPerCharacter * 4,
            delay.TotalMilliseconds
        );
    }

    [Fact]
    public void For_CapsAtMaximum()
    {
        var delay = ReplyDelay.For(new string('a', 200));
        Assert.Equal(ReplyDelay.MaxMilliseconds, delay.TotalMilliseconds);
    }

    [Fact]
    public void For_NullSay_UsesBaseOnly()
    {
        Assert.Equal(ReplyDelay.BaseMilliseconds, ReplyDelay.For(null).TotalMilliseconds);
    }
}
