using SosariaAI.Logging;
using Xunit;

namespace SosariaAI.Tests;

public class ActivityLoggingTests
{
    [Fact]
    public void Render_FillsHolesInOrder()
    {
        Assert.Equal(
            "Erol sold 3 stacks to Dillian for 120 gold",
            MessageTemplate.Render("{Name} sold {Count} stacks to {Vendor} for {Gold} gold", ["Erol", 3, "Dillian", 120])
        );
        Assert.Equal("plain", MessageTemplate.Render("plain", []));
        Assert.Equal("a {Missing}", MessageTemplate.Render("{Got} {Missing}", ["a"]));
        Assert.Equal("null at {x}", MessageTemplate.Render("{Name} at {{x}}", [null]));
        Assert.Equal("cost 1.50", MessageTemplate.Render("cost {Value:F2}", [1.5]));
    }

    [Fact]
    public void Line_SaysWhereTheDetailIs()
    {
        var line = ActivityPulse.Line(100, 240, 31, 12, 3, "/srv/activity.log");

        Assert.Contains("100 people", line);
        Assert.Contains("240 steps done", line);
        Assert.Contains("31 failed", line);
        Assert.Contains("12 without a route", line);
        Assert.Contains("3 plans given up", line);
        Assert.EndsWith("/srv/activity.log", line);
    }
}
