using System;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class SkillFailReasonTests
{
    private const string Reason = "no shop in reach";

    [Fact]
    public void CannotStart_KeepsItsReasonUntilCleared()
    {
        var skill = new RefusingSkill();

        Assert.False(skill.Begin(null));
        Assert.Equal(Reason, skill.FailReason);

        skill.ClearFailReason();
        Assert.Null(skill.FailReason);
    }

    [Fact]
    public void Fail_ReturnsFailedWithItsReason()
    {
        var skill = new RefusingSkill();

        Assert.Equal(SkillStatus.Failed, skill.Tick());
        Assert.Equal(Reason, skill.FailReason);
    }

    private sealed class RefusingSkill : Skill
    {
        public override string Name => nameof(RefusingSkill);

        public override bool Begin(SosariaCharacter character) => CannotStart(Reason);

        public override SkillStatus Tick() => Fail(Reason);

        public override void Abort()
        {
        }
    }
}
