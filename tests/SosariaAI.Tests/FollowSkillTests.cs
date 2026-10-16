using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A follower planned its walk again each time the leader stepped to a new tile, and threw the
/// route away before it came back: it never moved while the leader walked.
/// </summary>
public class FollowSkillTests
{
    private const int WayLeft = 40;
    private const int SmallDrift = 3;

    private static readonly Point3D Follower = new(1000, 1000, 0);
    private static readonly Point3D WalkEnd = new(1000 + WayLeft, 1000, 0);

    [Fact]
    public void LeaderMovedOff_OneStep_KeepsTheWalk() =>
        Assert.False(FollowSkill.LeaderMovedOff(Follower, WalkEnd, new Point3D(WalkEnd.X + 1, WalkEnd.Y, 0)));

    [Fact]
    public void LeaderMovedOff_SmallDriftOnALongWay_KeepsTheWalk() =>
        Assert.False(FollowSkill.LeaderMovedOff(Follower, WalkEnd, new Point3D(WalkEnd.X + SmallDrift, WalkEnd.Y, 0)));

    [Fact]
    public void LeaderMovedOff_PastHalfTheWayLeft_PlansAgain() =>
        Assert.True(FollowSkill.LeaderMovedOff(Follower, WalkEnd, new Point3D(WalkEnd.X, WalkEnd.Y + WayLeft / 2 + 1, 0)));

    [Fact]
    public void LeaderMovedOff_WalkArrived_PlansAgainOnAnyStep() =>
        Assert.True(FollowSkill.LeaderMovedOff(WalkEnd, WalkEnd, new Point3D(WalkEnd.X + 1, WalkEnd.Y, 0)));

    [Fact]
    public void LeaderMovedOff_LeaderStillAtTheEnd_KeepsTheWalk() =>
        Assert.False(FollowSkill.LeaderMovedOff(WalkEnd, WalkEnd, WalkEnd));
}
