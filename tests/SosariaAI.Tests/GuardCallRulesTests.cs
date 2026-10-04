using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class GuardCallRulesTests
{
    [Fact]
    public void ShouldCall_GuardedTownWithALiveFoe()
    {
        Assert.True(
            GuardCallRules.ShouldCall(
                selfAlive: true,
                ghost: false,
                inGuardedRegion: true,
                foeAlive: true,
                sameMap: true,
                foeIsGuard: false,
                foeIsPerson: false,
                foeIsOutlaw: false,
                foeUnderGuards: true
            )
        );
    }

    [Fact]
    public void ShouldCall_NotOutsideTownOrOnAFriend()
    {
        Assert.False(
            GuardCallRules.ShouldCall(
                selfAlive: true,
                ghost: false,
                inGuardedRegion: false,
                foeAlive: true,
                sameMap: true,
                foeIsGuard: false,
                foeIsPerson: false,
                foeIsOutlaw: false,
                foeUnderGuards: true
            )
        );
        Assert.False(
            GuardCallRules.ShouldCall(
                selfAlive: true,
                ghost: false,
                inGuardedRegion: true,
                foeAlive: true,
                sameMap: true,
                foeIsGuard: false,
                foeIsPerson: true,
                foeIsOutlaw: false,
                foeUnderGuards: true
            )
        );
        Assert.False(
            GuardCallRules.ShouldCall(
                selfAlive: true,
                ghost: false,
                inGuardedRegion: true,
                foeAlive: true,
                sameMap: true,
                foeIsGuard: true,
                foeIsPerson: false,
                foeIsOutlaw: false,
                foeUnderGuards: true
            )
        );
    }

    [Fact]
    public void ShouldCall_OnARedOrGrayPersonUnderTheSameWatch()
    {
        Assert.True(
            GuardCallRules.ShouldCall(
                selfAlive: true,
                ghost: false,
                inGuardedRegion: true,
                foeAlive: true,
                sameMap: true,
                foeIsGuard: false,
                foeIsPerson: true,
                foeIsOutlaw: true,
                foeUnderGuards: true
            )
        );
        Assert.False(
            GuardCallRules.ShouldCall(
                selfAlive: true,
                ghost: false,
                inGuardedRegion: true,
                foeAlive: true,
                sameMap: true,
                foeIsGuard: false,
                foeIsPerson: true,
                foeIsOutlaw: true,
                foeUnderGuards: false
            )
        );
    }

    [Fact]
    public void MarksFoeCriminal_OnlyAMonster()
    {
        Assert.True(GuardCallRules.MarksFoeCriminal(foeIsPerson: false));
        Assert.False(GuardCallRules.MarksFoeCriminal(foeIsPerson: true));
    }

    [Fact]
    public void Shout_IsGuards()
    {
        Assert.Equal("Guards!", GuardCallRules.Shout);
        Assert.Contains("guard", GuardCallRules.Shout, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsOutlaw_AGrayFromALawfulFightIsNot()
    {
        Assert.True(GuardCallRules.IsOutlaw(murderer: true, criminal: false, inLawfulFight: true));
        Assert.True(GuardCallRules.IsOutlaw(murderer: false, criminal: true, inLawfulFight: false));
        Assert.False(GuardCallRules.IsOutlaw(murderer: false, criminal: true, inLawfulFight: true));
        Assert.False(GuardCallRules.IsOutlaw(murderer: false, criminal: false, inLawfulFight: false));
    }

    [Fact]
    public void CallDue_OneShoutPerFoeAtATime()
    {
        var now = new System.DateTime(2026, 9, 24, 10, 9, 13);

        Assert.True(GuardCallRules.CallDue(default, default, now));
        Assert.False(GuardCallRules.CallDue(default, now, now));
        Assert.False(GuardCallRules.CallDue(now, default, now.AddSeconds(GuardCallRules.CooldownSeconds - 1)));
        Assert.False(GuardCallRules.CallDue(default, now, now.AddSeconds(GuardCallRules.FoeCooldownSeconds - 1)));
        Assert.True(GuardCallRules.CallDue(now, now, now.AddSeconds(GuardCallRules.FoeCooldownSeconds)));
    }
}
