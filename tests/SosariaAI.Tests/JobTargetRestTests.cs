using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class JobTargetRestTests
{
    private const uint Bran = 0x7A0001;
    private const uint Darian = 0x7A0002;
    private const uint Jonas = 0x7A0003;
    private const uint Runa = 0x7A0004;
    private const uint Katla = 0x7A0005;
    private const uint Sela = 0x7A0006;

    private const string NoWalkToSela = "no walk to the ghost (Sela)";
    private const string TimeUpForSela = "the ghost was not raised in time (Sela)";
    private const string NoRoute = TravelSkill.NoRouteWhy;

    /// <summary>A way of failing may hold any text, even the tab that splits a saved streak.</summary>
    private const string NoWalkWithTab = "no walk\tto the passage";

    private static readonly DateTime Start = new(2026, 9, 28, 1, 6, 38, DateTimeKind.Utc);
    private static readonly JobTarget SelaGhost = new("112233", Point3D.Zero);
    private static readonly JobTarget OtherGhost = new("445566", Point3D.Zero);
    private static readonly Point3D DeluciaPassage = new(5903, 1402, 39);
    private static readonly JobTarget WalkToDelucia = new(DeluciaPassage.ToString(), DeluciaPassage);

    public JobTargetRestTests() => TestMap.EnsureInternal();

    private static SosariaCharacter Person(uint serial) => new((Serial)serial);

    [Fact]
    public void AfterTargetFailure_TheSameWayThreeTimesRestsTheTarget()
    {
        var streak = default(TargetStreak);

        for (var i = 0; i < RepeatFailure.Limit - 1; i++)
        {
            streak = RepeatFailure.AfterTargetFailure(streak, NoWalkToSela, Start, Point3D.Zero);
        }

        Assert.False(RepeatFailure.TargetRests(streak, Start));

        streak = RepeatFailure.AfterTargetFailure(streak, NoWalkToSela, Start, Point3D.Zero);

        Assert.True(RepeatFailure.TargetRests(streak, Start));
        Assert.Equal(Start + RepeatFailure.SkillCooldown, streak.RestUntil);
        Assert.False(RepeatFailure.TargetRests(streak, streak.RestUntil));
    }

    [Fact]
    public void AfterTargetFailure_AnotherWayStartsTheStreakAgain()
    {
        var streak = RepeatFailure.AfterTargetFailure(default, NoWalkToSela, Start, Point3D.Zero);
        streak = RepeatFailure.AfterTargetFailure(streak, NoWalkToSela, Start, Point3D.Zero);
        streak = RepeatFailure.AfterTargetFailure(streak, TimeUpForSela, Start, Point3D.Zero);

        Assert.Equal(1, streak.Count);
        Assert.False(RepeatFailure.TargetRests(streak, Start));
    }

    [Fact]
    public void AfterTargetFailure_TheRestGrowsAsTheStreakRunsOn()
    {
        var streak = default(TargetStreak);

        for (var i = 0; i <= RepeatFailure.Limit; i++)
        {
            streak = RepeatFailure.AfterTargetFailure(streak, NoWalkToSela, Start, Point3D.Zero);
        }

        Assert.Equal(Start + RepeatFailure.CooldownAfter(RepeatFailure.Limit + 1), streak.RestUntil);
    }

    [Fact]
    public void Store_RestsOnlyThatTargetOfThatPersonsJob()
    {
        var bran = Person(Bran);
        var darian = Person(Darian);

        for (var i = 0; i < RepeatFailure.Limit; i++)
        {
            JobTargetRest.NoteFailure(bran, ResurrectAidSkill.SkillName, SelaGhost, NoWalkToSela, Start);
        }

        Assert.True(JobTargetRest.Rests(bran, ResurrectAidSkill.SkillName, SelaGhost.Key, Start));
        Assert.False(JobTargetRest.Rests(bran, ResurrectAidSkill.SkillName, OtherGhost.Key, Start));
        Assert.False(JobTargetRest.Rests(darian, ResurrectAidSkill.SkillName, SelaGhost.Key, Start));
        Assert.False(JobTargetRest.Rests(bran, SkillKinds.Visit, SelaGhost.Key, Start));
    }

    [Fact]
    public void Store_AWorkedTargetEndsItsStreak()
    {
        var darian = Person(Darian);

        for (var i = 0; i < RepeatFailure.Limit; i++)
        {
            JobTargetRest.NoteFailure(darian, SkillKinds.Mount, SelaGhost, MountSkill.WalkFailedWhy, Start);
        }

        JobTargetRest.NoteSuccess(darian, SkillKinds.Mount, SelaGhost);

        Assert.False(JobTargetRest.Rests(darian, SkillKinds.Mount, SelaGhost.Key, Start));
        Assert.Empty(darian.TargetStreaks);
    }

    [Fact]
    public void Store_ARefusalOfARestingTargetIsNoNewWayOfFailing()
    {
        var runa = Person(Runa);

        for (var i = 0; i < RepeatFailure.Limit; i++)
        {
            JobTargetRest.NoteFailure(runa, StableClaimSkill.SkillName, SelaGhost, StableWalk.WalkFailedWhy, Start);
        }

        var refused = JobTargetRest.NoteFailure(runa, StableClaimSkill.SkillName, SelaGhost, JobTargetRest.RestingWhy, Start);

        Assert.Equal(RepeatFailure.Limit, refused.Count);
        Assert.True(JobTargetRest.Rests(runa, StableClaimSkill.SkillName, SelaGhost.Key, Start));
    }

    [Fact]
    public void Store_TheSavedStreakRestsTheLoadedPersonUntilTheSameMoment()
    {
        // The streak lives in the saved field: a copy of it is what a load after a restart holds.
        var sela = Person(Sela);
        var loaded = Person(Sela);

        for (var i = 0; i < RepeatFailure.Limit; i++)
        {
            JobTargetRest.NoteFailure(sela, SkillKinds.GoTo, WalkToDelucia, NoWalkWithTab, Start);
        }

        foreach (var (name, record) in sela.TargetStreaks)
        {
            loaded.TargetStreaks[name] = record;
        }

        var restEnd = Start + RepeatFailure.SkillCooldown;

        Assert.True(JobTargetRest.Rests(loaded, SkillKinds.GoTo, WalkToDelucia.Key, restEnd - TimeSpan.FromSeconds(1)));
        Assert.False(JobTargetRest.Rests(loaded, SkillKinds.GoTo, WalkToDelucia.Key, restEnd));
        Assert.Equal([DeluciaPassage], JobTargetRest.RestingSpots(loaded, SkillKinds.GoTo, Start));

        // The streak runs on after the load: the next failure the same way makes the rest longer.
        var next = JobTargetRest.NoteFailure(loaded, SkillKinds.GoTo, WalkToDelucia, NoWalkWithTab, restEnd);

        Assert.Equal(RepeatFailure.Limit + 1, next.Count);
        Assert.Equal(NoWalkWithTab, next.Reason);
        Assert.Equal(restEnd + RepeatFailure.CooldownAfter(RepeatFailure.Limit + 1), next.RestUntil);
    }

    [Fact]
    public void RestingSpots_NameTheGoalsOfRestingWalks()
    {
        // Jonas walked for the Delucia passage 21 times: "no route" each time.
        var jonas = Person(Jonas);

        for (var i = 0; i < RepeatFailure.Limit; i++)
        {
            JobTargetRest.NoteFailure(jonas, SkillKinds.GoTo, WalkToDelucia, NoRoute, Start);
        }

        Assert.Contains(DeluciaPassage, JobTargetRest.RestingSpots(jonas, SkillKinds.GoTo, Start));
        Assert.Empty(JobTargetRest.RestingSpots(Person(Katla), SkillKinds.GoTo, Start));
        Assert.Empty(JobTargetRest.RestingSpots(jonas, SkillKinds.Dungeon, Start));
        Assert.Empty(JobTargetRest.RestingSpots(jonas, SkillKinds.GoTo, Start + RepeatFailure.MaxSkillCooldown));
    }

    [Fact]
    public void KeyOf_IsTheSerial()
    {
        var serial = (Serial)Katla;

        Assert.Equal(Katla.ToString(), JobTargetRest.KeyOf(serial));
        Assert.Null(JobTargetRest.KeyOf((Mobile)null));
    }
}
