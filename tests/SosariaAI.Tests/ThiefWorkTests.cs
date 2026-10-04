using System;
using SosariaAI.Behaviour;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class ThiefWorkTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 1, 0, 0);

    [Fact]
    public void LookDue_AtOnceThenAfterTheGap()
    {
        Assert.True(ThiefWork.LookDue(default, Now));
        Assert.False(ThiefWork.LookDue(Now + StealRules.LookGapMin, Now));
        Assert.True(ThiefWork.LookDue(Now, Now));
    }
}
