using System.Collections.Generic;
using Server;
using SosariaAI.Logging;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class StallTallyTests
{
    private const int Minutes = CensusText.CensusMinutes;

    [Fact]
    public void Line_NothingStalled_IsNull()
    {
        Assert.Null(StallTally.Line(new Dictionary<(Point3D At, Point3D Leg), int>(), Minutes));
        Assert.Null(StallTally.Line(null, Minutes));
    }

    [Fact]
    public void Line_NamesTheWorstTilesMostFirstAndTheTotal()
    {
        var counts = new Dictionary<(Point3D At, Point3D Leg), int>();

        for (var i = 0; i < StallTally.TopTiles + 2; i++)
        {
            counts[(new Point3D(i, 0, 0), new Point3D(i, 1, 0))] = i + 1;
        }

        var line = StallTally.Line(counts, Minutes);

        Assert.StartsWith($"Walk stalls in the last {Minutes} minutes: 28;", line);
        Assert.Contains("7 at (6, 0, 0) on leg (6, 1, 0); 6 at (5, 0, 0)", line);
        Assert.Contains("3 at (2, 0, 0)", line);
        Assert.DoesNotContain("2 at (1, 0, 0)", line);
    }
}
