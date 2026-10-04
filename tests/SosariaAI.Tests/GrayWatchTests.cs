using System;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class GrayWatchTests
{
    private const uint Gray = 0x100;
    private const uint OtherGray = 0x101;
    private const uint First = 0x200;
    private const uint Second = 0x201;
    private const uint Third = 0x202;
    private const uint Fourth = 0x203;
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0);

    [Fact]
    public void TryJoin_AtMostThreeWatchers()
    {
        var watch = new GrayWatch();

        Assert.True(watch.TryJoin(Gray, First));
        Assert.True(watch.TryJoin(Gray, Second));
        Assert.True(watch.TryJoin(Gray, Third));
        Assert.False(watch.TryJoin(Gray, Fourth));
        Assert.True(watch.TryJoin(Gray, First));
        Assert.True(watch.TryJoin(OtherGray, Fourth));
    }

    [Fact]
    public void Leave_OpensAPlace()
    {
        var watch = new GrayWatch();
        watch.TryJoin(Gray, First);
        watch.TryJoin(Gray, Second);
        watch.TryJoin(Gray, Third);
        watch.Leave(Gray, Second);

        Assert.False(watch.IsWatcher(Gray, Second));
        Assert.True(watch.TryJoin(Gray, Fourth));
    }

    [Fact]
    public void Prune_DropsWatchersNoLongerOnTheGray()
    {
        var watch = new GrayWatch();
        watch.TryJoin(Gray, First);
        watch.TryJoin(Gray, Second);
        watch.Prune(Gray, watcher => watcher == First, Now);

        Assert.True(watch.IsWatcher(Gray, First));
        Assert.False(watch.IsWatcher(Gray, Second));

        watch.Prune(Gray, _ => false, Now);
        Assert.False(watch.IsWatcher(Gray, First));
    }

    [Fact]
    public void Prune_ALetGoRestsFromThatGrayOnly()
    {
        var watch = new GrayWatch();
        watch.TryJoin(Gray, First);
        watch.Prune(Gray, _ => false, Now);

        Assert.True(watch.Resting(First, Gray, Now + GrayWatch.ReengageRest - TimeSpan.FromSeconds(1)));
        Assert.False(watch.Resting(First, Gray, Now + GrayWatch.ReengageRest));
        Assert.False(watch.Resting(First, OtherGray, Now));
        Assert.False(watch.Resting(Second, Gray, Now));
    }

    [Fact]
    public void Sweep_ForgetsFinishedGrays()
    {
        var watch = new GrayWatch();
        watch.TryJoin(Gray, First);
        watch.TryJoin(OtherGray, Second);
        watch.Sweep(gray => gray == Gray, Now);

        Assert.False(watch.IsWatcher(Gray, First));
        Assert.True(watch.IsWatcher(OtherGray, Second));
    }

    [Fact]
    public void NoteFlag_KeepsTheFirstSighting_UntilSwept()
    {
        var watch = new GrayWatch();

        Assert.Equal(Now, watch.NoteFlag(Gray, Now));
        Assert.Equal(Now, watch.NoteFlag(Gray, Now + TimeSpan.FromMinutes(1)));

        var later = Now + GrayWatch.SweepGap;
        watch.Sweep(gray => gray == Gray, later);
        Assert.Equal(later, watch.NoteFlag(Gray, later));
    }

    [Fact]
    public void FlagFresh_NoFightOnAFlagRunningOut()
    {
        Assert.True(GrayWatch.FlagFresh(Now, Now + GrayWatch.StaleFlag - TimeSpan.FromSeconds(1)));
        Assert.False(GrayWatch.FlagFresh(Now, Now + GrayWatch.StaleFlag));
    }

    [Fact]
    public void IsWilling_StableAndNudgedByNerve()
    {
        const uint steady = 60;

        Assert.False(GrayWatch.IsWilling(steady, brave: false, cautious: false));
        Assert.True(GrayWatch.IsWilling(steady, brave: true, cautious: false));
        Assert.True(GrayWatch.IsWilling(10, brave: false, cautious: true));
        Assert.False(GrayWatch.IsWilling(30, brave: false, cautious: true));
        Assert.Equal("Criminal! Stop Nyle!", GrayWatch.CallLine("Nyle"));
    }

    [Fact]
    public void IsAnswerable_NotAGrayInAGuildWarOrFactionFight()
    {
        Assert.False(GrayWatch.IsAnswerable(grayInLawfulFight: true));
        Assert.True(GrayWatch.IsAnswerable(grayInLawfulFight: false));
    }

    [Fact]
    public void BystanderCall_ComesAfterAHumanPause()
    {
        Assert.True(GrayWatch.BystanderCallMin > System.TimeSpan.Zero);
        Assert.True(GrayWatch.BystanderCallMax > GrayWatch.BystanderCallMin);
    }

    [Fact]
    public void CallStands_OnlyWhileTheGrayIsStillThereToCatch()
    {
        const int Near = 3;

        Assert.True(GrayWatch.CallStands(grayCriminal: true, grayAlive: true, grayUnderGuards: true, Near));
        Assert.False(GrayWatch.CallStands(grayCriminal: true, grayAlive: true, grayUnderGuards: false, Near));
        Assert.False(GrayWatch.CallStands(grayCriminal: false, grayAlive: true, grayUnderGuards: true, Near));
        Assert.False(GrayWatch.CallStands(grayCriminal: true, grayAlive: true, grayUnderGuards: true, GrayWatch.WatchRange + 1));
    }
}
