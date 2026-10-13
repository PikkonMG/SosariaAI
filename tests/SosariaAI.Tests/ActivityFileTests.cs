using System;
using SosariaAI.Logging;
using Xunit;

namespace SosariaAI.Tests;

public class ActivityFileTests
{
    private static readonly DateTime RunEnd = new(2026, 10, 11, 23, 20, 5);

    [Fact]
    public void RunFileName_NamesTheRunByTheTimeItEnded()
    {
        Assert.Equal("activity-20261011-232005.log", ActivityFile.RunFileName(RunEnd));
    }

    [Fact]
    public void Expired_KeepsTheNewestRuns_AndLeavesOtherFilesAlone()
    {
        var names = new[]
        {
            ActivityFile.RunFileName(RunEnd.AddDays(-1)),
            ActivityFile.RunFileName(RunEnd),
            "notes.txt",
            ActivityFile.RunFileName(RunEnd.AddDays(-2)),
            ActivityFile.RunFileName(RunEnd.AddHours(1))
        };

        Assert.Equal(
            [ActivityFile.RunFileName(RunEnd.AddDays(-2)), ActivityFile.RunFileName(RunEnd.AddDays(-1))],
            ActivityFile.Expired(names, 2)
        );
    }

    [Fact]
    public void Expired_IsEmpty_WhileTheRunsFit()
    {
        Assert.Empty(ActivityFile.Expired([ActivityFile.RunFileName(RunEnd)], ActivityFile.KeptRuns));
    }
}
