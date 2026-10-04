using System;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class RedAlarmTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 10, 0, 0);

    [Fact]
    public void WorthScream_NotInTheRedsTownNorAtOnesOwnFoe()
    {
        Assert.True(RedAlarm.WorthScream(redInBucsDen: false, fightingWitness: false));
        Assert.False(RedAlarm.WorthScream(redInBucsDen: true, fightingWitness: false));
        Assert.False(RedAlarm.WorthScream(redInBucsDen: false, fightingWitness: true));
    }

    [Fact]
    public void ScreamDue_OncePerPlaceInTheRest()
    {
        Assert.True(RedAlarm.ScreamDue(default, Now));
        Assert.False(RedAlarm.ScreamDue(Now, Now + RedAlarm.ScreamRest - TimeSpan.FromSeconds(1)));
        Assert.True(RedAlarm.ScreamDue(Now, Now + RedAlarm.ScreamRest));
    }
}
