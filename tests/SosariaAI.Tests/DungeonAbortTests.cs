using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class DungeonAbortTests
{
    [Theory]
    [InlineData(GhostSkill.SkillName, DelveFailure.Died)]
    [InlineData(ResurrectAidSkill.SkillName, DungeonAbort.RaiseWhy)]
    [InlineData(SkillKinds.Dungeon, DungeonAbort.NewRunWhy)]
    [InlineData(SkillKinds.Follow, DungeonAbort.PartyWhy)]
    [InlineData(PartyGatherSkill.SkillName, DungeonAbort.PartyWhy)]
    [InlineData(SkillKinds.Flee, DungeonAbort.FleeWhy)]
    public void Reason_NamesWhatTookTheTripOver(string nextSkill, string reason) =>
        Assert.Equal(reason, DungeonAbort.Reason(nextSkill));

    [Fact]
    public void Reason_AnyOtherJob_IsNamed() =>
        Assert.Equal($"{SkillKinds.Conflict} {DungeonAbort.TookOverWhy}", DungeonAbort.Reason(SkillKinds.Conflict));

    [Fact]
    public void Line_ReadsLikeTheOtherEndLines() =>
        Assert.Equal(
            "Iolo ended Dungeon (Aborted: it died on the trip) at (2731, 2234, 0)",
            DungeonAbort.Line("Iolo", DelveFailure.Died, new Point3D(2731, 2234, 0))
        );
}
