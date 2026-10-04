using System;
using System.Collections.Generic;
using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class GuardLineRulesTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0);

    [Fact]
    public void ShouldStandDown_OnlyUnderGuards()
    {
        Assert.True(GuardLineRules.ShouldStandDown(selfUnderGuards: true, foeUnderGuards: false));
        Assert.True(GuardLineRules.ShouldStandDown(selfUnderGuards: false, foeUnderGuards: true));
        Assert.True(GuardLineRules.ShouldStandDown(selfUnderGuards: true, foeUnderGuards: true));
        Assert.False(GuardLineRules.ShouldStandDown(selfUnderGuards: false, foeUnderGuards: false));
    }

    [Theory]
    [InlineData(true, true, true, false, true)]
    [InlineData(false, true, true, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, true, true, true, false)]
    public void RedBreaksOff_AnyFightUnderTheGuardsUnlessRunningAlready(
        bool murderer,
        bool underGuards,
        bool fighting,
        bool running,
        bool breaksOff
    ) =>
        Assert.Equal(breaksOff, GuardLineRules.RedBreaksOff(murderer, underGuards, fighting, running));

    [Fact]
    public void InStandDownGrace_InsideWindow_Blocks()
    {
        Assert.True(GuardLineRules.InStandDownGrace(Now, Now.AddMinutes(GuardLineRules.StandDownGrace.TotalMinutes - 0.5)));
        Assert.False(GuardLineRules.InStandDownGrace(Now, Now.Add(GuardLineRules.StandDownGrace)));
        Assert.False(GuardLineRules.InStandDownGrace(default, Now));
    }

    [Fact]
    public void SpeaksAtLine_OnceInTheGrace()
    {
        Assert.True(GuardLineRules.SpeaksAtLine(default, Now));
        Assert.False(GuardLineRules.SpeaksAtLine(Now, Now.Add(GuardLineRules.StandDownGrace).AddSeconds(-1)));
        Assert.True(GuardLineRules.SpeaksAtLine(Now, Now.Add(GuardLineRules.StandDownGrace)));
    }

    [Fact]
    public void LineRing_TheEightPointsAtTheMargin()
    {
        var ring = GuardLineRules.LineRing;

        Assert.Equal(8, ring.Length);
        Assert.Equal(ring.Length, new HashSet<(int, int)>(ring).Count);

        foreach (var (x, y) in ring)
        {
            Assert.Equal(GuardLineRules.LineMarginTiles, Math.Max(Math.Abs(x), Math.Abs(y)));
        }
    }
}
