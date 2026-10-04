using System;
using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class GoToSkillTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0);

    [Fact]
    public void TimeUp_AfterGiveUp()
    {
        Assert.False(GoToSkill.TimeUp(Start, default));
        Assert.False(GoToSkill.TimeUp(Start, Start));
        Assert.True(GoToSkill.TimeUp(Start + GoToSkill.GiveUp, Start));
    }
}
