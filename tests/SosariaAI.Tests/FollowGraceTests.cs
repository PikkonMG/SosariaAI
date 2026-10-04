using System;
using Server;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// Every way into a fight keeps the run grace but the follow step: a party member that ran
/// from the leader's foe was handed that foe again on its next think, and ran again.
/// </summary>
public class FollowGraceTests
{
    private static uint _nextSerial = 0x8E01;

    static FollowGraceTests() => Timer.Init(0);

    public FollowGraceTests() => TestMap.EnsureInternal();

    [Fact]
    public void TakesLeadersFoe_AFreshFoe()
    {
        Assert.True(FollowSkill.TakesLeadersFoe(Person(), Person()));
    }

    [Fact]
    public void TakesLeadersFoe_NeverTheOneItRanFromLately()
    {
        var follower = Person();
        var foe = Person();
        follower.LastRanFrom = foe.Serial;
        follower.LastRanFromAt = DateTime.UtcNow;

        Assert.False(FollowSkill.TakesLeadersFoe(follower, foe));
    }

    [Fact]
    public void TakesLeadersFoe_NeverWithNoFoe() =>
        Assert.False(FollowSkill.TakesLeadersFoe(Person(), null));

    private static SosariaCharacter Person()
    {
        var person = new SosariaCharacter((Serial)_nextSerial++);
        person.DefaultMobileInit();
        return person;
    }
}
