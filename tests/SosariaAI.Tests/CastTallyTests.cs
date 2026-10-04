using SosariaAI.Combat;
using SosariaAI.Logging;
using Xunit;

namespace SosariaAI.Tests;

public class CastTallyTests
{
    private const int Minutes = CensusText.CensusMinutes;

    [Fact]
    public void Line_GivesTheInterruptRatePerCastAndByCircle()
    {
        var begun = new int[CastTiming.TopCircle + 1];
        var broken = new int[CastTiming.TopCircle + 1];
        begun[1] = 30;
        broken[1] = 0;
        begun[6] = 20;
        broken[6] = 10;

        var line = CastTally.Line(begun, broken, steppedClear: 12, stood: 4, Minutes);

        Assert.Equal(
            "Casting in the last 10 minutes: 50 casts, 10 broken by blows (20%); " +
            "broken/cast by circle 1: 0/30, 6: 10/20; 12 stepped clear first, 4 cast where they stood",
            line
        );
    }

    [Fact]
    public void Line_NobodyCast_IsNull() =>
        Assert.Null(CastTally.Line(new int[CastTiming.TopCircle + 1], new int[CastTiming.TopCircle + 1], 0, 0, Minutes));
}
