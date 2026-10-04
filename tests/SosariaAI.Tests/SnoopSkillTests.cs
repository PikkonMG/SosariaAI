using System;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class SnoopSkillTests
{
    private const string ExpectedSkillName = "Snoop";

    [Fact]
    public void Name_IsSnoop() =>
        Assert.Equal(ExpectedSkillName, new SnoopSkill().Name);

    [Fact]
    public void MaySnoop_Null_IsFalse() =>
        Assert.False(StealRules.MaySnoop(null));

    [Fact]
    public void ThiefLift_AddsSnoopAfterLockpick()
    {
        var file = CharactersFile.CreateDefault();
        var nyle = file.Facets[FacetNames.Felucca].FindRoster(PersonasFile.NyleId);
        Assert.NotNull(nyle);

        var lift = nyle.Routines["lift"];
        var lockpickIndex = lift.FindIndex(s =>
            string.Equals(s.Skill, SkillKinds.Lockpick, StringComparison.OrdinalIgnoreCase));
        var trapIndex = lift.FindIndex(s =>
            string.Equals(s.Skill, SkillKinds.RemoveTrap, StringComparison.OrdinalIgnoreCase));
        var tinkerIndex = lift.FindIndex(s =>
            string.Equals(s.Skill, SkillKinds.Tinker, StringComparison.OrdinalIgnoreCase));
        var snoopIndex = lift.FindIndex(s =>
            string.Equals(s.Skill, SkillKinds.Snoop, StringComparison.OrdinalIgnoreCase));

        Assert.True(lockpickIndex >= 0);
        Assert.Equal(lockpickIndex + 1, trapIndex);
        Assert.Equal(trapIndex + 1, tinkerIndex);
        Assert.Equal(tinkerIndex + 1, snoopIndex);
    }
}
