using System;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>A way out of a dungeon the walk home cannot leave, in a player's order.</summary>
public class DungeonEscapeRulesTests
{
    private const string BritainSewer = "Britain Sewer";
    private const string Despise = "Despise";

    private static readonly DateTime Start = new(2026, 9, 27, 0, 17, 42, DateTimeKind.Utc);

    [Fact]
    public void Track_KeepsTheFirstTryInOneDungeon_AndForgetsOutside()
    {
        var first = DungeonEscapeRules.Track(default, BritainSewer, Start);
        var later = DungeonEscapeRules.Track(first, BritainSewer, Start + DungeonEscapeRules.StuckBeforeDoor);

        Assert.Equal(new DungeonTrouble(BritainSewer, Start), first);
        Assert.Equal(first, later);
        Assert.Equal(Start + DungeonEscapeRules.StuckBeforeDoor, DungeonEscapeRules.Track(first, Despise, Start + DungeonEscapeRules.StuckBeforeDoor).Since);
        Assert.Equal(default(DungeonTrouble), DungeonEscapeRules.Track(first, null, Start));
    }

    [Fact]
    public void IsStuck_AfterFiveMinutesOfFailedWalksHome()
    {
        // Kaisa stood on one Britain Sewer tile for forty minutes, every walk home failing.
        var trouble = new DungeonTrouble(BritainSewer, Start);

        Assert.False(DungeonEscapeRules.IsStuck(trouble, Start));
        Assert.False(DungeonEscapeRules.IsStuck(trouble, Start + DungeonEscapeRules.StuckBeforeDoor - TimeSpan.FromSeconds(1)));
        Assert.True(DungeonEscapeRules.IsStuck(trouble, Start + DungeonEscapeRules.StuckBeforeDoor));
        Assert.False(DungeonEscapeRules.IsStuck(default, Start + DungeonEscapeRules.StuckBeforeDoor));
    }

    [Fact]
    public void NeedsWayOut_OnlyInsideTheDungeonAWalkHomeFailedFrom()
    {
        var failed = DungeonEscapeRules.Track(default, BritainSewer, Start);

        Assert.True(DungeonEscapeRules.NeedsWayOut(failed, BritainSewer));
        Assert.False(DungeonEscapeRules.NeedsWayOut(default, BritainSewer));

        // A walk home that failed in the sewer, or in town, says nothing about Despise.
        Assert.False(DungeonEscapeRules.NeedsWayOut(failed, Despise));
        Assert.False(DungeonEscapeRules.NeedsWayOut(failed, null));
    }

    [Fact]
    public void AfterMove_KeepsTheTroubleOnlyInItsOwnDungeon()
    {
        // A hunter whose one walk home failed left the sewer by a pad: back in later, it is
        // not five minutes stuck, and goes nowhere near the door.
        var failed = DungeonEscapeRules.Track(default, BritainSewer, Start);

        Assert.Equal(failed, DungeonEscapeRules.AfterMove(failed, BritainSewer));
        Assert.Equal(default(DungeonTrouble), DungeonEscapeRules.AfterMove(failed, null));
        Assert.Equal(default(DungeonTrouble), DungeonEscapeRules.AfterMove(failed, Despise));
        Assert.Equal(default(DungeonTrouble), DungeonEscapeRules.AfterMove(default, BritainSewer));
    }

    [Fact]
    public void Next_RecallThenPadThenDoorOnceStuck()
    {
        Assert.Equal(DungeonWayOut.Recall, DungeonEscapeRules.Next(true, false, true, false, true));
        Assert.Equal(DungeonWayOut.ExitPad, DungeonEscapeRules.Next(true, true, true, false, true));
        Assert.Equal(DungeonWayOut.ExitPad, DungeonEscapeRules.Next(false, false, true, false, false));
        Assert.Equal(DungeonWayOut.Door, DungeonEscapeRules.Next(false, false, true, true, true));
        Assert.Equal(DungeonWayOut.Door, DungeonEscapeRules.Next(false, false, false, false, true));

        // Not stuck long enough and nothing else left: the walk home is tried again.
        Assert.Equal(DungeonWayOut.None, DungeonEscapeRules.Next(false, false, false, false, false));
        Assert.Equal(DungeonWayOut.None, DungeonEscapeRules.Next(true, true, true, true, false));
    }

    [Fact]
    public void DoorLine_NamesTheDungeonAndBothSpots()
    {
        var line = DungeonEscapeRules.DoorLine("Kaisa", BritainSewer, new Server.Point3D(6032, 1498, 22), new Server.Point3D(1492, 1641, 0));

        Assert.Contains(BritainSewer, line, StringComparison.Ordinal);
        Assert.Contains("(6032,1498)", line, StringComparison.Ordinal);
        Assert.Contains("(1492,1641)", line, StringComparison.Ordinal);
    }
}
