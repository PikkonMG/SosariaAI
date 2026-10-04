using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using Xunit;

namespace SosariaAI.Tests;

public class RepeatFailureTests
{
    private const int CooldownDoubling = 2;
    private const int ManyFailures = 50;
    private const int SlowFailureMinutes = 3;

    private static readonly DateTime Start = new(2026, 9, 24, 6, 26, 0, DateTimeKind.Utc);
    private static readonly Point3D BritainInnStop = new(1439, 1710, 17);
    private static readonly TimeSpan AtOnce = TimeSpan.Zero;
    private static readonly TimeSpan SlowFailure = TimeSpan.FromMinutes(SlowFailureMinutes);

    [Fact]
    public void SkillFailures_BlockAtTheLimit()
    {
        var failures = FailSlowly(RepeatFailure.Limit - 1, Start);
        var far = new Point3D(BritainInnStop.X + RepeatFailure.FailSpotRange + 1, BritainInnStop.Y, BritainInnStop.Z);

        Assert.False(RepeatFailure.IsSkillBlocked(failures, Start, far));

        failures = RepeatFailure.AfterSkillFailure(failures, Start, SlowFailure, BritainInnStop);

        Assert.True(RepeatFailure.IsSkillBlocked(failures, Start, far));
        Assert.Equal(Start + RepeatFailure.SkillCooldown, failures.BlockedUntil);
    }

    [Fact]
    public void SkillFailures_DoNotStartOverWhenTheBlockRunsOut()
    {
        // The Britain cooks failed three times, rested five minutes, and failed three
        // times again all day: the count went back to one when the block ran out.
        var failures = FailSlowly(RepeatFailure.Limit, Start);
        var afterBlock = failures.BlockedUntil + TimeSpan.FromSeconds(1);

        Assert.False(RepeatFailure.IsSkillBlocked(failures, afterBlock, BritainInnStop));

        failures = RepeatFailure.AfterSkillFailure(failures, afterBlock, SlowFailure, BritainInnStop);

        Assert.True(RepeatFailure.IsSkillBlocked(failures, afterBlock, BritainInnStop));
        Assert.Equal(afterBlock + RepeatFailure.SkillCooldown * CooldownDoubling, failures.BlockedUntil);
    }

    [Fact]
    public void CooldownAfter_DoublesUpToTheCap()
    {
        Assert.Equal(TimeSpan.Zero, RepeatFailure.CooldownAfter(RepeatFailure.Limit - 1));
        Assert.Equal(RepeatFailure.SkillCooldown, RepeatFailure.CooldownAfter(RepeatFailure.Limit));
        Assert.Equal(RepeatFailure.SkillCooldown * CooldownDoubling, RepeatFailure.CooldownAfter(RepeatFailure.Limit + 1));
        Assert.Equal(RepeatFailure.MaxSkillCooldown, RepeatFailure.CooldownAfter(ManyFailures));
    }

    [Fact]
    public void QuickFailure_BarsTheSkillAtThatSpotOnly()
    {
        // A cook that failed at its start was picked again four seconds later on the
        // same tile. One quick failure now bars the spot, not the whole world.
        var failures = RepeatFailure.AfterSkillFailure(default, Start, AtOnce, BritainInnStop);
        var near = new Point3D(BritainInnStop.X + RepeatFailure.FailSpotRange, BritainInnStop.Y, BritainInnStop.Z);
        var far = new Point3D(BritainInnStop.X + RepeatFailure.FailSpotRange + 1, BritainInnStop.Y, BritainInnStop.Z);

        Assert.False(RepeatFailure.IsBlocked(failures.Count));
        Assert.True(RepeatFailure.IsSkillBlocked(failures, Start, BritainInnStop));
        Assert.True(RepeatFailure.IsSkillBlocked(failures, Start, near));
        Assert.False(RepeatFailure.IsSkillBlocked(failures, Start, far));
        Assert.False(RepeatFailure.IsSkillBlocked(failures, Start + RepeatFailure.QuickSpotBar, BritainInnStop));
    }

    [Fact]
    public void SlowFailure_BarsTheSpotWhereItEnded_Briefly()
    {
        // Mirabel's walk to a Magincia shop failed after 24 seconds, and the scorer picked
        // the sale again six seconds later on the same tile. A slow failure bars the spot
        // it ended on too, for less time than a failed start.
        var failures = RepeatFailure.AfterSkillFailure(default, Start, SlowFailure, BritainInnStop);
        var far = new Point3D(BritainInnStop.X + RepeatFailure.FailSpotRange + 1, BritainInnStop.Y, BritainInnStop.Z);

        Assert.True(SlowFailure > RepeatFailure.QuickFailure);
        Assert.True(RepeatFailure.SlowSpotBar < RepeatFailure.QuickSpotBar);
        Assert.False(RepeatFailure.IsBlocked(failures.Count));
        Assert.True(RepeatFailure.IsSkillBlocked(failures, Start, BritainInnStop));
        Assert.False(RepeatFailure.IsSkillBlocked(failures, Start, far));
        Assert.False(RepeatFailure.IsSkillBlocked(failures, Start + RepeatFailure.SlowSpotBar, BritainInnStop));
    }

    [Fact]
    public void LaterFailure_MovesTheBarToWhereItFailed()
    {
        var first = RepeatFailure.AfterSkillFailure(default, Start, AtOnce, BritainInnStop);
        var elsewhere = new Point3D(BritainInnStop.X + RepeatFailure.FailSpotRange * 2, BritainInnStop.Y, BritainInnStop.Z);
        var second = RepeatFailure.AfterSkillFailure(first, Start, SlowFailure, elsewhere);

        Assert.True(RepeatFailure.IsSkillBlocked(second, Start, elsewhere));
        Assert.False(RepeatFailure.IsSkillBlocked(second, Start, BritainInnStop));
    }

