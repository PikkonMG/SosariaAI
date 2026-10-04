using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// 273 Conflict, 84 Hunt and 104 Lore failures ended with a bare "(Failed)". A job that fails
/// says why on its end line, and a practice session carries the reason of its last try.
/// </summary>
public class JobFailureReasonTests
{
    private const int FarGroundX = 5000;
    private const int FarGroundY = 5000;
    private const int GroundSize = 10;
    private const double StopBelowHits = 0.3;
    private static readonly TimeSpan HuntLength = TimeSpan.FromMinutes(10);
    private static readonly Point3D Britain = new(1434, 1699, 0);
    private static uint _nextSerial = 0x8D01;

    static JobFailureReasonTests() => Timer.Init(0);

    public JobFailureReasonTests() => TestMap.EnsureInternal();

    [Fact]
    public void Conflict_NoConflictToSeek_SaysSo()
    {
        var skill = new ConflictSkill();

        Assert.False(skill.Begin(Person()));
        Assert.Equal(ConflictSkill.NoConflictWhy, skill.FailReason);
    }

    [Fact]
    public void Conflict_Reasons_NameTheGoal()
    {
        var camp = ConflictSkill.CampGoal("Shame");

        Assert.Equal("the camp at Shame", camp);
        Assert.Equal("no walk to the camp at Shame", ConflictSkill.NoWalkWhy(camp));
        Assert.Equal("the walk to the murder report failed", ConflictSkill.WalkFailedWhy(ConflictSkill.ReportGoal));
    }

    [Fact]
    public void Hunt_GroundBeyondTheLeash_SaysSo()
    {
        var hunt = new HuntSkill(
            new Rectangle2D(FarGroundX, FarGroundY, GroundSize, GroundSize),
            HuntLength,
            StopBelowHits,
            null,
            null
        );

        var hunter = Person();
        hunter.Location = Britain;

        Assert.False(hunt.Begin(hunter));
        Assert.Equal(HuntSkill.GroundOutOfReachWhy, hunt.FailReason);
    }

    [Fact]
    public void Practice_CarriesTheReasonOfItsLastTry()
    {
        var session = new PracticeSession(new LoreSkill());

        Assert.False(session.Begin(Person()));
        Assert.Equal(LoreRules.NoSubjectWhy, session.FailReason);
    }

    private static SosariaCharacter Person()
    {
        var person = new SosariaCharacter((Serial)_nextSerial++);
        person.DefaultMobileInit();
        return person;
    }
}
