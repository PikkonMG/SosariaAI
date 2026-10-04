using Server;
using Moves = Server.Movement.Movement;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class RunRulesTests
{
    [Fact]
    public void StepDelay_MatchesTheMountedAndFootDelays()
    {
        Assert.Equal(Moves.RunFootDelay, RunRules.StepDelay(mounted: false, running: true));
        Assert.Equal(Moves.WalkFootDelay, RunRules.StepDelay(mounted: false, running: false));
        Assert.Equal(Moves.RunMountDelay, RunRules.StepDelay(mounted: true, running: true));
        Assert.Equal(Moves.WalkMountDelay, RunRules.StepDelay(mounted: true, running: false));
    }

    [Theory]
    [InlineData(SkillKinds.GoTo)]
    [InlineData(SkillKinds.GoHome)]
    [InlineData(SkillKinds.VendorBuy)]
    [InlineData(SkillKinds.VendorSell)]
    [InlineData(SkillKinds.BankDeposit)]
    [InlineData(SkillKinds.BuyMount)]
    [InlineData(SkillKinds.Visit)]
    [InlineData(SkillKinds.Lumberjack)]
    [InlineData(SkillKinds.Hunt)]
    [InlineData(SkillKinds.Flee)]
    [InlineData(SkillKinds.Tame)]
    public void RunsErrand_TravelAndErrandSkillsRun(string kind) => Assert.True(RunRules.RunsErrand(kind));

    [Theory]
    [InlineData(SkillKinds.IdleWander)]
    [InlineData(SkillKinds.Loiter)]
    [InlineData(SkillKinds.Rest)]
    [InlineData(SkillKinds.Patrol)]
    [InlineData(SkillKinds.Beg)]
    [InlineData(SkillKinds.Steal)]
    [InlineData(SkillKinds.Snoop)]
    [InlineData(SkillKinds.Herd)]
    [InlineData(SkillKinds.Cook)]
    [InlineData(SkillKinds.Taste)]
    public void RunsErrand_AmbientAndInPlaceSkillsWalk(string kind) => Assert.False(RunRules.RunsErrand(kind));

    [Fact]
    public void RunsNow_ARunnerRuns_AWalkerWalks()
    {
        const long now = 10_000;
        const long noRest = 0;

        Assert.True(RunRules.RunsNow(running: true, now, noRest));
        Assert.False(RunRules.RunsNow(running: false, now, noRest));
    }

    [Fact]
    public void RunsNow_ATiredRiderWalksUntilItsMountHasRested()
    {
        // Den reds kept asking a spent mount to run and stood on one tile for an hour.
        const long refusedAt = 10_000;
        var walksUntil = refusedAt + RunRules.TiredRunRestMs;

        Assert.False(RunRules.RunsNow(running: true, refusedAt, walksUntil));
        Assert.False(RunRules.RunsNow(running: true, walksUntil - 1, walksUntil));
        Assert.True(RunRules.RunsNow(running: true, walksUntil, walksUntil));
    }

    [Fact]
    public void RunsErrand_GhostsHurryToTheHealer() =>
        Assert.True(RunRules.RunsErrand(Skills.GhostSkill.SkillName));
}
