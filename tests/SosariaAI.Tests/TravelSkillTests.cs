using Server;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class TravelSkillTests
{
    [Fact]
    public void ContinueAfterWalk_WithoutSteps_ReturnsFailed()
    {
        var skill = new TravelSkill(new Point3D(1425, 1690, 0), 2);

        Assert.Equal(SkillStatus.Failed, skill.ContinueAfterWalk());
    }

    [Fact]
    public void UsesMoongate_OnlyAPlanThroughAPublicMoongate()
    {
        TravelStep[] walk =
        [
            new("A", new Point3D(1, 1, 0), NavGateKind.None),
            new("B", new Point3D(9, 1, 0), NavGateKind.Teleporter)
        ];
        TravelStep[] gated = [walk[0], new("C", new Point3D(900, 1, 0), NavGateKind.Moongate)];

        Assert.False(TravelSkill.UsesMoongate(walk));
        Assert.True(TravelSkill.UsesMoongate(gated));
        Assert.False(TravelSkill.UsesMoongate(null));
    }

    [Fact]
    public void Tick_WithoutBegin_ReturnsFailed()
    {
        var skill = new TravelSkill(new Point3D(1425, 1690, 0), 2);

        Assert.Equal(SkillStatus.Failed, skill.Tick());
    }

    [Fact]
    public void GroundRestLine_NamesTheSpotAndTheRest() =>
        Assert.Equal(
            "Tancred found no road back from (2161, 460, 0) and rests 120 s before it plans again",
            TravelSkill.GroundRestLine("Tancred", new Point3D(2161, 460, 0))
        );

    [Fact]
    public void DangerWaitLine_NamesTheWalkerAndWhereItWaits() =>
        Assert.Equal(
            "Maida Wilde waits at (2489, 764, 0) for the place it ran from to go quiet",
            TravelSkill.DangerWaitLine("Maida Wilde", new Point3D(2489, 764, 0))
        );

    [Fact]
    public void WithWalkWhy_AddsTheWalksReason_OnlyWhenItGaveOne()
    {
        var failedWalk = new TravelSkill(new Point3D(1425, 1690, 0), 2);
        failedWalk.NoteFailReason(TravelSkill.DangerousRoadWhy);

        Assert.Equal(
            $"{GoHomeSkill.EveryWayFailedWhy}: {TravelSkill.DangerousRoadWhy}",
            TravelSkill.WithWalkWhy(GoHomeSkill.EveryWayFailedWhy, failedWalk)
        );
        Assert.Equal(GoHomeSkill.EveryWayFailedWhy, TravelSkill.WithWalkWhy(GoHomeSkill.EveryWayFailedWhy, new TravelSkill(Point3D.Zero, 2)));
        Assert.Equal(GoHomeSkill.EveryWayFailedWhy, TravelSkill.WithWalkWhy(GoHomeSkill.EveryWayFailedWhy, null));
    }

    [Fact]
    public void GateRefusedLine_NamesTheGateAndThePad() =>
        Assert.Equal(
            "Harlan found no moongate to take at (1828, 2948, -20); the trip ends and later plans pay for the hop",
            TravelSkill.GateRefusedLine("Harlan", NavGateKind.Moongate, new Point3D(1828, 2948, -20))
        );
}
