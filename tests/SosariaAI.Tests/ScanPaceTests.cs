using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class ScanPaceTests
{
    [Fact]
    public void Due_FirstCall_FiresAndSchedules()
    {
        long next = 0;

        Assert.True(ScanPace.Due(10_000, ref next, ScanPace.AmbientMs, phase: 7));
        Assert.True(next > 10_000);
    }

    [Fact]
    public void Due_BeforeInterval_DoesNotFire()
    {
        long next = 0;

        Assert.True(ScanPace.Due(10_000, ref next, ScanPace.DangerMs, phase: 0));
        Assert.False(ScanPace.Due(10_500, ref next, ScanPace.DangerMs, phase: 0));
    }

    [Fact]
    public void Due_AfterInterval_FiresAgain()
    {
        long next = 0;

        ScanPace.Due(10_000, ref next, ScanPace.DangerMs, phase: 0);
        Assert.True(ScanPace.Due(next, ref next, ScanPace.DangerMs, phase: 0));
    }

    [Fact]
    public void Due_PhaseSpreadsTheCohort()
    {
        long a = 0, b = 0, c = 0;

        ScanPace.Due(10_000, ref a, ScanPace.AmbientMs, phase: 0);
        ScanPace.Due(10_000, ref b, ScanPace.AmbientMs, phase: 1000);
        ScanPace.Due(10_000, ref c, ScanPace.AmbientMs, phase: 2000);

        Assert.NotEqual(a, b);
        Assert.NotEqual(b, c);
    }
}