    private static SkillFailures FailSlowly(int times, DateTime at)
    {
        SkillFailures failures = default;

        for (var i = 0; i < times; i++)
        {
            failures = RepeatFailure.AfterSkillFailure(failures, at, SlowFailure, BritainInnStop);
        }

        return failures;
    }

    [Fact]
    public void AfterThreeFailures_IsBlocked()
    {
        var count = 0;
        count = RepeatFailure.AfterFailure(null, "despise", count);
        count = RepeatFailure.AfterFailure("despise", "despise", count);
        Assert.False(RepeatFailure.IsBlocked(count));
        count = RepeatFailure.AfterFailure("despise", "despise", count);
        Assert.True(RepeatFailure.IsBlocked(count));
        Assert.Equal(RepeatFailure.Limit, count);
        Assert.Equal(0, RepeatFailure.AfterSuccess());
        Assert.Equal(1, RepeatFailure.AfterFailure("despise", "graveyard", count));
    }

    [Fact]
    public void DecideAndBank_DoNotCountOrClearAFailedDungeon()
    {
        Assert.False(RepeatFailure.Counts(SkillKinds.Decide));
        Assert.False(RepeatFailure.Counts(SkillKinds.IdleWander));
        Assert.False(RepeatFailure.Counts(SkillKinds.Rest));
        Assert.True(RepeatFailure.Counts(SkillKinds.Dungeon));
        Assert.True(RepeatFailure.Counts(SkillKinds.GoTo));
        Assert.True(RepeatFailure.Counts(SkillKinds.VendorSell));
        Assert.True(RepeatFailure.Counts(SkillKinds.UpgradeGear));
        Assert.True(RepeatFailure.Clears(SkillKinds.Dungeon));
        Assert.True(RepeatFailure.Clears(SkillKinds.VendorSell));
        Assert.True(RepeatFailure.Clears(SkillKinds.House));
        Assert.False(RepeatFailure.Clears(SkillKinds.GoTo));
        Assert.False(RepeatFailure.Clears(SkillKinds.BankDeposit));
        Assert.False(RepeatFailure.Clears(SkillKinds.Decide));
        Assert.False(RepeatFailure.Clears(SkillKinds.Sightsee));
        Assert.False(RepeatFailure.Clears(SkillKinds.Loiter));
        Assert.False(RepeatFailure.Clears(SkillKinds.Tavern));
        Assert.False(RepeatFailure.Clears(SkillKinds.Visit));
    }

    [Fact]
    public void CoolsDown_PlaceBoundFillers_Only()
    {
        // Idle and rest refuse on a bad spot; cooling the skill stops the
        // start-fail-restart loop without touching the errand streak.
        Assert.True(RepeatFailure.CoolsDown(SkillKinds.IdleWander));
        Assert.True(RepeatFailure.CoolsDown(SkillKinds.Rest));
        Assert.False(RepeatFailure.CoolsDown(SkillKinds.Decide));
        Assert.False(RepeatFailure.CoolsDown(SkillKinds.Dungeon));
        Assert.False(RepeatFailure.CoolsDown(SkillKinds.GoTo));
        Assert.False(RepeatFailure.CoolsDown(null));
    }

    [Fact]
    public void RestsAfterDone_Sightsee_Only()
    {
        // A finished outing stays off the menu a while; failures already cool
        // down, but a success never did, so the same sight repeated forever.
        Assert.True(RepeatFailure.RestsAfterDone(SkillKinds.Sightsee));
        Assert.False(RepeatFailure.RestsAfterDone(SkillKinds.Mine));
        Assert.False(RepeatFailure.RestsAfterDone(SkillKinds.VendorSell));
        Assert.False(RepeatFailure.RestsAfterDone(SkillKinds.Tavern));
        Assert.False(RepeatFailure.RestsAfterDone(SkillKinds.Decide));
        Assert.False(RepeatFailure.RestsAfterDone(null));
    }

    [Fact]
    public void FailedLine_SaysItLikeAPlayer_NotThePlanner()
    {
        // "I could not seek player conflict today" came out of the scorer's own names.
        for (var seed = 0; seed < 50; seed++)
        {
            var line = SpeechLines.FailedLine("work:Conflict:0", seed);

            Assert.False(string.IsNullOrWhiteSpace(line));
            Assert.DoesNotContain(":", line, StringComparison.Ordinal);
            Assert.DoesNotContain("conflict", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("could not", line, StringComparison.OrdinalIgnoreCase);
        }

        Assert.False(string.IsNullOrWhiteSpace(SpeechLines.FailedLine(null, 0)));
        Assert.DoesNotContain(":", SpeechLines.FailedLine("work:GoTo:1392,1724,5", 0), StringComparison.Ordinal);
    }

    [Fact]
    public void FailedLine_GerundKind_NamesTheWorkNowAndThen()
    {
        var named = 0;

        for (var seed = 0; seed < 100; seed++)
        {
            if (SpeechLines.FailedLine("work:Mine:1425,1695,0", seed).Contains("mining", StringComparison.OrdinalIgnoreCase))
            {
                named++;
            }
        }

        Assert.True(named > 0);
    }

    [Fact]
    public void ErrandsThatFailToWalk_CountTowardTheBlock()
    {
        // A Cove buyer with no route to Minoc started VendorBuy, failed, and started it
        // again three seconds later, all day. An errand that fails is a failure.
        Assert.True(RepeatFailure.Counts(SkillKinds.VendorBuy));
        Assert.True(RepeatFailure.Counts(SkillKinds.BankDeposit));
        Assert.True(RepeatFailure.Counts(SkillKinds.BankShop));
        Assert.True(RepeatFailure.Counts(SkillKinds.Follow));
    }
}
