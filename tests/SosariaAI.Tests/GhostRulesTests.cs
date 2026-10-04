using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class GhostRulesTests
{
    private static readonly DateTime Start = new(2026, 9, 9, 12, 0, 0);

    public GhostRulesTests() => TestMap.EnsureInternal();

    [Fact]
    public void ShouldFallback_NeverWhileTheGhostMakesHeadway()
    {
        var late = Start + GhostRules.BackstopAfter;

        Assert.False(GhostRules.ShouldFallback(late, Start, late - TimeSpan.FromMinutes(1)));
        Assert.False(GhostRules.ShouldFallback(late, default, default));
    }

    [Fact]
    public void ShouldFallback_OnlyLongDeadAndLongStuck()
    {
        var stuckSince = Start;

        Assert.False(GhostRules.ShouldFallback(Start + TimeSpan.FromMinutes(10), Start, stuckSince));
        Assert.False(GhostRules.ShouldFallback(Start + GhostRules.StuckFor, Start, stuckSince));
        Assert.True(GhostRules.ShouldFallback(Start + GhostRules.FallbackAfter, Start, stuckSince));
        Assert.False(
            GhostRules.ShouldFallback(Start + GhostRules.FallbackAfter, Start, Start + GhostRules.FallbackAfter - GhostRules.StuckFor + TimeSpan.FromMinutes(1))
        );
    }

    [Fact]
    public void ShouldFallbackNoWay_AfterAShortWaitWithNoHeadway_NotTheHalfHour()
    {
        // Blues on the Deceit island had no road to any healer and waited out the half hour.
        var noWay = Start + GhostRules.NoWayFallbackAfter;

        Assert.True(GhostRules.NoWayFallbackAfter < GhostRules.FallbackAfter);
        Assert.False(GhostRules.ShouldFallbackNoWay(noWay - TimeSpan.FromSeconds(1), Start));
        Assert.True(GhostRules.ShouldFallbackNoWay(noWay, Start));
        Assert.False(GhostRules.ShouldFallbackNoWay(noWay, noWay - TimeSpan.FromSeconds(1)));
        Assert.True(GhostRules.ShouldFallbackNoWay(Start, default));
    }

    [Fact]
    public void BackstopDelay_TwoHoursAfterDeath_NeverSoonerThanTheStuckWait()
    {
        Assert.Equal(GhostRules.BackstopAfter, GhostRules.BackstopDelay(Start, Start));
        Assert.Equal(GhostRules.BackstopAfter, GhostRules.BackstopDelay(Start, default));
        Assert.Equal(TimeSpan.FromMinutes(90), GhostRules.BackstopDelay(Start + TimeSpan.FromMinutes(30), Start));

        // A ghost kept over a restart: the old death is long past, and it still gets its walk.
        Assert.Equal(GhostRules.StuckFor, GhostRules.BackstopDelay(Start + TimeSpan.FromHours(5), Start));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(7L)]
    [InlineData(0x7FFFFFFFL)]
    public void HauntFor_StaysInsideTheWindow(long serial)
    {
        var haunt = GhostRules.HauntFor(serial);

        Assert.InRange(haunt, GhostRules.HauntMin, GhostRules.HauntMax);
    }

    [Fact]
    public void NextRoute_HelperFirst_ThenDungeonExit_ThenShrine()
    {
        Assert.Equal(GhostPhase.AskHelper, GhostRules.NextRoute(helperInSight: true, underground: true));
        Assert.Equal(GhostPhase.AskHelper, GhostRules.NextRoute(helperInSight: true, underground: false));
        Assert.Equal(GhostPhase.ExitDungeon, GhostRules.NextRoute(helperInSight: false, underground: true));
        Assert.Equal(GhostPhase.WalkToShrine, GhostRules.NextRoute(helperInSight: false, underground: false));
    }

    [Fact]
    public void AfterRaised_RunsToTheCorpse()
    {
        Assert.Equal(GhostPhase.CorpseRun, GhostRules.AfterRaised());
    }

    [Fact]
    public void AskIsDue_FirstAtOnce_SecondAfterRepeat_NoThird()
    {
        Assert.True(GhostRules.AskIsDue(Start, default, asks: 0));
        Assert.False(GhostRules.AskIsDue(Start + TimeSpan.FromSeconds(1), Start, asks: 1));
        Assert.True(GhostRules.AskIsDue(Start + GhostRules.AskRepeat, Start, asks: 1));
        Assert.False(
            GhostRules.AskIsDue(Start + GhostRules.AskRepeat + GhostRules.AskRepeat, Start, GhostRules.MaxAsksPerHelper)
        );
    }

    [Fact]
    public void AskExpired_AfterTheWait_NeverBeforeTheFirstAsk()
    {
        Assert.False(GhostRules.AskExpired(Start, default));
        Assert.False(GhostRules.AskExpired(Start + TimeSpan.FromSeconds(1), Start));
        Assert.True(GhostRules.AskExpired(Start + GhostRules.AskWait, Start));
    }

    [Fact]
    public void StillRefused_UntilTheMarkRunsOut()
    {
        Assert.True(GhostRules.StillRefused(Start, Start + GhostRules.HelperRefuseFor));
        Assert.False(GhostRules.StillRefused(Start + GhostRules.HelperRefuseFor, Start + GhostRules.HelperRefuseFor));
    }

    [Fact]
    public void GhostLines_PleaAndWail()
    {
        Assert.Equal("rez plz", Talk.Line(TalkCategory.GhostPlea, 0, default));
        Assert.Equal("noooo", Talk.Line(TalkCategory.GhostHaunt, 0, default));
        Assert.Equal("ty!", Talk.Line(TalkCategory.ResThanks, 0, default));
    }

    [Fact]
    public void MayRaiseInPlace_NotBesideTheKiller()
    {
        Assert.False(GhostRules.MayRaiseInPlace(killerNearby: true));
        Assert.True(GhostRules.MayRaiseInPlace(killerNearby: false));
    }

    [Fact]
    public void GhostSkill_IsRaisedPhase_OnlyAfterTheRaise()
    {
        Assert.True(GhostSkill.IsRaisedPhase(GhostPhase.CorpseRun));
        Assert.False(GhostSkill.IsRaisedPhase(GhostPhase.Haunt));
        Assert.False(GhostSkill.IsRaisedPhase(GhostPhase.Fallback));
        Assert.False(GhostSkill.IsRaisedPhase(GhostPhase.AskHelper));
        Assert.False(GhostSkill.IsRaisedPhase(GhostPhase.WalkToShrine));
    }

    [Fact]
    public void GhostSkill_MaySeekAgain_AfterTheWait()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0);

        var wait = GhostRules.SeekRetryDelay(1);

        Assert.True(GhostSkill.MaySeekAgain(now, default));
        Assert.False(GhostSkill.MaySeekAgain(now, now + wait));
        Assert.True(GhostSkill.MaySeekAgain(now + wait, now + wait));
    }

    [Fact]
    public void SeekRetryDelay_DoublesAfterEachMiss_UpToTheCap()
    {
        Assert.Equal(GhostRules.SeekRetryFirst, GhostRules.SeekRetryDelay(0));
        Assert.Equal(GhostRules.SeekRetryFirst, GhostRules.SeekRetryDelay(1));
        Assert.Equal(GhostRules.SeekRetryFirst * 2, GhostRules.SeekRetryDelay(2));
        Assert.Equal(GhostRules.SeekRetryFirst * 4, GhostRules.SeekRetryDelay(3));
        Assert.Equal(GhostRules.SeekRetryMax, GhostRules.SeekRetryDelay(64));
        Assert.True(GhostRules.SeekRetryMax < GhostRules.StuckFor);
    }

    [Fact]
    public void KeepsHaunting_UntilTheWailEnds()
    {
        var hauntUntil = Start + GhostRules.HauntMin;

        Assert.True(GhostRules.KeepsHaunting(Start, hauntUntil, criminal: false, murderer: false, Start));
        Assert.False(GhostRules.KeepsHaunting(hauntUntil, hauntUntil, criminal: false, murderer: false, Start));
    }

    [Fact]
    public void KeepsHaunting_AGrayGhostWaitsOutItsFlagByTheBody()
    {
        var hauntUntil = Start + GhostRules.HauntMin;
        var later = hauntUntil + TimeSpan.FromSeconds(30);

        Assert.True(GhostRules.KeepsHaunting(later, hauntUntil, criminal: true, murderer: false, Start));
        Assert.False(GhostRules.KeepsHaunting(Start + GhostRules.GrayWaitLimit, hauntUntil, criminal: true, murderer: false, Start));

        // A red is turned away by every healer whatever its flag: it sets off for a shrine.
        Assert.False(GhostRules.KeepsHaunting(later, hauntUntil, criminal: true, murderer: true, Start));
        Assert.False(GhostRules.KeepsHaunting(later, hauntUntil, criminal: true, murderer: false, default));
    }

    [Fact]
    public void GhostSkill_BeginsOnTheLiving_OnlyWhenMadeForARaise()
    {
        var living = new SosariaCharacter((Serial)0x6A01) { Name = "Annora" };

        Assert.False(new GhostSkill().Begin(living));
        Assert.True(GhostSkill.AfterRaise().Begin(living));
    }
}
