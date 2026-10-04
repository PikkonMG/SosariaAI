using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class IdleAtCurrentSpotTests
{
    [Fact]
    public void AlreadyLoitering_EveryMinuteKeepsTheSameStand()
    {
        // The lifecycle pulse idles an off-hours fixture every minute. Dropping the routine
        // each time restarted its step: one hider rejoined the bank crowd sixty times.
        Assert.True(LoiterSpotRules.AlreadyLoitering(SkillKinds.Loiter, needsNext: false));
        Assert.False(LoiterSpotRules.AlreadyLoitering(SkillKinds.Loiter, needsNext: true));
        Assert.False(LoiterSpotRules.AlreadyLoitering(SkillKinds.BankCrowd, needsNext: false));
        Assert.False(LoiterSpotRules.AlreadyLoitering(null, needsNext: false));
    }
}
