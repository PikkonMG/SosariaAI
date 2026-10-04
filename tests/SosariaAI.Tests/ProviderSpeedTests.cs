using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class ProviderSpeedTests
{
    private const string Chat = "frontier";
    private const double LimitMs = 5000;
    private const long FastMs = 800;
    private const long SlowMs = 30000;

    [Fact]
    public void IsFastEnough_ANewProviderGetsAFirstTry() =>
        Assert.True(new ProviderSpeed().IsFastEnough(Chat, LimitMs));

    [Fact]
    public void IsFastEnough_OneSlowReplyAfterAFastOneStopsTheHelp()
    {
        var speed = new ProviderSpeed();
        speed.Record(Chat, FastMs);
        speed.Record(Chat, SlowMs);

        Assert.False(speed.IsFastEnough(Chat, LimitMs));
    }

    [Fact]
    public void IsFastEnough_FastRepliesBringItBack()
    {
        var speed = new ProviderSpeed();
        speed.Record(Chat, SlowMs);

        for (var i = 0; i < 10; i++)
        {
            speed.Record(Chat, FastMs);
        }

        Assert.True(speed.IsFastEnough(Chat, LimitMs));
    }

    [Fact]
    public void Record_IgnoresNamelessOrNegativeReplies()
    {
        var speed = new ProviderSpeed();
        speed.Record(null, SlowMs);
        speed.Record(Chat, -1);

        Assert.Null(speed.AverageMs(Chat));
    }
}
